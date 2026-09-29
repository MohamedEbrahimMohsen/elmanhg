import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetLessonMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import {
  getCreateQuestionMockHandler,
  getGetQuestionMockHandler,
} from '@/shared/api/generated/questions/questions.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = '11111111-1111-4111-8111-111111111111';
const questionId = '22222222-2222-4222-8222-222222222222';

const lesson: LessonDetailResult = {
  id: lessonId,
  unitId: '33333333-3333-4333-8333-333333333333',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [],
};

async function openEssayEditor(user: UserEvent) {
  const rendered = renderApp(`/admin/question/new/${lessonId}`, { session: testSessions.admin });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/question/new/$lessonId']);
  await user.selectOptions(await screen.findByLabelText('Type'), 'Essay (v2)');
}

async function replace(user: UserEvent, label: string, text: string) {
  const input = screen.getByLabelText(label);
  await user.clear(input);
  await user.type(input, text);
}

async function fillEssay(user: UserEvent) {
  await user.click(screen.getByRole('textbox', { name: 'Question text' }));
  await user.type(screen.getByRole('textbox', { name: 'Question text' }), 'Explain inertia.');
  await replace(user, 'Criterion 1 title', 'Definition');
  await replace(user, 'Criterion 1 points', '2');
  await replace(user, 'Criterion 1, level 2 points', '2');
  await replace(user, 'Criterion 1, level 1 description', 'Missing');
  await replace(user, 'Criterion 1, level 2 description', 'Complete');
  await user.click(screen.getByRole('textbox', { name: 'Model answer 1' }));
  await user.type(screen.getByRole('textbox', { name: 'Model answer 1' }), 'Inertia resists change.');
}

describe('NewQuestionPage essay', () => {
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
        type: 'Essay',
        stem: '<p>Explain inertia.</p>',
        body: {},
        gradingSpec: { criteria: [], modelAnswers: [] },
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

  it('creates an essay question with its rubric and model answer', async () => {
    const user = userEvent.setup();
    await openEssayEditor(user);

    await fillEssay(user);
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('Question created.')).toBeInTheDocument();
    expect(created).toEqual([
      expect.objectContaining({
        type: 'Essay',
        body: {},
        gradingSpec: {
          criteria: [
            expect.objectContaining({
              id: 'c1',
              title: 'Definition',
              points: 2,
              levels: [
                { points: 0, description: 'Missing' },
                { points: 2, description: 'Complete' },
              ],
            }),
          ],
          modelAnswers: [expect.stringContaining('Inertia resists change.')],
        },
      }),
    ]);
  });

  it('adds and removes criteria and levels within the limits', async () => {
    const user = userEvent.setup();
    await openEssayEditor(user);

    expect(screen.getByRole('button', { name: 'Remove criterion 1' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remove level 1 of criterion 1' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remove level 2 of criterion 1' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Add criterion' }));
    expect(screen.getByText('Criterion 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove criterion 1' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Remove criterion 2' }));
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Remove criterion 1' })).toBeDisabled();
    });
    await user.click(screen.getByRole('button', { name: 'Add level to criterion 1' }));
    expect(screen.getByLabelText('Criterion 1, level 3 points')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove level 1 of criterion 1' })).toBeEnabled();
  });

  it('shows rubric errors on submit', async () => {
    const user = userEvent.setup();
    await openEssayEditor(user);

    await user.click(screen.getByRole('button', { name: 'Create question' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Criterion 1 title')).toHaveAttribute('aria-invalid', 'true');
    });
    expect(screen.getByLabelText('Criterion 1, level 1 description')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Criterion 1, level 2 description')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Criterion 1 title')).toHaveAccessibleDescription('This field is required.');
    expect(created).toEqual([]);
  });

  it('shows a server rubric error on the rubric', async () => {
    server.use(
      http.post('*/api/questions', () =>
        HttpResponse.json({ code: 'QUESTION_RUBRIC_LEVEL_POINTS_INVALID' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    await openEssayEditor(user);

    await fillEssay(user);
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    const rubric = within(screen.getByRole('group', { name: 'Grading rubric' }));
    expect(
      await rubric.findByText(
        "Level points must be different whole numbers from 0 to the criterion's points, including 0 and the full points.",
      ),
    ).toBeInTheDocument();
  });

  it('explains that essays are not test-graded in the preview', async () => {
    const user = userEvent.setup();
    await openEssayEditor(user);

    const preview = within(await screen.findByRole('region', { name: 'Student preview' }));
    expect(preview.queryByRole('button', { name: 'Try the answer' })).not.toBeInTheDocument();
    expect(
      preview.getByText('Essays are graded by the AI grader; the test grader does not cover them.'),
    ).toBeInTheDocument();
    expect(preview.getByRole('textbox', { name: 'Your essay' })).toBeInTheDocument();
  });
});
