import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { getGetAvatarStatusMockHandler } from '@/shared/api/generated/avatar/avatar.msw';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { avatarStatus } from '@/test/avatarFixtures';
import { examItem, examSecondUnitId, examSessionId, examUnitId, openExam, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openExamPage = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student, lng });

const freezeClock = () => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-09-28T10:00:00Z'));
};

describe('ExamPage', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows every question with its saved answer restored', async () => {
    server.use(
      getGetExamSessionMockHandler(
        openExam([examItem(1, { savedAnswer: { optionId: 'b' }, answerSavedAt: '2026-09-28T10:01:00Z' }), examItem(2)]),
      ),
    );
    openExamPage();

    expect(await screen.findByRole('heading', { name: 'Question 1 of 2' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Question 2 of 2' })).toBeInTheDocument();
    expect(screen.getAllByRole('radio', { name: '4' })[0]).toBeChecked();
    expect(screen.getAllByRole('radio', { name: '4' })[1]).not.toBeChecked();
    expect(screen.getByText('Saved automatically')).toBeInTheDocument();
  });

  it('redirects a submitted exam to its result', async () => {
    server.use(getGetExamSessionMockHandler(submittedExam([examItem(1)])));
    openExamPage();

    expect(await screen.findByRole('heading', { name: 'Result: Mechanics' })).toBeInTheDocument();
  });

  it('shows retry when the exam fails to load', async () => {
    server.use(http.get('*/api/exams/:sessionId', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));
    openExamPage();

    expect(await screen.findByRole('button', { name: 'Retry' })).toBeInTheDocument();
    expect(screen.getByText('Could not load the exam.')).toBeInTheDocument();
  });

  it('shows the countdown from the server clock', async () => {
    freezeClock();
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)])));
    openExamPage();

    expect(await screen.findByRole('timer')).toHaveTextContent('Time left 29:59');
    expect(screen.getByRole('timer')).not.toHaveClass('text-danger');
  });

  it('announces the last two minutes', async () => {
    freezeClock();
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)], { deadline: '2026-09-28T10:01:31Z' })));
    openExamPage();

    expect(await screen.findByText('Less than two minutes left.')).toHaveAttribute('role', 'status');
    expect(screen.getByRole('timer')).toHaveTextContent('Time left 1:30');
    expect(screen.getByRole('timer')).toHaveClass('text-danger');
  });

  it('hides the countdown for an untimed exam', async () => {
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)], { deadline: null, timeLimitMinutes: null })));
    openExamPage();

    expect(await screen.findByRole('heading', { name: 'Question 1 of 1' })).toBeInTheDocument();
    expect(screen.queryByRole('timer')).toBeNull();
  });

  it('renders right to left in Arabic', async () => {
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)])));
    openExamPage('ar');

    expect(await screen.findByRole('heading', { name: 'امتحان: Mechanics' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('titles a multi-unit exam with every unit name', async () => {
    const units = [
      { unitId: examUnitId, name: 'Mechanics' },
      { unitId: examSecondUnitId, name: 'Waves' },
    ];
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)], { kind: 'MultiUnitExam', units })));
    openExamPage();

    expect(await screen.findByRole('heading', { name: 'Multi-unit exam: Mechanics + Waves' })).toBeInTheDocument();
  });

  it('shows the exam refusal when the assistant is opened during the exam', async () => {
    server.use(
      getGetExamSessionMockHandler(openExam([examItem(1)])),
      getGetAvatarStatusMockHandler(avatarStatus({ examInProgress: true })),
    );
    const user = userEvent.setup();
    openExamPage();

    await user.click(await screen.findByRole('button', { name: 'Ask the assistant' }));

    const panel = await screen.findByRole('dialog', { name: 'AI assistant' });
    expect(within(panel).getByText('Context: Exam in progress')).toBeInTheDocument();
    expect(await within(panel).findByText(/I cannot help while an exam is in progress/)).toBeInTheDocument();
    expect(within(panel).getByRole('textbox', { name: 'Your question' })).toBeDisabled();
  });
});
