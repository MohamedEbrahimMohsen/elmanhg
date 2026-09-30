import { screen, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetLessonMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import {
  getCreateQuestionMockHandler,
  getGetQuestionMockHandler,
  getGradeQuestionDraftMockHandler,
} from '@/shared/api/generated/questions/questions.msw';
import { mathStepsDraftGradeResult } from '@/test/mathStepGradeFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = '11111111-1111-4111-8111-111111111111';
const questionId = '22222222-2222-4222-8222-222222222222';

const lesson: LessonDetailResult = {
  id: lessonId,
  unitId: '33333333-3333-4333-8333-333333333333',
  name: 'Linear equations',
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [],
};

async function openMathEditor(user: UserEvent) {
  const rendered = renderApp(`/admin/question/new/${lessonId}`, { session: testSessions.admin });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/question/new/$lessonId']);
  await user.selectOptions(await screen.findByLabelText('Type'), 'Math with steps (v2)');
}

async function fillMath(user: UserEvent) {
  await user.click(screen.getByRole('textbox', { name: 'Question text' }));
  await user.type(screen.getByRole('textbox', { name: 'Question text' }), 'Solve 2x + 3 = 7.');
  await user.type(screen.getByLabelText('Accepted answer 1'), 'x = 2');
}

describe('NewQuestionPage math with steps', () => {
  let created: unknown[];

  beforeEach(() => {
    created = [];
    server.use(
      getGetLessonMockHandler(lesson),
      getCreateQuestionMockHandler(async ({ request }) => {
        created.push(await request.json());
        return { id: questionId };
      }),
      getGetQuestionMockHandler({
        id: questionId,
        lessonId,
        subjectId: '44444444-4444-4444-8444-444444444444',
        type: 'MathSteps',
        stem: '<p>Solve 2x + 3 = 7.</p>',
        body: {},
        gradingSpec: { acceptedAnswers: ['x = 2'], form: 'equivalent' },
        explanation: '',
        difficulty: 'Medium',
        objectiveId: null,
        tags: [],
        maxScore: 1,
        version: 1,
        validationStatus: 'Pending',
        rejectionReason: null,
        retiredAt: null,
      }),
    );
  });

  it('creates a math question with answers, form and tolerance', async () => {
    const user = userEvent.setup();
    await openMathEditor(user);

    await fillMath(user);
    await user.selectOptions(screen.getByLabelText('Required form'), 'Any equivalent form');
    await user.type(screen.getByLabelText('Numeric tolerance (optional)'), '0.01');
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('Question created.')).toBeInTheDocument();
    expect(created).toEqual([
      expect.objectContaining({
        type: 'MathSteps',
        body: {},
        gradingSpec: { acceptedAnswers: ['x = 2'], form: 'equivalent', tolerance: 0.01, toleranceMode: 'absolute' },
      }),
    ]);
  });

  it('grades the preview answer through grade-draft', async () => {
    let body: unknown = null;
    server.use(
      getGradeQuestionDraftMockHandler(async ({ request }) => {
        body = await request.json();
        return { score: 1, normalisedScore: 1, outcome: 'Correct', maxScore: 1, feedback: 'Final answer checked.' };
      }),
    );
    const user = userEvent.setup();
    await openMathEditor(user);
    await fillMath(user);
    const preview = within(screen.getByRole('region', { name: 'Student preview' }));

    await user.type(preview.getByRole('textbox', { name: 'Final answer' }), 'x=2');
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    expect(await preview.findByText('Final answer checked.')).toBeInTheDocument();
    expect(body).toEqual(expect.objectContaining({ answer: { steps: [], finalAnswer: 'x=2' } }));
  });

  it('creates a math question with a model solution and steps weight', async () => {
    const user = userEvent.setup();
    await openMathEditor(user);

    await fillMath(user);
    const weight = screen.getByLabelText('Steps weight (%)');
    await user.clear(weight);
    await user.type(weight, '50');
    const solution = within(screen.getByRole('group', { name: 'Model solution' }));
    await user.click(solution.getByRole('button', { name: 'Add step' }));
    await user.type(solution.getByRole('textbox', { name: 'Step 1' }), '2x = 4');
    await user.click(solution.getByRole('button', { name: 'Add step' }));
    await user.type(solution.getByRole('textbox', { name: 'Step 2' }), 'x = 2');
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('Question created.')).toBeInTheDocument();
    expect(created).toEqual([
      expect.objectContaining({
        gradingSpec: {
          acceptedAnswers: ['x = 2'],
          form: 'equivalent',
          modelSolution: ['2x = 4', 'x = 2'],
          stepsWeight: 50,
        },
      }),
    ]);
  });

  it('shows step marks, justification and confidence from the draft grader', async () => {
    server.use(getGradeQuestionDraftMockHandler(mathStepsDraftGradeResult));
    const user = userEvent.setup();
    await openMathEditor(user);
    await fillMath(user);
    const preview = within(screen.getByRole('region', { name: 'Student preview' }));

    await user.type(preview.getByRole('textbox', { name: 'Final answer' }), 'x=2');
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    const marks = within(await preview.findByRole('region', { name: 'Marks per step' }));
    expect(marks.getAllByText('Step 1')[0]).toBeInTheDocument();
    expect(marks.getByText('2 / 2')).toBeInTheDocument();
    expect(preview.getByText('Final answer: correct')).toBeInTheDocument();
    expect(preview.getByText('Good working; finish the division.')).toBeInTheDocument();
    expect(preview.getByText('Confidence: 90%')).toBeInTheDocument();
  });

  it('shows the server model-solution error under the solution field', async () => {
    server.use(
      http.post('*/api/questions', () =>
        HttpResponse.json({ code: 'QUESTION_MATH_MODEL_SOLUTION_REQUIRED' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    await openMathEditor(user);

    await fillMath(user);
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(
      await screen.findByText('Add at least one model solution step when the steps weight is above 0.'),
    ).toBeInTheDocument();
  });

  it('shows the server tolerance-form conflict under the tolerance field', async () => {
    server.use(
      http.post('*/api/questions', () =>
        HttpResponse.json({ code: 'QUESTION_MATH_TOLERANCE_FORM_CONFLICT' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    await openMathEditor(user);

    await fillMath(user);
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('A tolerance works only with the any-equivalent-form rule.')).toBeInTheDocument();
    expect(screen.getByLabelText('Numeric tolerance (optional)')).toHaveAttribute('aria-invalid', 'true');
  });
});
