import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { getGetUnitExamOverviewQueryKey } from '@/shared/api/generated/exams/exams';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { getGetSessionHistoryQueryKey } from '@/shared/api/generated/progress/progress';
import { examItem, examSessionId, examUnitId, openExam, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const session = openExam([examItem(1), examItem(2)]);

const recordRequests = () => {
  const requests: string[] = [];
  server.use(
    getGetExamSessionMockHandler(session),
    http.put('*/api/exams/:sessionId/answers/:questionId', ({ params }) => {
      requests.push('PUT');
      return HttpResponse.json({ questionId: params.questionId, answerSavedAt: '2026-09-28T10:05:00Z' });
    }),
    http.post('*/api/exams/:sessionId/submit', () => {
      requests.push('POST');
      return HttpResponse.json(submittedExam(session.items));
    }),
  );
  return requests;
};

const firstQuestionRadio = async (name: string) =>
  within(await screen.findByRole('article', { name: 'Question 1 of 2' })).getByRole('radio', { name });

const openExamPage = () => renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

describe('ExamPage submit', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('asks for confirmation with the unanswered count', async () => {
    recordRequests();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('4'));
    await user.click(screen.getByRole('button', { name: 'Submit exam' }));

    const dialog = await screen.findByRole('dialog', { name: 'Submit the exam?' });
    expect(within(dialog).getByText('1 question has no answer.')).toBeInTheDocument();
  });

  it('keeps the exam open when the confirmation is cancelled', async () => {
    const requests = recordRequests();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await screen.findByRole('button', { name: 'Submit exam' }));
    await user.click(await screen.findByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByRole('heading', { name: 'Exam: Mechanics' })).toBeInTheDocument();
    expect(requests).not.toContain('POST');
  });

  it('saves pending answers before submitting', async () => {
    const requests = recordRequests();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await firstQuestionRadio('4'));
    await user.click(screen.getByRole('button', { name: 'Submit exam' }));
    await user.click(await screen.findByRole('button', { name: 'Submit' }));

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    expect(requests).toEqual(['PUT', 'POST']);
  });

  it('opens the result after submitting', async () => {
    recordRequests();
    const user = userEvent.setup();
    openExamPage();

    await user.click(await screen.findByRole('button', { name: 'Submit exam' }));
    await user.click(await screen.findByRole('button', { name: 'Submit' }));

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    expect(screen.getByText('80 / 100')).toBeInTheDocument();
  });

  it('marks the exam start page and the history stale after submitting', async () => {
    recordRequests();
    const user = userEvent.setup();
    const { queryClient } = openExamPage();
    queryClient.setQueryData(getGetUnitExamOverviewQueryKey(examUnitId), { unitId: examUnitId });
    queryClient.setQueryData(getGetSessionHistoryQueryKey(), { items: [] });

    await user.click(await screen.findByRole('button', { name: 'Submit exam' }));
    await user.click(await screen.findByRole('button', { name: 'Submit' }));

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    expect(queryClient.getQueryState(getGetUnitExamOverviewQueryKey(examUnitId))?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(getGetSessionHistoryQueryKey())?.isInvalidated).toBe(true);
  });

  it('submits automatically when the time runs out', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-28T10:00:00Z'));
    const requests: string[] = [];
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      getGetExamSessionMockHandler(openExam(session.items, { deadline: '2026-09-28T10:00:03Z' })),
      http.post('*/api/exams/:sessionId/submit', async () => {
        requests.push('POST');
        await released;
        return HttpResponse.json(submittedExam(session.items));
      }),
    );
    openExamPage();

    await screen.findByRole('timer');
    vi.setSystemTime(new Date('2026-09-28T10:00:05Z'));

    expect(await screen.findByText('Time is up. Submitting your exam…')).toBeInTheDocument();
    release();
    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
    expect(requests).toEqual(['POST']);
  });
});
