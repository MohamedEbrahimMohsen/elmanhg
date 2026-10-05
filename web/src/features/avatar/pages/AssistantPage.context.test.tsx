import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  getGetMyAvatarConversationsMockHandler,
  getSendAvatarMessageMockHandler,
} from '@/shared/api/generated/avatar/avatar.msw';
import {
  getGetStudentSubjectMockHandler,
  getGetStudentUnitMockHandler,
} from '@/shared/api/generated/browse/browse.msw';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { SendAvatarMessageCommand } from '@/shared/api/generated/model';
import { avatarReply, myAvatarConversationsPage } from '@/test/avatarFixtures';
import { browseLessonId, studentSubject, studentUnit } from '@/test/browseFixtures';
import { masteryOverview, physicsId } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

type User = ReturnType<typeof userEvent.setup>;

const captureSends = () => {
  const bodies: SendAvatarMessageCommand[] = [];
  server.use(
    getSendAvatarMessageMockHandler(async ({ request }) => {
      bodies.push((await request.json()) as SendAvatarMessageCommand);
      return avatarReply();
    }),
  );
  return bodies;
};

const openPage = () => {
  const user = userEvent.setup();
  renderApp('/student/assistant', { session: testSessions.student });
  return { user };
};

const pickPhysics = async (user: User) => {
  await user.selectOptions(await screen.findByRole('combobox', { name: 'Subject' }), 'Physics');
  const lesson = screen.getByRole('combobox', { name: 'Lesson' });
  await waitFor(() => {
    expect(lesson).toBeEnabled();
  });
  return lesson;
};

const ask = async (user: User, text: string) => {
  await user.type(screen.getByRole('textbox', { name: 'Your question' }), text);
  await user.click(screen.getByRole('button', { name: 'Send' }));
};

describe('AssistantPage context picker', () => {
  beforeEach(() => {
    server.use(
      getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([])),
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetStudentSubjectMockHandler(studentSubject({ id: physicsId })),
      getGetStudentUnitMockHandler(studentUnit()),
    );
  });

  it('lists the subjects and the lessons of the chosen subject by unit', async () => {
    const { user } = openPage();
    const subject = await screen.findByRole('combobox', { name: 'Subject' });

    expect(
      within(subject)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['General', 'Physics', 'Chemistry']);
    const lesson = await pickPhysics(user);

    const group = within(lesson).getByRole('group', { name: 'Mechanics' });
    expect(
      within(group)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Forces', 'Energy']);
  });

  it('sends the picked lesson as the lesson context with the lesson greeting', async () => {
    const bodies = captureSends();
    const { user } = openPage();
    const lesson = await pickPhysics(user);

    await user.selectOptions(lesson, 'Energy');
    expect(screen.getByText(/assistant for "Energy"/)).toBeInTheDocument();
    await ask(user, 'What is energy?');

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]).toMatchObject({ entryPoint: 'Lesson', lessonId: browseLessonId, conversationId: null });
  });

  it('disables locked lessons for a free student', async () => {
    const unit = studentUnit();
    server.use(
      getGetStudentUnitMockHandler({
        ...unit,
        lessons: unit.lessons.map((lesson) => (lesson.name === 'Energy' ? { ...lesson, isLocked: true } : lesson)),
      }),
    );
    const { user } = openPage();
    const lesson = await pickPhysics(user);

    expect(within(lesson).getByRole('option', { name: 'Energy (subscribers only)' })).toBeDisabled();
    expect(within(lesson).getByRole('option', { name: 'Forces' })).toBeEnabled();
  });

  it('returns to the general context when the subject changes', async () => {
    const bodies = captureSends();
    const { user } = openPage();
    const lesson = await pickPhysics(user);
    await user.selectOptions(lesson, 'Energy');

    await user.selectOptions(screen.getByRole('combobox', { name: 'Subject' }), 'General');

    expect(lesson).toHaveValue('');
    expect(screen.getByText(/Open the lesson you need/)).toBeInTheDocument();
    await ask(user, 'How do I study?');
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.entryPoint).toBe('Global');
  });

  it('shows the context line instead of the picker once the chat has started', async () => {
    captureSends();
    const { user } = openPage();
    const lesson = await pickPhysics(user);
    await user.selectOptions(lesson, 'Energy');

    await ask(user, 'What is energy?');

    expect(await screen.findByText(avatarReply().reply)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Subject' })).not.toBeInTheDocument();
    expect(screen.getByText('Context: Energy')).toBeInTheDocument();
  });

  it('shows the lessons error with retry and recovers', async () => {
    server.use(
      http.get('*/api/browse/units/:id', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const { user } = openPage();

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Subject' }), 'Physics');
    expect(await screen.findByText('Subjects and lessons could not load.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    const lesson = await screen.findByRole('combobox', { name: 'Lesson' });
    expect(await within(lesson).findByRole('option', { name: 'Forces' })).toBeInTheDocument();
  });
});
