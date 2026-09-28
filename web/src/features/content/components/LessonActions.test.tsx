import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import {
  getDeleteLessonMockHandler,
  getGetLessonMockHandler,
  getPublishLessonMockHandler,
} from '@/shared/api/generated/lessons/lessons.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lesson: LessonDetailResult = {
  id: 'l1',
  unitId: 'u1',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '',
  summary: '',
  videoUrl: null,
  objectives: [],
};

const openEditor = () => renderApp('/admin/lesson/l1', { session: testSessions.admin });

const findActions = async () => within(await screen.findByRole('group', { name: "Actions for Newton's laws" }));

describe('LessonActions', () => {
  let state: string;

  beforeEach(() => {
    state = 'Draft';
    server.use(
      getGetLessonMockHandler(() => ({ ...lesson, state })),
      getPublishLessonMockHandler(() => {
        state = 'Published';
      }),
      getDeleteLessonMockHandler(),
      getGetSubjectsMockHandler([]),
    );
  });

  it('publishes from the editor and shows the new state', async () => {
    const user = userEvent.setup();
    openEditor();

    const actions = await findActions();
    await user.click(actions.getByRole('button', { name: 'Publish' }));
    await user.click(actions.getByRole('button', { name: 'Yes, publish' }));

    expect(await screen.findByText('Published')).toBeInTheDocument();
    const updated = await findActions();
    expect(await updated.findByRole('button', { name: 'Move to draft' })).toBeInTheDocument();
    expect(updated.getByRole('button', { name: 'Archive' })).toBeInTheDocument();
  });

  it('returns to the content tree after deleting', async () => {
    const user = userEvent.setup();
    const { router } = openEditor();

    const actions = await findActions();
    await user.click(actions.getByRole('button', { name: 'Delete' }));
    await user.click(actions.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('Lesson deleted.')).toBeInTheDocument();
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/admin/content');
    });
  });

  it('does not offer delete for a published lesson', async () => {
    state = 'Published';
    openEditor();

    const actions = await findActions();

    expect(actions.getByRole('button', { name: 'Archive' })).toBeInTheDocument();
    expect(actions.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('stays on the editor when delete is rejected', async () => {
    server.use(
      http.delete('*/api/lessons/:lessonId', () => HttpResponse.json({ code: 'LESSON_IS_PUBLISHED' }, { status: 400 })),
    );
    const user = userEvent.setup();
    const { router } = openEditor();

    const actions = await findActions();
    await user.click(actions.getByRole('button', { name: 'Delete' }));
    await user.click(actions.getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('Move this lesson to draft or archive it before deleting it.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/lesson/l1');
  });
});
