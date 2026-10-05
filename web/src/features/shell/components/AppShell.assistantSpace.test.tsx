import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { getGetMyAvatarConversationsMockHandler } from '@/shared/api/generated/avatar/avatar.msw';
import { getGetExamSessionMockHandler } from '@/shared/api/generated/exams/exams.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import { getValidationQueueMock } from '@/shared/api/generated/validation-queue/validation-queue.msw';
import { myAvatarConversationsPage } from '@/test/avatarFixtures';
import { examItem, examSessionId, openExam } from '@/test/examFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const expectNoAssistantRoom = (main: HTMLElement) => {
  expect(main).toHaveClass('pb-24', 'lg:pb-8');
  expect(main).not.toHaveClass('pb-assistant-dock');
};

describe('AppShell assistant space', () => {
  beforeEach(() => {
    server.use(...getValidationQueueMock(), ...getMasteryMock());
  });

  it('reserves room below the content for the assistant button on student pages', async () => {
    renderApp('/student', { session: testSessions.student });

    await screen.findByRole('button', { name: 'Assistant' });

    const main = screen.getByRole('main');
    expect(main).toHaveClass('pb-assistant-dock', 'lg:pb-assistant-dock-desktop');
    expect(main).not.toHaveClass('pb-24');
  });

  it('reserves no assistant room on the full-page assistant', async () => {
    server.use(getGetMyAvatarConversationsMockHandler(myAvatarConversationsPage([])));
    renderApp('/student/assistant', { session: testSessions.student });

    await screen.findByRole('textbox', { name: 'Your question' });

    expectNoAssistantRoom(screen.getByRole('main'));
  });

  it('reserves no assistant room while an exam is being taken', async () => {
    server.use(getGetExamSessionMockHandler(openExam([examItem(1)])));
    renderApp(`/student/exam/${examSessionId}`, { session: testSessions.student });

    await screen.findByRole('button', { name: 'Ask the assistant' });

    expectNoAssistantRoom(screen.getByRole('main'));
  });

  it('reserves no assistant room for teachers', async () => {
    renderApp('/teacher', { session: testSessions.teacher });

    await screen.findByRole('navigation', { name: 'Main navigation' });

    expectNoAssistantRoom(screen.getByRole('main'));
  });
});
