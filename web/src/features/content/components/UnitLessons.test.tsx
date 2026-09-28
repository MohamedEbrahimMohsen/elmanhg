import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { LessonResult, SubjectDetailResult } from '@/shared/api/generated/model';
import {
  getCreateLessonMockHandler,
  getGetLessonMockHandler,
  getGetLessonsMockHandler,
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
  units: [{ id: 'u1', subjectId: 's1', name: 'Mechanics', order: 1, lessonCount: 2 }],
};

const lessons: LessonResult[] = [
  {
    id: 'l1',
    unitId: 'u1',
    name: "Newton's laws",
    order: 1,
    state: 'Draft',
    questionCount: 0,
    servableQuestionCount: 0,
  },
  { id: 'l2', unitId: 'u1', name: 'Momentum', order: 2, state: 'Draft', questionCount: 0, servableQuestionCount: 0 },
];

const openContent = () => renderApp('/admin/content', { session: testSessions.admin });

const expandUnits = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(await screen.findByRole('button', { name: 'Show units' }));
  return within(await screen.findByRole('list', { name: 'Units of Physics' }));
};

const expandLessons = async (user: ReturnType<typeof userEvent.setup>) => {
  const units = await expandUnits(user);
  const toggle = units.getByRole('button', { name: 'Show lessons' });
  await user.click(toggle);
  return toggle;
};

describe('UnitLessons', () => {
  let bodies: unknown[];

  beforeEach(() => {
    bodies = [];
    server.use(
      getGetSubjectsMockHandler([{ id: 's1', name: 'Physics', order: 1, unitCount: 1 }]),
      getGetSubjectMockHandler(physics),
      getGetLessonsMockHandler(lessons),
    );
  });

  it('shows the lesson count of each unit', async () => {
    const user = userEvent.setup();
    openContent();

    const units = await expandUnits(user);

    expect(units.getByText('2 lessons')).toBeInTheDocument();
  });

  it('shows the lessons of a unit with links to the editor', async () => {
    const user = userEvent.setup();
    openContent();

    const toggle = await expandLessons(user);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    const list = within(await screen.findByRole('list', { name: 'Lessons of Mechanics' }));
    const [first, second] = list.getAllByRole('listitem');
    expect(first).toHaveTextContent("Newton's laws");
    expect(second).toHaveTextContent('Momentum');
    expect(list.getByRole('link', { name: "Newton's laws" })).toHaveAttribute('href', '/admin/lesson/l1');
    expect(list.getAllByText('Draft')).toHaveLength(2);
  });

  it('shows the empty state for a unit without lessons', async () => {
    server.use(getGetLessonsMockHandler([]));
    const user = userEvent.setup();
    openContent();

    await expandLessons(user);

    expect(await screen.findByText('No lessons in this unit yet.')).toBeInTheDocument();
  });

  it('shows an error and recovers on retry', async () => {
    server.use(http.get('*/api/lessons', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })));
    const user = userEvent.setup();
    openContent();

    await expandLessons(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load lessons');
    server.use(getGetLessonsMockHandler(lessons));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('list', { name: 'Lessons of Mechanics' })).toBeInTheDocument();
  });

  it('adds a lesson and opens the editor', async () => {
    server.use(
      getCreateLessonMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return { id: 'l9' };
      }),
      getGetLessonMockHandler({
        id: 'l9',
        unitId: 'u1',
        name: 'Energy',
        order: 3,
        state: 'Draft',
        explanation: '',
        summary: '',
        videoUrl: null,
        objectives: [],
      }),
    );
    const user = userEvent.setup();
    const { router } = openContent();

    await expandLessons(user);
    await user.type(await screen.findByLabelText('Lesson name'), 'Energy');
    await user.click(screen.getByRole('button', { name: 'Add lesson' }));

    expect(await screen.findByText('Lesson added.')).toBeInTheDocument();
    expect(bodies).toEqual([{ unitId: 'u1', name: 'Energy' }]);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/admin/lesson/l9');
    });
  });

  it('shows the required error without calling the API', async () => {
    server.use(
      getCreateLessonMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return { id: 'l9' };
      }),
    );
    const user = userEvent.setup();
    openContent();

    await expandLessons(user);
    await screen.findByRole('list', { name: 'Lessons of Mechanics' });
    await user.click(screen.getByRole('button', { name: 'Add lesson' }));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(bodies).toEqual([]);
  });

  it('shows an error toast when a unit with lessons cannot be deleted', async () => {
    server.use(
      http.delete('*/api/subjects/:subjectId/units/:unitId', () =>
        HttpResponse.json({ code: 'UNIT_HAS_LESSONS' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    openContent();

    const units = await expandUnits(user);
    const [mechanics] = units.getAllByRole('listitem');
    if (!mechanics) {
      throw new Error('The unit list is empty.');
    }
    await user.click(within(mechanics).getByRole('button', { name: 'Delete' }));
    await user.click(within(mechanics).getByRole('button', { name: 'Yes, delete' }));

    expect(await screen.findByText('This unit has lessons and cannot be deleted.')).toBeInTheDocument();
  });

  it('has no axe violations when lessons are shown', async () => {
    const user = userEvent.setup();
    const { container } = openContent();

    await expandLessons(user);
    await screen.findByRole('list', { name: 'Lessons of Mechanics' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
