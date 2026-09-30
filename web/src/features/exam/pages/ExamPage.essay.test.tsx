import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { essayExamItem, examItem, examSessionId, openExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('ExamPage essay', () => {
  it('autosaves an essay answer to the server', async () => {
    server.use(getGetExamSessionMockHandler(openExam([essayExamItem(1, { body: { maxWords: 50 } }), examItem(2)])));
    const bodies: unknown[] = [];
    server.use(
      http.put('*/api/exams/:sessionId/answers/:questionId', async ({ request, params }) => {
        bodies.push(await request.json());
        return HttpResponse.json({ questionId: params.questionId, answerSavedAt: '2026-09-28T10:05:00Z' });
      }),
    );
    const user = userEvent.setup();
    renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

    const article = await screen.findByRole('article', { name: 'Question 1 of 2' });
    await user.type(within(article).getByRole('textbox', { name: 'Your essay' }), 'مقال قصير');

    expect(await screen.findByText(/^Saved \d/)).toBeInTheDocument();
    await waitFor(() => {
      expect(bodies.at(-1)).toEqual({ answer: { text: 'مقال قصير' } });
    });
    expect(within(article).getByText('2 of 50 words')).toBeInTheDocument();
  });
});
