import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetLessonMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import type { LessonDetailResult, QuestionDetailResult, QuestionGradeResult } from '@/shared/api/generated/model';
import {
  getGetQuestionMockHandler,
  getGradeQuestionDraftMockHandler,
  getResubmitQuestionMockHandler,
  getUpdateQuestionMockHandler,
} from '@/shared/api/generated/questions/questions.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const questionId = '11111111-1111-4111-8111-111111111111';
const lessonId = '22222222-2222-4222-8222-222222222222';
const objectiveId = '33333333-3333-4333-8333-333333333333';

const lesson: LessonDetailResult = {
  id: lessonId,
  unitId: '44444444-4444-4444-8444-444444444444',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [{ id: objectiveId, text: 'State the first law', order: 1 }],
};

const question = (overrides: Partial<QuestionDetailResult> = {}): QuestionDetailResult => ({
  id: questionId,
  lessonId,
  subjectId: '55555555-5555-4555-8555-555555555555',
  type: 'Mcq',
  stem: '<p>2 + 2 = ?</p>',
  body: {
    options: [
      { id: 'a', text: '<p>3</p>' },
      { id: 'b', text: '<p>4</p>' },
    ],
  },
  gradingSpec: { correctOptionId: 'b' },
  explanation: '<p>Add the numbers.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  tags: ['arithmetic'],
  maxScore: 1,
  version: 3,
  validationStatus: 'Approved',
  rejectionReason: null,
  retiredAt: null,
  ...overrides,
});

const request = {
  type: 'Mcq',
  stem: '<p>2 + 2 = ?</p>',
  body: {
    options: [
      { id: 'a', text: '<p>3</p>' },
      { id: 'b', text: '<p>4</p>' },
    ],
  },
  gradingSpec: { correctOptionId: 'b' },
  explanation: '<p>Add the numbers.</p>',
  difficulty: 'Medium',
  objectiveId: null,
  tags: ['arithmetic'],
  maxScore: 1,
};

const allRulesOn = {
  stripTashkeel: true,
  stripTatweel: true,
  unifyAlef: true,
  unifyTaaMarbuta: true,
  unifyAlefMaqsura: true,
  convertDigits: true,
  collapseWhitespace: true,
  foldCase: true,
};

const fillQuestion = () =>
  question({
    type: 'Fill',
    stem: '<p>v = [[1]] m/s</p>',
    body: { blanks: [{ id: '1' }] },
    gradingSpec: { blanks: [{ id: '1', acceptedAnswers: ['20'] }], normalization: { ...allRulesOn, unifyAlef: false } },
  });

const openEditor = (lng: 'en' | 'ar' = 'en') =>
  renderApp(`/admin/question/${questionId}`, { session: testSessions.admin, lng });

const findPreview = async () => within(await screen.findByRole('region', { name: 'Student preview' }));

const gradeResult = (overrides: Partial<QuestionGradeResult> = {}): QuestionGradeResult => ({
  score: 1,
  normalisedScore: 1,
  outcome: 'Correct',
  maxScore: 1,
  feedback: null,
  ...overrides,
});

