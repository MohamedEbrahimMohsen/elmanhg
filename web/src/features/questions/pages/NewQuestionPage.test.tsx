import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

const openNew = () => renderApp(`/admin/question/new/${lessonId}`, { session: testSessions.admin });

const findPreview = async () => within(await screen.findByRole('region', { name: 'Student preview' }));

describe('NewQuestionPage', () => {
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
        type: 'TrueFalse',
        stem: '<p>Mass is a vector.</p>',
        body: {},
        gradingSpec: { correctAnswer: false },
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

  it('creates a true-or-false question and opens it', async () => {
    const user = userEvent.setup();
    openNew();

    await user.selectOptions(await screen.findByLabelText('Type'), 'True or false');
    await user.click(screen.getByRole('textbox', { name: 'Question text' }));
    await user.type(screen.getByRole('textbox', { name: 'Question text' }), 'Mass is a vector.');
    await user.selectOptions(screen.getByLabelText('Correct answer'), 'False');
    await user.click(screen.getByRole('button', { name: 'Create question' }));

    expect(await screen.findByText('Question created.')).toBeInTheDocument();
    expect(created).toEqual([
      expect.objectContaining({
        lessonId,
        type: 'TrueFalse',
        stem: '<p>Mass is a vector.</p>',
        body: {},
        gradingSpec: { correctAnswer: false },
      }),
    ]);
    expect(await screen.findByRole('heading', { name: 'Edit question' })).toBeInTheDocument();
  });

  it('shows the fields of each type and updates the preview', async () => {
    const user = userEvent.setup();
    openNew();

    const type = await screen.findByLabelText('Type');
    for (const number of [1, 2, 3, 4]) {
      expect(screen.getByRole('textbox', { name: `Option ${String(number)}` })).toBeInTheDocument();
    }
    await user.selectOptions(type, 'Fill in the blank');
    expect(screen.getByLabelText('Accepted answers for blank 1 (one per line)')).toBeInTheDocument();
    expect((await findPreview()).getByLabelText('Blank 1')).toBeInTheDocument();
    await user.selectOptions(type, 'Short answer');
    expect(screen.getByLabelText('Answer type')).toBeInTheDocument();
    expect(screen.getByLabelText('Correct value')).toBeInTheDocument();
    expect((await findPreview()).getByLabelText('Your answer')).toBeInTheDocument();
    await user.selectOptions(type, 'True or false');
    const preview = await findPreview();
    expect(preview.getByRole('radio', { name: 'True' })).toBeInTheDocument();
    expect(preview.getByRole('radio', { name: 'False' })).toBeInTheDocument();
  });

  it('shows required errors on submit', async () => {
    const user = userEvent.setup();
    openNew();

    await user.click(await screen.findByRole('button', { name: 'Create question' }));

    expect((await screen.findAllByText('This field is required.')).length).toBeGreaterThan(0);
    expect(screen.getByText('Mark exactly one correct answer.')).toBeInTheDocument();
    expect(created).toEqual([]);
  });

  it('adds and removes options within the limits', async () => {
    const user = userEvent.setup();
    openNew();

    const removeFirst = await screen.findByRole('button', { name: 'Remove option 1' });
    expect(removeFirst).toBeEnabled();
    await user.click(removeFirst);
    await user.click(screen.getByRole('button', { name: 'Remove option 1' }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Remove option 1' })).toBeDisabled();
    });
    expect(screen.queryByRole('textbox', { name: 'Option 3' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Add option' }));
    expect(screen.getByRole('textbox', { name: 'Option 3' })).toBeInTheDocument();
  });

  it('shows an error when the lesson cannot load', async () => {
    server.use(
      http.get('*/api/lessons/:lessonId', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    openNew();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the lesson');
    server.use(getGetLessonMockHandler(lesson));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'New question' })).toBeInTheDocument();
  });
});
