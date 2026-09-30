import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { EssayGradeResult } from '@/shared/api/generated/model';
import { getGetEssayGradeMockHandler } from '@/shared/api/generated/sessions/sessions.msw';
import { axe } from '@/test/axe';
import {
  essayQuestionId,
  essaySessionId,
  gradedEssayGrade,
  inReviewEssayGrade,
  pendingEssayGrade,
} from '@/test/essayGradeFixtures';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/renderWithProviders';
import { essayGradePollIntervalMs } from '../hooks/useEssayGrade';
import { EssayGradeStatus } from './EssayGradeStatus';

const essayGradeUrl = '*/api/sessions/:sessionId/questions/:questionId/essay-grade';

function renderStatus(grade: EssayGradeResult | (() => EssayGradeResult), lng: 'en' | 'ar' = 'en') {
  server.use(getGetEssayGradeMockHandler(typeof grade === 'function' ? () => grade() : grade));
  return renderWithProviders(<EssayGradeStatus sessionId={essaySessionId} questionId={essayQuestionId} />, {
    lng,
  });
}

describe('EssayGradeStatus', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a loading state while the grade loads', () => {
    server.use(
      http.get(essayGradeUrl, async () => {
        await delay('infinite');
        return HttpResponse.json(pendingEssayGrade);
      }),
    );
    renderWithProviders(<EssayGradeStatus sessionId={essaySessionId} questionId={essayQuestionId} />);

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading the grade…');
    expect(status).toHaveAttribute('aria-busy', 'true');
  });

  it('shows grading in progress while the essay is pending', async () => {
    renderStatus(pendingEssayGrade);

    expect(await screen.findByText('Grading your essay…')).toBeInTheDocument();
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Grading your essay…');
    expect(status).toHaveTextContent('The AI grader is marking your answer against the rubric.');
  });

  it('polls until the grade is final', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let polls = 0;
    renderStatus(() => {
      polls += 1;
      return polls === 1 ? pendingEssayGrade : gradedEssayGrade;
    });

    expect(await screen.findByText('Grading your essay…')).toBeInTheDocument();
    await vi.advanceTimersByTimeAsync(essayGradePollIntervalMs);
    expect(await screen.findByText('Partially correct')).toBeInTheDocument();
  });

  it('shows under review without a score', async () => {
    renderStatus(inReviewEssayGrade);

    expect(await screen.findByText('Under review')).toBeInTheDocument();
    expect(screen.getByText('A teacher will review your grade before it is final.')).toBeInTheDocument();
    expect(screen.queryByText(/Score/)).not.toBeInTheDocument();
  });

  it('shows the verdict, score, criterion marks and justification when graded', async () => {
    renderStatus(gradedEssayGrade);

    const group = await screen.findByRole('group', { name: 'Essay grade' });
    expect(within(group).getByRole('status')).toHaveTextContent('Partially correct');
    expect(within(group).getByRole('status')).toHaveTextContent('Score 2.5 / 5');
    const criteria = within(group).getByRole('list', { name: 'Marks per criterion' });
    expect(within(criteria).getByText('Definition')).toBeInTheDocument();
    expect(within(criteria).getByText('1 / 2')).toBeInTheDocument();
    expect(within(criteria).getByText('Partly correct.')).toBeInTheDocument();
    expect(within(group).getByRole('heading', { name: "Grader's comment" })).toBeInTheDocument();
    expect(within(group).getByText('Good definition; add an example.')).toBeInTheDocument();
  });

  it('shows an error with retry when the grade fails to load', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(essayGradeUrl, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
      getGetEssayGradeMockHandler(gradedEssayGrade),
    );
    renderWithProviders(<EssayGradeStatus sessionId={essaySessionId} questionId={essayQuestionId} />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the grade.');
    await user.click(within(alert).getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Partially correct')).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    renderStatus(inReviewEssayGrade, 'ar');

    expect(await screen.findByText('قيد المراجعة')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = renderStatus(gradedEssayGrade);

    await screen.findByRole('group', { name: 'Essay grade' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
