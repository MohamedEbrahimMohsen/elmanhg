import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import type { ExamSessionResult } from '@/shared/api/generated/model';
import { examItem, examSessionId, openExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
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
    { id: 'i5', text: 'Engine' },
  ],
};

const recordSaves = (session: ExamSessionResult) => {
  const bodies: { answer: unknown }[] = [];
  server.use(
    getGetExamSessionMockHandler(session),
    http.put('*/api/exams/:sessionId/answers/:questionId', async ({ request, params }) => {
      bodies.push((await request.json()) as { answer: unknown });
      return HttpResponse.json({ questionId: params.questionId, answerSavedAt: '2026-09-28T10:05:00Z' });
    }),
  );
  return bodies;
};

const openExamPage = () => renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

describe('ExamPage drag and drop', () => {
  it('autosaves placements', async () => {
    const bodies = recordSaves(openExam([examItem(1, { type: 'DragDrop', body, maxScore: 4 }), examItem(2)]));
    const user = userEvent.setup();
    openExamPage();

    await user.click(await screen.findByRole('button', { name: 'Nucleus' }));
    await user.click(screen.getByRole('button', { name: 'Place here (Nucleus in zone 1)' }));

    await waitFor(() => {
      expect(bodies.at(-1)).toEqual({ answer: { placements: [{ zoneId: 'z1', itemIds: ['i1'] }] } });
    });
  });

  it('restores saved placements on resume', async () => {
    const savedAnswer = { placements: [{ zoneId: 'z1', itemIds: ['i1'] }] };
    recordSaves(openExam([examItem(1, { type: 'DragDrop', body, maxScore: 4, savedAnswer }), examItem(2)]));
    openExamPage();

    const zone1 = within(await screen.findByRole('listitem', { name: 'Zone 1' }));

    expect(zone1.getByRole('button', { name: 'Nucleus' })).toBeInTheDocument();
  });
});
