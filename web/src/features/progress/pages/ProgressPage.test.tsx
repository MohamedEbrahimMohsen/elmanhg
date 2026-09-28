import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { SubjectProgressResult, WeakSpotsResult } from '@/shared/api/generated/model';
import {
  getGetSessionHistoryMockHandler,
  getGetSubjectProgressMockHandler,
  getGetWeakSpotsMockHandler,
} from '@/shared/api/generated/progress/progress.msw';
import { axe } from '@/test/axe';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import {
  sessionHistoryPage,
  subjectProgress,
  weakLessonId,
  weakObjectiveLessonId,
  weakSpots,
} from '@/test/progressFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

interface ProgressData {
  subjects?: SubjectProgressResult[];
  weak?: WeakSpotsResult;
}

const openProgress = ({ subjects, weak }: ProgressData = {}, lng: 'en' | 'ar' = 'en') => {
  server.use(
    getGetMasteryOverviewMockHandler(masteryOverview()),
    getGetSubjectProgressMockHandler(subjects ?? subjectProgress()),
    getGetWeakSpotsMockHandler(weak ?? weakSpots()),
    getGetSessionHistoryMockHandler(sessionHistoryPage()),
  );
  return renderApp('/student/progress', { session: testSessions.student, lng });
};

const failOnce = (path: string) =>
  http.get(`*${path}`, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), { once: true });

const listItemWith = async (text: string) => {
  await screen.findAllByText(text);
  const item = screen.getAllByRole('listitem').find((element) => within(element).queryByText(text));
  if (!item) {
    throw new Error(`No list item shows ${text}.`);
  }
  return item;
};

describe('ProgressPage', () => {
  it('shows loading then the headline summary', async () => {
    openProgress();

    expect(await screen.findByRole('status', { name: 'Loading your summary…' })).toBeInTheDocument();
    expect(await screen.findByText('40 of 60 questions left for you')).toBeInTheDocument();
    expect(screen.getByText('Seen 30 · Mastered 20 · Streak: 3 days')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'My progress' })).toBeInTheDocument();
  });

  it('shows each subject with its units, mastery and best exam', async () => {
    openProgress();

    const physics = await screen.findByRole('article', { name: 'Physics' });

    expect(within(physics).getByRole('progressbar', { name: 'Physics mastery' })).toHaveAttribute('value', '40');
    expect(within(physics).getByText('40% mastered')).toBeInTheDocument();
    const mechanics = within(physics).getByRole('row', { name: /Mechanics/ });
    expect(within(mechanics).getByText('50%')).toBeInTheDocument();
    expect(within(mechanics).getByText('88%')).toBeInTheDocument();
    const waves = within(physics).getByRole('row', { name: /Waves/ });
    expect(within(waves).getByText('0%')).toBeInTheDocument();
    expect(within(waves).getByText('—')).toBeInTheDocument();
  });

  it('shows the subjects empty state', async () => {
    openProgress({ subjects: [] });

    expect(await screen.findByText('No subjects yet.')).toBeInTheDocument();
  });

  it('lists weak lessons with a Train now link to practice', async () => {
    openProgress();

    const item = await listItemWith("Ohm's law");

    expect(within(item).getByText('Physics')).toBeInTheDocument();
    expect(within(item).getByText('Mastery 20%')).toBeInTheDocument();
    expect(within(item).getByRole('link', { name: 'Train now' })).toHaveAttribute(
      'href',
      `/student/lesson/${weakLessonId}/practice`,
    );
  });

  it('lists weak objectives with their lesson and a Train now link', async () => {
    openProgress();

    const item = await listItemWith("State Ohm's law");

    expect(within(item).getByText("Ohm's law · Physics")).toBeInTheDocument();
    expect(within(item).getByRole('link', { name: 'Train now' })).toHaveAttribute(
      'href',
      `/student/lesson/${weakObjectiveLessonId}/practice`,
    );
  });

  it('shows the weak-spots empty state', async () => {
    openProgress({ weak: weakSpots({ lessons: [], objectives: [] }) });

    expect(await screen.findByText('No data yet. Practise a lesson to find your weak spots.')).toBeInTheDocument();
    expect(screen.queryByText('Weakest lessons')).toBeNull();
  });

  it('shows the subjects error and retries', async () => {
    openProgress();
    server.use(failOnce('/api/progress/subjects'));
    const user = userEvent.setup();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load subject progress.');

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('article', { name: 'Physics' })).toBeInTheDocument();
  });

  it('shows the weak-spots error and retries', async () => {
    openProgress();
    server.use(failOnce('/api/progress/weak-spots'));
    const user = userEvent.setup();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load weak spots.');

    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('Weakest lessons')).toBeInTheDocument();
  });

  it('renders right to left in Arabic', async () => {
    openProgress(undefined, 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'تقدّمي' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect((await screen.findAllByRole('link', { name: 'درّب الآن' })).length).toBeGreaterThan(0);
    const physics = await screen.findByRole('article', { name: 'Physics' });
    const mechanics = within(physics).getByRole('row', { name: /Mechanics/ });
    expect(within(mechanics).getByText('٥٠٪')).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = openProgress();

    await screen.findByRole('article', { name: 'Physics' });
    await screen.findByText('Weakest lessons');
    await screen.findByRole('table', { name: 'Your sessions, newest first' });
    await screen.findByText('40 of 60 questions left for you');

    expect((await axe(container)).violations).toEqual([]);
  });
});
