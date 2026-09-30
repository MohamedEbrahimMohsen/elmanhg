import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetEssayGradeReviewMockHandler,
  getGetGradeReviewQueueMockHandler,
  getGetGradeReviewSubjectsMockHandler,
  getGetMathStepGradeReviewMockHandler,
  getReviewEssayGradeMockHandler,
} from '@/shared/api/generated/grade-reviews/grade-reviews.msw';
import type { GradeReviewDetailResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import {
  essayGradeId,
  essayReviewDetail,
  failedEssayReviewDetail,
  gradeReviewSubjects,
  mathGradeId,
  mathReviewDetail,
  queuePage,
  reviewedEssayDetail,
  reviewSubjectId,
} from '@/test/gradeReviewFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const essayRoute = `*/api/subjects/:subjectId/grade-reviews/essays/:essayGradeId`;

async function openDetail(
  detail: GradeReviewDetailResult = essayReviewDetail,
  segment = 'essay',
  gradeId = essayGradeId,
) {
  server.use(getGetEssayGradeReviewMockHandler(detail), getGetMathStepGradeReviewMockHandler(detail));
  const path = `/teacher/grade/${reviewSubjectId}/${segment}/${gradeId}`;
  const rendered = renderApp(path, { session: testSessions.teacher });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/grade/$subjectId/$kind/$gradeId']);
  return rendered;
}

const form = () => screen.findByRole('form', { name: 'Your decision' });

describe('GradeReviewDetailPage', () => {
  beforeEach(() => {
    server.use(
      getGetGradeReviewSubjectsMockHandler(gradeReviewSubjects),
      getGetGradeReviewQueueMockHandler(queuePage([])),
    );
  });

  it("shows the question, the student's essay, the suggested score and the criteria", async () => {
    await openDetail();

    expect(await screen.findByRole('heading', { level: 1, name: 'Review answer' })).toBeInTheDocument();
    expect(screen.getByText('Explain inertia.')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Grading rubric' })).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: "Student's answer" })).getByText('Inertia keeps a body at rest.'),
    ).toBeInTheDocument();
    const ai = within(screen.getByRole('region', { name: 'AI grading' }));
    expect(ai.getByText('Suggested score: 2.5 of 5')).toBeInTheDocument();
    expect(ai.getByText('Confidence: 55%')).toBeInTheDocument();
    expect(ai.getByText('Partly correct.')).toBeInTheDocument();
  });

  it('accepts the suggested score, then toasts and returns to the queue', async () => {
    let body: unknown;
    server.use(
      getReviewEssayGradeMockHandler(async ({ request }) => {
        body = await request.json();
        return reviewedEssayDetail;
      }),
    );
    const user = userEvent.setup();
    const { router } = await openDetail();

    await user.click(within(await form()).getByRole('button', { name: 'Save decision' }));

    expect(await screen.findByText('Decision saved. The score is now final.')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 1, name: 'AI grade review' })).toBeInTheDocument();
    expect(body).toEqual({ decision: 'Accepted', score: null, comment: null });
    expect(router.state.location.search).toMatchObject({ subjectId: reviewSubjectId, kind: 'Essay' });
  });

  it('shows inline errors when overriding without score or note', async () => {
    const user = userEvent.setup();
    await openDetail();

    await user.click(within(await form()).getByRole('radio', { name: 'Set the score myself' }));
    await user.click(screen.getByRole('button', { name: 'Save decision' }));

    expect(await screen.findByText('Enter the score.')).toBeInTheDocument();
    expect(screen.getByText('Write a note explaining the score.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Score (out of 5)' })).toHaveAttribute('aria-invalid', 'true');
  });

  it('sends the teacher score and note on override', async () => {
    let body: unknown;
    server.use(
      getReviewEssayGradeMockHandler(async ({ request }) => {
        body = await request.json();
        return reviewedEssayDetail;
      }),
    );
    const user = userEvent.setup();
    await openDetail();

    await user.click(within(await form()).getByRole('radio', { name: 'Set the score myself' }));
    await user.type(screen.getByRole('textbox', { name: 'Score (out of 5)' }), '4');
    await user.type(screen.getByRole('textbox', { name: 'Note to the student' }), 'Full marks for the definition.');
    await user.click(screen.getByRole('button', { name: 'Save decision' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'AI grade review' })).toBeInTheDocument();
    expect(body).toEqual({ decision: 'Overridden', score: 4, comment: 'Full marks for the definition.' });
  });

  it('disables accept when there is no AI score', async () => {
    await openDetail(failedEssayReviewDetail);

    const accept = within(await form()).getByRole('radio', { name: /Accept the suggested score/ });
    expect(accept).toBeDisabled();
    expect(screen.getByText('There is no suggested score to accept.')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Set the score myself' })).toBeChecked();
    expect(screen.getByText('The AI could not give this answer a score.')).toBeInTheDocument();
  });

  it('shows the math steps and final answer for an unchecked math answer', async () => {
    await openDetail(mathReviewDetail, 'math-steps', mathGradeId);

    const answer = within(await screen.findByRole('region', { name: "Student's answer" }));
    expect(answer.getByRole('group', { name: 'Your solution' })).toBeInTheDocument();
    expect(answer.getByText('Final answer')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Final answer check' })).toBeInTheDocument();
    expect(screen.getByText('Final answer could not be checked', { selector: 'p' })).toBeInTheDocument();
  });

  it('shows the reviewed card and no form for a reviewed answer', async () => {
    await openDetail(reviewedEssayDetail);

    const status = await screen.findByRole('status', { name: 'Reviewed' });
    expect(within(status).getByText('Score changed')).toBeInTheDocument();
    expect(within(status).getByText('Final score: 4 of 5')).toBeInTheDocument();
    expect(within(status).getByText('Full marks for the definition.')).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Your decision' })).toBeNull();
  });

  it('shows a conflict toast on 409', async () => {
    server.use(http.post(essayRoute, () => HttpResponse.json({ code: 'GRADE_NOT_IN_REVIEW' }, { status: 409 })));
    const user = userEvent.setup();
    await openDetail();

    await user.click(within(await form()).getByRole('button', { name: 'Save decision' }));

    expect(await screen.findByText('Another teacher just reviewed this answer.')).toBeInTheDocument();
  });

  it('shows a server field error under the score', async () => {
    server.use(http.post(essayRoute, () => HttpResponse.json({ code: 'GRADE_REVIEW_SCORE_INVALID' }, { status: 422 })));
    const user = userEvent.setup();
    await openDetail();

    await user.click(within(await form()).getByRole('radio', { name: 'Set the score myself' }));
    await user.type(screen.getByRole('textbox', { name: 'Score (out of 5)' }), '4');
    await user.type(screen.getByRole('textbox', { name: 'Note to the student' }), 'Good.');
    await user.click(screen.getByRole('button', { name: 'Save decision' }));

    expect(
      await screen.findByText('The score must be a non-negative number with at most two decimals.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Score (out of 5)' })).toHaveFocus();
  });

  it('shows not found for an unknown answer', async () => {
    server.use(http.get(essayRoute, () => HttpResponse.json({ code: 'GRADE_REVIEW_NOT_FOUND' }, { status: 404 })));
    renderApp(`/teacher/grade/${reviewSubjectId}/essay/${essayGradeId}`, { session: testSessions.teacher });

    expect(await screen.findByText('This answer was not found.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to AI grade review' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = await openDetail();
    await form();

    expect((await axe(container)).violations).toEqual([]);
  });
});
