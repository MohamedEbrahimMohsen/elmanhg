import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { PageDataOfQuestionListItemResult } from '@/shared/api/generated/model';
import { getGetLessonsMockHandler } from '@/shared/api/generated/lessons/lessons.msw';
import { getGetQuestionsMockHandler } from '@/shared/api/generated/questions/questions.msw';
import { getGetSubjectMockHandler, getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { getGetTeachersMockHandler } from '@/shared/api/generated/teachers/teachers.msw';
import { axe } from '@/test/axe';
import { mintButtons } from '@/test/mintButtons';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const physicsId = '11111111-1111-4111-8111-111111111111';
const chemistryId = '22222222-2222-4222-8222-222222222222';
const mechanicsId = '33333333-3333-4333-8333-333333333333';
const lessonId = '44444444-4444-4444-8444-444444444444';

const emptyPage: PageDataOfQuestionListItemResult = {
  items: [],
  pageNumber: 1,
  pageSize: 20,
  totalItems: 0,
  totalPages: 0,
};

const subjects = [
  { id: physicsId, name: 'Physics', order: 1, unitCount: 1 },
  { id: chemistryId, name: 'Chemistry', order: 2, unitCount: 0 },
];

const openList = (path = '/admin/questions', lng: 'en' | 'ar' = 'en') =>
  renderApp(path, { session: testSessions.admin, lng });

const openDialog = async (name = 'Add question') => {
  const user = userEvent.setup();
  openList('/admin/questions', name === 'Add question' ? 'en' : 'ar');
  await screen.findByText(name === 'Add question' ? 'No questions yet.' : 'لا توجد أسئلة بعد.');
  await user.click(screen.getByRole('button', { name }));
  return { user, dialog: await screen.findByRole('dialog', { name }) };
};

describe('AddQuestionButton', () => {
  beforeEach(() => {
    server.use(
      getGetQuestionsMockHandler(emptyPage),
      getGetTeachersMockHandler([]),
      getGetSubjectsMockHandler(subjects),
      getGetSubjectMockHandler(({ params }) => ({
        id: String(params.subjectId),
        name: params.subjectId === physicsId ? 'Physics' : 'Chemistry',
        order: 1,
        units:
          params.subjectId === physicsId
            ? [{ id: mechanicsId, subjectId: physicsId, name: 'Mechanics', order: 1, lessonCount: 1 }]
            : [],
      })),
      getGetLessonsMockHandler([
        {
          id: lessonId,
          unitId: mechanicsId,
          name: "Newton's laws",
          order: 1,
          state: 'Published',
          questionCount: 0,
          servableQuestionCount: 0,
        },
      ]),
    );
  });

  it('is the only primary action on the questions page', async () => {
    openList();

    await screen.findByText('No questions yet.');
    const [primary, ...others] = mintButtons();
    expect(others).toHaveLength(0);
    expect(primary).toHaveAccessibleName('Add question');
  });

  it('links straight to a new question when the list is filtered by a lesson', async () => {
    openList(`/admin/questions?lessonId=${lessonId}`);

    expect(await screen.findByRole('link', { name: 'Add question' })).toHaveAttribute(
      'href',
      `/admin/question/new/${lessonId}`,
    );
    expect(mintButtons()).toHaveLength(1);
  });

  it('picks subject, unit and lesson then links to new question and import', async () => {
    const { user, dialog } = await openDialog();

    expect(within(dialog).getByRole('button', { name: 'New question' })).toBeDisabled();
    expect(within(dialog).getByLabelText('Unit')).toBeDisabled();
    await user.selectOptions(within(dialog).getByLabelText('Subject'), 'Physics');
    await user.selectOptions(await within(dialog).findByRole('combobox', { name: 'Unit' }), 'Mechanics');
    await within(dialog).findByRole('option', { name: "Newton's laws" });
    await user.selectOptions(within(dialog).getByLabelText('Lesson'), "Newton's laws");

    expect(within(dialog).getByRole('link', { name: 'New question' })).toHaveAttribute(
      'href',
      `/admin/question/new/${lessonId}`,
    );
    expect(within(dialog).getByRole('link', { name: 'Import from file' })).toHaveAttribute(
      'href',
      `/admin/question/import/${lessonId}`,
    );
  });

  it('clears the unit and lesson when the subject changes', async () => {
    const { user, dialog } = await openDialog();

    await user.selectOptions(within(dialog).getByLabelText('Subject'), 'Physics');
    await user.selectOptions(await within(dialog).findByRole('combobox', { name: 'Unit' }), 'Mechanics');
    await within(dialog).findByRole('option', { name: "Newton's laws" });
    await user.selectOptions(within(dialog).getByLabelText('Lesson'), "Newton's laws");
    await user.selectOptions(within(dialog).getByLabelText('Subject'), 'Chemistry');

    expect(within(dialog).getByLabelText('Unit')).toHaveValue('');
    expect(within(dialog).getByLabelText('Lesson')).toHaveValue('');
    expect(within(dialog).getByLabelText('Lesson')).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'New question' })).toBeDisabled();
  });

  it('shows an error and recovers on retry when the subjects fail', async () => {
    server.use(http.get('*/api/subjects', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const { user, dialog } = await openDialog();

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Could not load the lessons');
    server.use(getGetSubjectsMockHandler(subjects));
    await user.click(within(dialog).getByRole('button', { name: 'Retry' }));

    expect(await within(dialog).findByRole('option', { name: 'Physics' })).toBeInTheDocument();
  });

  it('renders the dialog in Arabic', async () => {
    const { dialog } = await openDialog('إضافة سؤال');

    expect(within(dialog).getByLabelText('المادة')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'سؤال جديد' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'استيراد من ملف' })).toBeDisabled();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations with the dialog open', async () => {
    const { dialog } = await openDialog();

    expect((await axe(dialog)).violations).toEqual([]);
  });
});
