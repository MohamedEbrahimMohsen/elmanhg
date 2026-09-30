import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { examItem, examSessionId, openExam, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const math = examItem(1, { type: 'MathSteps', body: {}, maxScore: 2 });
const session = openExam([math, examItem(2)]);
const draftKey = `elmanhg.mathDraft.s1.${examSessionId}.${math.questionId}`;

const recordSaves = () => {
  const bodies: { answer: { finalAnswer?: string } }[] = [];
  server.use(
    getGetExamSessionMockHandler(session),
    http.put('*/api/exams/:sessionId/answers/:questionId', async ({ request, params }) => {
      bodies.push((await request.json()) as { answer: { finalAnswer?: string } });
      return HttpResponse.json({ questionId: params.questionId, answerSavedAt: '2026-09-28T10:05:00Z' });
    }),
    http.post('*/api/exams/:sessionId/submit', () => HttpResponse.json(submittedExam(session.items))),
  );
  return bodies;
};

const openExamPage = () => renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

describe('ExamPage math with steps', () => {
  it('autosaves the math answer', async () => {
    const bodies = recordSaves();
    const user = userEvent.setup();
    openExamPage();

    await user.type(await screen.findByRole('textbox', { name: 'Final answer' }), 'x = 2');

    await waitFor(() => {
      expect(bodies.at(-1)?.answer.finalAnswer).toBe('x = 2');
    });
  });

  it('autosaves a restored draft on open', async () => {
    localStorage.setItem(
      draftKey,
      JSON.stringify({ savedAt: Date.now(), answer: { steps: [], finalAnswer: 'x = 3' } }),
    );
    const bodies = recordSaves();
    openExamPage();

    await waitFor(() => {
      expect(bodies.at(-1)?.answer.finalAnswer).toBe('x = 3');
    });
  });

  it('clears math drafts after submitting', async () => {
    recordSaves();
    const user = userEvent.setup();
    openExamPage();

    await user.type(await screen.findByRole('textbox', { name: 'Final answer' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Submit exam' }));
    await user.click(await screen.findByRole('button', { name: 'Submit' }));

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    expect(localStorage.getItem(draftKey)).toBeNull();
  });
});
