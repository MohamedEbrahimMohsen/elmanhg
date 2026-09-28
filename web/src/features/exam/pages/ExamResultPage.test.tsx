import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { ExamSessionResult } from '@/shared/api/generated/model';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { axe } from '@/test/axe';
import { examItem, examLessonId, examSessionId, examUnitId, openExam, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openResult = (session: ExamSessionResult, lng: 'en' | 'ar' = 'en') => {
  server.use(getGetExamSessionMockHandler(session));
  return renderApp(`/student/exam-result/${examSessionId}`, { session: testSessions.student, lng });
};

describe('ExamResultPage', () => {
  it('shows the score and a pass badge', async () => {
    openResult(submittedExam([examItem(1), examItem(2)]));

    expect(await screen.findByText('80 / 100')).toBeInTheDocument();
    expect(screen.getByText('Passed')).toBeInTheDocument();
    expect(screen.getByText('Time: 12 min 30 s')).toBeInTheDocument();
  });

  it('shows the fail badge with the pass mark', async () => {
    openResult(submittedExam([examItem(1)], { scorePercent: 30, isPassed: false }));

    expect(await screen.findByText('Below the pass mark (50)')).toBeInTheDocument();
    expect(screen.queryByText('Passed')).toBeNull();
  });

  it('lists the per-lesson breakdown with a practice link', async () => {
    openResult(submittedExam([examItem(1)]));

    const row = await screen.findByRole('row', { name: /Newton's laws/ });
    expect(within(row).getByText('50%')).toBeInTheDocument();
    expect(within(row).getByRole('link', { name: 'Train now' })).toHaveAttribute(
      'href',
      `/student/lesson/${examLessonId}/practice`,
    );
  });

  it('lists the weakest objectives', async () => {
    openResult(submittedExam([examItem(1)]));

    expect(await screen.findByRole('heading', { name: 'Weakest objectives' })).toBeInTheDocument();
    expect(screen.getByText('State the first law')).toBeInTheDocument();
  });

  it('says there are no weak objectives when none are left', async () => {
    openResult(submittedExam([examItem(1)], { weakestObjectives: [] }));

    expect(await screen.findByText('No weak objectives. Well done!')).toBeInTheDocument();
  });

  it('reviews an unanswered question with the correct answer', async () => {
    openResult(submittedExam([examItem(1)]));

    const review = await screen.findByRole('article', { name: 'Question 1' });
    expect(within(review).getByText('You did not answer this question.')).toBeInTheDocument();
    expect(within(review).getByText('The correct answer')).toBeInTheDocument();
    expect(within(review).getAllByText('4').length).toBeGreaterThan(1);
    expect(within(review).getByText('Four.')).toBeInTheDocument();
  });

  it("links Retake to the unit's exam start", async () => {
    openResult(submittedExam([examItem(1)]));

    expect(await screen.findByRole('link', { name: 'Retake exam' })).toHaveAttribute(
      'href',
      `/student/exam-start/${examUnitId}`,
    );
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'My progress' })).toHaveAttribute(
      'href',
      '/student/progress',
    );
  });

  it('redirects an open exam to the exam screen', async () => {
    openResult(openExam([examItem(1)]));

    expect(await screen.findByRole('heading', { name: 'Exam: Mechanics' })).toBeInTheDocument();
  });

  it('shows retry on a load error', async () => {
    server.use(http.get('*/api/exams/:sessionId', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));
    renderApp(`/student/exam-result/${examSessionId}`, { session: testSessions.student });

    expect(await screen.findByRole('button', { name: 'Retry' })).toBeInTheDocument();
    expect(screen.getByText('Could not load the result.')).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    openResult(submittedExam([examItem(1)]), 'ar');

    expect(await screen.findByText('ناجح')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openResult(submittedExam([examItem(1)]));

    await screen.findByText('80 / 100');

    expect((await axe(container)).violations).toEqual([]);
  });
});
