import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { ExamSessionResult } from '@/shared/api/generated/model';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { getGetEssayGradeMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { inReviewEssayGrade, pendingEssayGrade } from '@/test/essayGradeFixtures';
import { essayExamItem, examItem, examSessionId, submittedExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const text = 'القصور الذاتي ممانعة';
const exam = () =>
  submittedExam([examItem(1), essayExamItem(2, { savedAnswer: { text }, explanation: '<p>Newton 1.</p>' })]);

const openResult = (session: ExamSessionResult, lng: 'en' | 'ar' = 'en') => {
  server.use(getGetExamSessionMockHandler(session));
  return renderApp(`/student/exam-result/${examSessionId}`, { session: testSessions.student, lng });
};

describe('ExamResultPage essay', () => {
  it('shows a written essay with its grading status instead of unanswered', async () => {
    server.use(getGetEssayGradeMockHandler(pendingEssayGrade));
    openResult(exam());

    const article = await screen.findByRole('article', { name: 'Question 2' });
    expect(within(article).getByText(text)).toBeInTheDocument();
    expect(await within(article).findByText('Grading your essay…')).toBeInTheDocument();
    expect(within(article).queryByText('You did not answer this question.')).not.toBeInTheDocument();
  });

  it('notes the provisional score while an essay is pending', async () => {
    server.use(getGetEssayGradeMockHandler(pendingEssayGrade));
    openResult(exam());

    expect(
      await screen.findByText('Some essays are still being graded. The score will update when they are done.'),
    ).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    server.use(getGetEssayGradeMockHandler(inReviewEssayGrade));
    openResult(exam(), 'ar');

    expect(await screen.findByText('قيد المراجعة')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
