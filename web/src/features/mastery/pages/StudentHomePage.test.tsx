import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { MasteryOverviewResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import { chemistryId, masteryOverview, physicsId } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const openHome = (overrides?: Partial<MasteryOverviewResult>, lng: 'en' | 'ar' = 'en') => {
  server.use(getGetMasteryOverviewMockHandler(masteryOverview(overrides)));
  return renderApp('/student', { session: testSessions.student, lng });
};

describe('StudentHomePage', () => {
  it('shows a loading state then the headline counter with seen, mastered and streak', async () => {
    openHome();

    expect(await screen.findByRole('status', { name: 'Loading your progress…' })).toBeInTheDocument();
    expect(await screen.findByText('40 of 60 questions left for you')).toBeInTheDocument();
    expect(screen.getByText('Seen 30 · Mastered 20 · Streak: 3 days')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
  });

  it('links the suggested lesson to its practice page', async () => {
    openHome();

    expect(await screen.findByText("Newton's laws")).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Train now' })).toHaveAttribute(
      'href',
      '/student/lesson/88888888-8888-4888-8888-888888888888/practice',
    );
  });

  it('hides the suggested lesson when there is none', async () => {
    openHome({ nextLesson: null });

    await screen.findByText('40 of 60 questions left for you');

    expect(screen.queryByText('Suggested next lesson')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Train now' })).toBeNull();
  });

  it('shows every subject with its mastery bar and available questions', async () => {
    openHome();

    const physics = await screen.findByRole('article', { name: 'Physics' });
    const chemistry = screen.getByRole('article', { name: 'Chemistry' });

    expect(within(physics).getByRole('progressbar', { name: 'Physics mastery' })).toHaveAttribute('value', '40');
    expect(within(physics).getByText('40% mastered · 50 questions available')).toBeInTheDocument();
    expect(within(chemistry).getByRole('progressbar', { name: 'Chemistry mastery' })).toHaveAttribute('value', '0');
  });

  it('shows the empty state when there are no subjects', async () => {
    openHome({ subjects: [] });

    expect(await screen.findByText('No subjects yet.')).toBeInTheDocument();
  });

  it('shows the error state and retries', async () => {
    server.use(getGetMasteryOverviewMockHandler(masteryOverview()));
    server.use(
      http.get('*/api/mastery/overview', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), {
        once: true,
      }),
    );
    const user = userEvent.setup();
    renderApp('/student', { session: testSessions.student });

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load your progress.');

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('40 of 60 questions left for you')).toBeInTheDocument();
  });

  it('renders right-to-left with the Arabic counter in Arabic', async () => {
    openHome(undefined, 'ar');

    expect(await screen.findByText('متبقّي لك 40 سؤال من 60')).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(screen.getByRole('link', { name: 'درّب الآن' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = openHome();

    await screen.findByText('40 of 60 questions left for you');

    expect((await axe(container)).violations).toEqual([]);
  });

  it('links each subject card to its subject page', async () => {
    openHome();

    const physics = await screen.findByRole('article', { name: 'Physics' });

    expect(within(physics).getByRole('link', { name: 'Physics' })).toHaveAttribute(
      'href',
      `/student/subject/${physicsId}`,
    );
  });

  it('lists chosen subjects under Your subjects and the rest under Other subjects', async () => {
    const base = masteryOverview();
    openHome({
      subjects: base.subjects.map((subject) => ({ ...subject, isInterested: subject.subjectId === chemistryId })),
    });

    const chemistry = await screen.findByRole('article', { name: 'Chemistry' });
    const physics = screen.getByRole('article', { name: 'Physics' });
    const yours = screen.getByRole('heading', { name: 'Your subjects' });
    const others = screen.getByRole('heading', { name: 'Other subjects' });

    expect(yours.compareDocumentPosition(chemistry) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(chemistry.compareDocumentPosition(others) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(others.compareDocumentPosition(physics) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('links to editing my subjects', async () => {
    openHome();

    expect(await screen.findByRole('link', { name: 'Edit my subjects' })).toHaveAttribute('href', '/onboarding');
  });
});
