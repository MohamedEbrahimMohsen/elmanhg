import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MathStepGradeResult } from '@/shared/api/generated/model';
import { getGetMathStepGradeMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { axe } from '@/test/axe';
import {
  gradedMathStepGrade,
  inReviewMathStepGrade,
  mathQuestionId,
  mathSessionId,
  overriddenMathStepGrade,
  pendingMathStepGrade,
} from '@/test/mathStepGradeFixtures';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/renderWithProviders';
import { mathStepGradePollIntervalMs } from '../hooks/useMathStepGrade';
import { MathStepGradeStatus } from './MathStepGradeStatus';

const gradeUrl = '*/api/sessions/:sessionId/questions/:questionId/math-step-grade';

function renderStatus(grade: MathStepGradeResult, options: { lng?: 'en' | 'ar'; showOutcome?: boolean } = {}) {
  server.use(getGetMathStepGradeMockHandler(grade));
  return renderWithProviders(
    <MathStepGradeStatus
      sessionId={mathSessionId}
      questionId={mathQuestionId}
      showOutcome={options.showOutcome ?? true}
    />,
    { lng: options.lng ?? 'en' },
  );
}

describe('MathStepGradeStatus', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a loading state while the grade loads', () => {
    server.use(
      http.get(gradeUrl, async () => {
        await delay('infinite');
        return HttpResponse.json(pendingMathStepGrade);
      }),
    );
    renderWithProviders(<MathStepGradeStatus sessionId={mathSessionId} questionId={mathQuestionId} />);

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading the grade…');
    expect(status).toHaveAttribute('aria-busy', 'true');
  });

  it('shows grading in progress while pending', async () => {
    renderStatus(pendingMathStepGrade);

    expect(await screen.findByText('Grading your answer…')).toBeInTheDocument();
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Grading your answer…');
    expect(status).toHaveTextContent('We check your final answer and mark your steps against the model solution.');
  });

  it('shows under review in the warning style', async () => {
    renderStatus(inReviewMathStepGrade);

    expect(await screen.findByText('Under review')).toBeInTheDocument();
    expect(screen.getByText('A teacher will review your grade before it is final.')).toBeInTheDocument();
    expect(screen.queryByText(/Score/)).not.toBeInTheDocument();
  });

  it('shows the verdict, score, final answer and step marks when graded', async () => {
    renderStatus(gradedMathStepGrade);

    const group = within(await screen.findByRole('group', { name: 'Step grading' }));
    expect(group.getByRole('status')).toHaveTextContent('Partially correct');
    expect(group.getByRole('status')).toHaveTextContent('Score 1.5 / 2');
    expect(group.getByText('Final answer:')).toBeInTheDocument();
    expect(group.getByText('correct')).toBeInTheDocument();
    const marks = within(group.getByRole('region', { name: 'Marks per step' }));
    expect(marks.getAllByText('Step 1')[0]).toBeInTheDocument();
    expect(marks.getByText('2 / 2')).toBeInTheDocument();
    expect(marks.getByText('The division is incomplete.')).toBeInTheDocument();
    expect(group.getByText('Good working; finish the division.')).toBeInTheDocument();
  });

  it('shows the teacher score and note without a verdict line when an unchecked answer was overridden', async () => {
    renderStatus(overriddenMathStepGrade);

    const group = within(await screen.findByRole('group', { name: 'Step grading' }));
    expect(group.getByRole('status')).toHaveTextContent('Score 2 / 2');
    expect(group.queryByText('Final answer:')).not.toBeInTheDocument();
    expect(group.queryByRole('region', { name: 'Marks per step' })).not.toBeInTheDocument();
    expect(group.getByRole('note')).toHaveTextContent("Teacher's note: Correct method.");
  });

  it('hides the outcome header but keeps step marks when showOutcome is false', async () => {
    renderStatus(gradedMathStepGrade, { showOutcome: false });

    const group = within(await screen.findByRole('group', { name: 'Step grading' }));
    expect(group.getByRole('region', { name: 'Marks per step' })).toBeInTheDocument();
    expect(group.queryByText(/Score/)).not.toBeInTheDocument();
    expect(group.queryByText('Partially correct')).not.toBeInTheDocument();
  });

  it('renders nothing when the answer has no step grade', async () => {
    let requested = false;
    server.use(
      http.get(gradeUrl, () => {
        requested = true;
        return HttpResponse.json({ code: 'MATH_STEP_GRADE_NOT_FOUND' }, { status: 404 });
      }),
    );
    renderWithProviders(<MathStepGradeStatus sessionId={mathSessionId} questionId={mathQuestionId} />);

    await vi.waitFor(() => {
      expect(requested).toBe(true);
    });
    await vi.waitFor(() => {
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows an error with retry and recovers', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(gradeUrl, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), { once: true }),
      getGetMathStepGradeMockHandler(pendingMathStepGrade),
    );
    renderWithProviders(<MathStepGradeStatus sessionId={mathSessionId} questionId={mathQuestionId} />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the grade.');
    await user.click(within(alert).getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Grading your answer…')).toBeInTheDocument();
  });

  it('polls while pending and calls onGraded once when graded', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const onGraded = vi.fn();
    let polls = 0;
    server.use(
      getGetMathStepGradeMockHandler(() => {
        polls += 1;
        return polls === 1 ? pendingMathStepGrade : gradedMathStepGrade;
      }),
    );
    renderWithProviders(
      <MathStepGradeStatus sessionId={mathSessionId} questionId={mathQuestionId} onGraded={onGraded} />,
    );

    expect(await screen.findByText('Grading your answer…')).toBeInTheDocument();
    expect(onGraded).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(mathStepGradePollIntervalMs);
    expect(await screen.findByText('Partially correct')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(mathStepGradePollIntervalMs);
    expect(onGraded).toHaveBeenCalledTimes(1);
  });

  it('renders right-to-left in Arabic', async () => {
    renderStatus(pendingMathStepGrade, { lng: 'ar' });

    expect(await screen.findByText('جارٍ تصحيح إجابتك…')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations when graded', async () => {
    const { container } = renderStatus(gradedMathStepGrade);

    await screen.findByRole('group', { name: 'Step grading' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
