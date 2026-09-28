import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetExamSessionMockHandler, getSubmitExamMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { examItem, examSessionId, openExam, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const session = openExam([examItem(1), examItem(2)]);

const recordSaves = (respond?: () => Response) => {
  const bodies: unknown[] = [];
  server.use(
    http.put('*/api/exams/:sessionId/answers/:questionId', async ({ request, params }) => {
      bodies.push(await request.json());
      return respond?.() ?? HttpResponse.json({ questionId: params.questionId, answerSavedAt: '2026-09-28T10:05:00Z' });
    }),
  );
  return bodies;
};

const firstQuestionRadio = async (name: string) =>
  within(await screen.findByRole('article', { name: 'Question 1 of 2' })).getByRole('radio', { name });

const openExamPage = () => renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

describe('ExamPage auto-save', () => {
  it('saves a chosen option after the auto-save delay', async () => {
    server.use(getGetExamSessionMockHandler(session));
    const bodies = recordSaves();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('4'));

    expect(await screen.findByText(/^Saved \d/)).toBeInTheDocument();
    expect(bodies).toEqual([{ answer: { optionId: 'b' } }]);
  });

  it('saves only the latest answer after quick changes', async () => {
    server.use(getGetExamSessionMockHandler(session));
    const bodies = recordSaves();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('3'));
    await user.click(await firstQuestionRadio('5'));

    expect(await screen.findByText(/^Saved \d/)).toBeInTheDocument();
    expect(bodies).toEqual([{ answer: { optionId: 'c' } }]);
  });

  it('shows a save error when saving fails', async () => {
    server.use(getGetExamSessionMockHandler(session));
    recordSaves(() => HttpResponse.json({ title: 'Boom' }, { status: 500 }));
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('4'));

    expect(await screen.findByText('Not saved. Change your answer to try again.')).toBeInTheDocument();
  });

  it('submits the exam when a save reports the time is over', async () => {
    let submitted = false;
    server.use(
      getGetExamSessionMockHandler(session),
      getSubmitExamMockHandler(() => {
        submitted = true;
        return submittedExam(session.items);
      }),
    );
    recordSaves(() => HttpResponse.json({ code: 'EXAM_TIME_EXPIRED' }, { status: 400 }));
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('4'));

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    await waitFor(() => {
      expect(submitted).toBe(true);
    });
  });
});
