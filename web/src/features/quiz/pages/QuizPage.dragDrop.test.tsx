import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SessionItemResult } from '@/shared/api/generated/model';
import {
  getGetSessionMockHandler,
  getSubmitSessionAnswerMockHandler,
} from '@/shared/api/generated/sessions/sessions.msw';
import { server } from '@/test/msw/server';
import { answered, quizItem, quizSession, quizSessionId } from '@/test/quizFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const body = {
  image: {
    key: 'question-diagrams/l/abc.png',
    url: '/api/media/question-diagrams/l/abc.png',
    width: 800,
    height: 600,
    alt: 'Plant cell',
  },
  zones: [
    { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
    { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
  ],
  items: [
    { id: 'i1', text: 'Nucleus' },
    { id: 'i2', text: 'Vacuole' },
    { id: 'i3', text: 'Wall' },
    { id: 'i4', text: 'Membrane' },
    { id: 'i5', text: 'Engine' },
  ],
};
const key = {
  zones: [
    { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
    { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
  ],
};
const dragDrop = quizItem(1, { type: 'DragDrop', body, maxScore: 4, stem: '<p>Label the plant cell.</p>' });
const tally = 'Items in the right place: 1 of 4; distractors placed: 0.';

const answeredDragDrop = (answer: object): SessionItemResult => ({
  ...answered(dragDrop, 'Partial', answer, { score: 1, normalisedScore: 0.25, feedback: tally }),
  correctAnswer: key,
});

const recordSubmits = () => {
  const bodies: { answer: unknown }[] = [];
  server.use(
    getSubmitSessionAnswerMockHandler(async ({ request }) => {
      const sent = (await request.json()) as { answer: object };
      bodies.push(sent);
      return answeredDragDrop(sent.answer);
    }),
  );
  return bodies;
};

const openQuiz = () => renderApp(`/student/quiz/${quizSessionId}`, { session: testSessions.student });

async function placeNucleus(user: UserEvent) {
  await user.click(await screen.findByRole('button', { name: 'Nucleus' }));
  await user.click(screen.getByRole('button', { name: 'Place here (Nucleus in zone 1)' }));
}

describe('QuizPage drag and drop', () => {
  beforeEach(() => {
    server.use(getGetSessionMockHandler(quizSession([dragDrop, quizItem(2)])));
  });

  it('sends the placements on check', async () => {
    const bodies = recordSubmits();
    const user = userEvent.setup();
    openQuiz();

    await placeNucleus(user);
    await user.click(screen.getByRole('button', { name: 'Check' }));

    await waitFor(() => {
      expect(bodies[0]?.answer).toEqual({ placements: [{ zoneId: 'z1', itemIds: ['i1'] }] });
    });
  });

  it('asks for an answer when nothing is placed', async () => {
    const bodies = recordSubmits();
    const user = userEvent.setup();
    openQuiz();
    await screen.findByRole('button', { name: 'Nucleus' });

    await user.click(screen.getByRole('button', { name: 'Check' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Answer the question first.');
    expect(bodies).toEqual([]);
  });

  it('shows marks, score, feedback line and the correct placements after check', async () => {
    recordSubmits();
    const user = userEvent.setup();
    openQuiz();

    await placeNucleus(user);
    await user.click(screen.getByRole('button', { name: 'Check' }));

    const feedback = within(await screen.findByRole('group', { name: 'Answer feedback' }));
    expect(feedback.getByRole('status')).toHaveTextContent('Partially correct');
    expect(feedback.getByText(tally)).toBeInTheDocument();
    expect(await feedback.findByText('Zone 2, in order: Membrane → Wall')).toBeInTheDocument();
    const zone1 = within(screen.getByRole('listitem', { name: 'Zone 1' }));
    expect(zone1.getByRole('listitem')).toHaveTextContent('Nucleus in the right place');
  });
});
