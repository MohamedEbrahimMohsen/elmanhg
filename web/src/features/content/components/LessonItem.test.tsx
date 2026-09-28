import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { LessonResult, SubjectDetailResult } from '@/shared/api/generated/model';
import {
  getArchiveLessonMockHandler,
  getDeleteLessonMockHandler,
  getGetLessonsMockHandler,
  getPublishLessonMockHandler,
  getReorderLessonMockHandler,
  getUnpublishLessonMockHandler,
} from '@/shared/api/generated/lessons/lessons.msw';
import { getGetSubjectMockHandler, getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const physics: SubjectDetailResult = {
  id: 's1',
  name: 'Physics',
  order: 1,
  units: [{ id: 'u1', subjectId: 's1', name: 'Mechanics', order: 1, lessonCount: 3 }],
};

const lessons: LessonResult[] = [
  { id: 'l1', unitId: 'u1', name: "Newton's laws", order: 1, state: 'Draft', questionCount: 2 },
  { id: 'l2', unitId: 'u1', name: 'Momentum', order: 2, state: 'Published', questionCount: 0 },
  { id: 'l3', unitId: 'u1', name: 'Energy', order: 3, state: 'Archived', questionCount: 1 },
];

const openTree = async (user: ReturnType<typeof userEvent.setup>, lng: 'en' | 'ar' = 'en') => {
  const rendered = renderApp('/admin/content', { session: testSessions.admin, lng });
  const [showUnits, showLessons, listName] =
    lng === 'ar'
      ? ['عرض الوحدات', 'عرض الدروس', 'دروس Mechanics']
      : ['Show units', 'Show lessons', 'Lessons of Mechanics'];
  await user.click(await screen.findByRole('button', { name: showUnits }));
  await user.click(await screen.findByRole('button', { name: showLessons }));
  const list = await screen.findByRole('list', { name: listName });
  return { ...rendered, rows: within(list).getAllByRole('listitem') };
};

const actionsOf = (name: string) => within(screen.getByRole('group', { name: `Actions for ${name}` }));

describe('LessonItem', () => {
  let calls: { publish: string[]; unpublish: string[]; archive: string[]; reorder: unknown[]; remove: string[] };

  beforeEach(() => {
    calls = { publish: [], unpublish: [], archive: [], reorder: [], remove: [] };
    server.use(
      getGetSubjectsMockHandler([{ id: 's1', name: 'Physics', order: 1, unitCount: 1 }]),
      getGetSubjectMockHandler(physics),
      getGetLessonsMockHandler(lessons),
      getPublishLessonMockHandler(({ params }) => {
        calls.publish.push(String(params.lessonId));
      }),
      getUnpublishLessonMockHandler(({ params }) => {
        calls.unpublish.push(String(params.lessonId));
      }),
      getArchiveLessonMockHandler(({ params }) => {
        calls.archive.push(String(params.lessonId));
      }),
      getReorderLessonMockHandler(async ({ request }) => {
        calls.reorder.push(await request.json());
      }),
      getDeleteLessonMockHandler(({ params }) => {
        calls.remove.push(String(params.lessonId));
      }),
    );
  });

  it('publishes a draft lesson after confirmation', async () => {
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Publish' }));
    expect(screen.getByText("Publish Newton's laws? Students will see it.")).toBeInTheDocument();
    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Yes, publish' }));

    expect(await screen.findByText('Lesson published.')).toBeInTheDocument();
    expect(calls.publish).toEqual(['l1']);
  });

  it('does not call the API when the confirmation is cancelled', async () => {
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf('Momentum').getByRole('button', { name: 'Archive' }));
    await user.click(actionsOf('Momentum').getByRole('button', { name: 'Cancel' }));

    expect(actionsOf('Momentum').getByRole('button', { name: 'Archive' })).toBeInTheDocument();
    expect(calls.archive).toEqual([]);
  });

  it('offers only the actions each state allows', async () => {
    const user = userEvent.setup();
    await openTree(user);

    const draft = actionsOf("Newton's laws");
    expect(draft.getByRole('button', { name: 'Publish' })).toBeInTheDocument();
    expect(draft.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(draft.queryByRole('button', { name: 'Archive' })).not.toBeInTheDocument();
    const published = actionsOf('Momentum');
    expect(published.getByRole('button', { name: 'Move to draft' })).toBeInTheDocument();
    expect(published.getByRole('button', { name: 'Archive' })).toBeInTheDocument();
    expect(published.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    expect(published.queryByRole('button', { name: 'Publish' })).not.toBeInTheDocument();
    const archived = actionsOf('Energy');
    expect(archived.getByRole('button', { name: 'Publish' })).toBeInTheDocument();
    expect(archived.getByRole('button', { name: 'Move to draft' })).toBeInTheDocument();
    expect(archived.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
  });

  it('archives a published lesson after confirmation', async () => {
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf('Momentum').getByRole('button', { name: 'Archive' }));
    await user.click(actionsOf('Momentum').getByRole('button', { name: 'Yes, archive' }));

    expect(await screen.findByText('Lesson archived.')).toBeInTheDocument();
    expect(calls.archive).toEqual(['l2']);
  });

  it('moves an archived lesson to draft after confirmation', async () => {
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf('Energy').getByRole('button', { name: 'Move to draft' }));
    await user.click(actionsOf('Energy').getByRole('button', { name: 'Yes, move to draft' }));

    expect(await screen.findByText('Lesson moved to draft.')).toBeInTheDocument();
    expect(calls.unpublish).toEqual(['l3']);
  });

  it('moves a lesson down', async () => {
    const user = userEvent.setup();
    const { rows } = await openTree(user);
    const [first] = rows;
    if (!first) {
      throw new Error('The lesson list is empty.');
    }

    expect(within(first).getByRole('button', { name: "Move Newton's laws up" })).toBeDisabled();
    await user.click(within(first).getByRole('button', { name: "Move Newton's laws down" }));

    expect(await screen.findByText('Lesson moved.')).toBeInTheDocument();
    expect(calls.reorder).toEqual([{ position: 2 }]);
  });

  it('deletes a draft lesson after confirmation', async () => {
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Delete' }));
    expect(screen.getByText("Delete Newton's laws?")).toBeInTheDocument();
    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('Lesson deleted.')).toBeInTheDocument();
    expect(calls.remove).toEqual(['l1']);
  });

  it('shows the server error when a transition is rejected', async () => {
    server.use(
      http.post('*/api/lessons/:lessonId/publish', () =>
        HttpResponse.json({ code: 'LESSON_ALREADY_PUBLISHED' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Publish' }));
    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Yes, publish' }));

    expect(await screen.findByText('This lesson is already published.')).toBeInTheDocument();
  });

  it('shows the confirmation in Arabic', async () => {
    const user = userEvent.setup();
    await openTree(user, 'ar');

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    const group = within(screen.getByRole('group', { name: "إجراءات Newton's laws" }));
    await user.click(group.getByRole('button', { name: 'نشر' }));

    expect(screen.getByText("نشر Newton's laws؟ سيظهر للطلاب.")).toBeInTheDocument();
  });

  it('shows the question count of each lesson', async () => {
    const user = userEvent.setup();
    const { rows } = await openTree(user);

    const [first, second, third] = rows;
    if (!first || !second || !third) {
      throw new Error('The lesson list is incomplete.');
    }
    expect(within(first).getByText('2 questions')).toBeInTheDocument();
    expect(within(second).getByText('0 questions')).toBeInTheDocument();
    expect(within(third).getByText('1 question')).toBeInTheDocument();
  });

  it('shows the question count in Arabic', async () => {
    const user = userEvent.setup();
    await openTree(user, 'ar');

    expect(screen.getByText('سؤالان')).toBeVisible();
  });

  it('explains why a lesson with questions cannot be deleted', async () => {
    server.use(
      http.delete('*/api/lessons/:lessonId', () =>
        HttpResponse.json({ code: 'LESSON_HAS_QUESTIONS' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    await openTree(user);

    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Delete' }));
    await user.click(actionsOf("Newton's laws").getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('This lesson has questions and cannot be deleted.')).toBeInTheDocument();
  });

  it('has no axe violations while confirming', async () => {
    const user = userEvent.setup();
    const { container } = await openTree(user);

    await user.click(actionsOf('Momentum').getByRole('button', { name: 'Archive' }));

    expect((await axe(container)).violations).toEqual([]);
  });
});