describe('QuestionEditorPage', () => {
  let bodies: unknown[];
  let resubmitted: unknown[];
  let graded: unknown[];

  beforeEach(() => {
    bodies = [];
    resubmitted = [];
    graded = [];
    server.use(
      getGetQuestionMockHandler(question()),
      getGetLessonMockHandler(lesson),
      getUpdateQuestionMockHandler(async ({ request: put }) => {
        bodies.push(await put.json());
      }),
      getResubmitQuestionMockHandler(async ({ request: put }) => {
        resubmitted.push(await put.json());
      }),
      getGradeQuestionDraftMockHandler(async ({ request: post }) => {
        graded.push(await post.json());
        return gradeResult();
      }),
    );
  });

  it('shows an approved question with its warning', async () => {
    openEditor();

    expect(await screen.findByText('Approved')).toBeInTheDocument();
    expect(screen.getByText('v3')).toBeInTheDocument();
    expect(screen.getByText(/returns this question to pending review/)).toBeInTheDocument();
    expect(screen.getByLabelText('Type')).toBeDisabled();
    expect((await findPreview()).getByRole('radio', { name: '4' })).toBeInTheDocument();
  });

  it('saves the question', async () => {
    const user = userEvent.setup();
    openEditor();

    const points = await screen.findByLabelText('Points');
    await user.clear(points);
    await user.type(points, '2');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Question saved.')).toBeInTheDocument();
    expect(bodies).toEqual([{ ...request, maxScore: 2 }]);
  });

  it('resubmits a rejected question', async () => {
    server.use(getGetQuestionMockHandler(question({ validationStatus: 'Rejected', rejectionReason: 'Wrong unit' })));
    const user = userEvent.setup();
    openEditor();

    expect(await screen.findByText('Rejection reason: Wrong unit')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Save and resubmit for review' }));

    expect(await screen.findByText('Question resubmitted for review.')).toBeInTheDocument();
    expect(resubmitted).toEqual([request]);
    expect(bodies).toEqual([]);
  });

  it('shows a server error on its field', async () => {
    server.use(
      http.put('*/api/questions/:questionId', () =>
        HttpResponse.json({ code: 'QUESTION_OBJECTIVE_NOT_IN_LESSON' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    await user.click(await screen.findByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Objective')).toHaveAccessibleDescription(
        "This objective does not belong to the question's lesson.",
      );
    });
  });

  it('test-grades the chosen answer with the real grader', async () => {
    const user = userEvent.setup();
    openEditor();

    const preview = await findPreview();
    await user.click(preview.getByRole('radio', { name: '4' }));
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    expect(await preview.findByText('Correct')).toBeInTheDocument();
    expect(preview.getByText('Score 1 / 1')).toBeInTheDocument();
    expect(graded).toEqual([expect.objectContaining({ type: 'Mcq', answer: { optionId: 'b' } })]);
  });

  it('shows partial credit', async () => {
    server.use(getGradeQuestionDraftMockHandler(gradeResult({ outcome: 'Partial', score: 0.5, normalisedScore: 0.5 })));
    const user = userEvent.setup();
    openEditor();

    const preview = await findPreview();
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    expect(await preview.findByText('Partially correct')).toBeInTheDocument();
    expect(preview.getByText('Score 0.5 / 1')).toBeInTheDocument();
  });

  it('shows the grader feedback', async () => {
    server.use(
      getGradeQuestionDraftMockHandler(
        gradeResult({
          outcome: 'Partial',
          score: 0.5,
          normalisedScore: 0.5,
          feedback: 'Correct choices: 1 of 2; wrong choices: 0.',
        }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    const preview = await findPreview();
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    expect(await preview.findByText('Correct choices: 1 of 2; wrong choices: 0.')).toBeInTheDocument();
  });

  it('explains why the draft cannot be graded', async () => {
    server.use(
      http.post('*/api/questions/grade-draft', () =>
        HttpResponse.json({ code: 'QUESTION_CORRECT_OPTION_INVALID' }, { status: 422 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    const preview = await findPreview();
    await user.click(preview.getByRole('button', { name: 'Try the answer' }));

    const alert = await preview.findByRole('alert');
    expect(alert).toHaveTextContent('This draft cannot be graded yet.');
    expect(alert).toHaveTextContent('Mark the correct answer among the options.');
  });

  it('shows an error and recovers on retry', async () => {
    server.use(
      http.get('*/api/questions/:questionId', () =>
        HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the question');
    server.use(getGetQuestionMockHandler(question()));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByLabelText('Points')).toBeInTheDocument();
  });

  it('renders in Arabic', async () => {
    openEditor('ar');

    expect(await screen.findByRole('heading', { name: 'تحرير سؤال' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('shows the stored normalisation rules and saves a changed rule', async () => {
    server.use(getGetQuestionMockHandler(fillQuestion()));
    const user = userEvent.setup();
    openEditor();

    expect(await screen.findByRole('checkbox', { name: 'Treat أ إ آ ٱ as ا' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Ignore diacritics (tashkeel)' })).toBeChecked();
    await user.click(screen.getByRole('checkbox', { name: 'Ignore Latin letter case' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect((await screen.findAllByText('Question saved.')).length).toBeGreaterThan(0);
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect((bodies[0] as { gradingSpec: unknown }).gradingSpec).toEqual({
      blanks: [{ id: '1', acceptedAnswers: ['20'] }],
      normalization: { ...allRulesOn, unifyAlef: false, foldCase: false },
    });
  });

  it('shows the normalisation rules only for a text short answer', async () => {
    server.use(
      getGetQuestionMockHandler(
        question({
          type: 'Short',
          stem: '<p>g = ?</p>',
          body: { answerKind: 'numeric' },
          gradingSpec: { value: 9.8, tolerance: 0.1, toleranceMode: 'absolute' },
        }),
      ),
    );
    const user = userEvent.setup();
    openEditor();

    const answerType = await screen.findByLabelText('Answer type');
    expect(screen.queryByRole('group', { name: 'Answer normalisation' })).not.toBeInTheDocument();
    await user.selectOptions(answerType, 'Text');

    const group = await screen.findByRole('group', { name: 'Answer normalisation' });
    expect(within(group).getAllByRole('checkbox')).toHaveLength(8);
  });

  it('labels the normalisation rules in Arabic', async () => {
    server.use(getGetQuestionMockHandler(fillQuestion()));
    openEditor('ar');

    expect(await screen.findByRole('group', { name: 'تطبيع الإجابة' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'تجاهل التشكيل' })).toBeInTheDocument();
  });
});
