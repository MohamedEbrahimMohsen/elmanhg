import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { getGetMathStepGradeMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { examItem, examSessionId, submittedExam } from '@/test/examFixtures';
import { pendingMathStepGrade } from '@/test/mathStepGradeFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const exam = () =>
  submittedExam(
    [
      examItem(1),
      examItem(2, {
        type: 'MathSteps',
        body: {},
        maxScore: 2,
        savedAnswer: { steps: ['2x = 4'], finalAnswer: 'x = 2' },
      }),
    ],
    { scorePercent: 20, isPassed: false },
  );

const openResult = () => {
  server.use(getGetExamSessionMockHandler(exam()), getGetMathStepGradeMockHandler(pendingMathStepGrade));
  return renderApp(`/student/exam-result/${examSessionId}`, { session: testSessions.student });
};

describe('ExamResultPage math step grading', () => {
  it('shows a pending math answer with its grading status instead of unanswered', async () => {
    openResult();

    const article = within(await screen.findByRole('article', { name: 'Question 2' }));
    expect(await article.findByText('Grading your answer…')).toBeInTheDocument();
    expect(article.queryByText('You did not answer this question.')).not.toBeInTheDocument();
  });

  it('shows the provisional badge instead of failed while a math answer is pending', async () => {
    openResult();

    expect(await screen.findByText('Under review')).toBeInTheDocument();
    expect(screen.queryByText(/Below the pass mark/)).not.toBeInTheDocument();
  });
});
