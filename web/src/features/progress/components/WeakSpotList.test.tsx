import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { WeakLessonResult, WeakObjectiveResult, WeakSpotsResult } from '@/shared/api/generated/model';
import {
  getGetSessionHistoryMockHandler,
  getGetSubjectProgressMockHandler,
  getGetWeakSpotsMockHandler,
} from '@/shared/api/generated/progress/progress.msw';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import {
  physicsSubjectId,
  sessionHistoryPage,
  subjectProgress,
  weakLessonId,
  weakSpots,
} from '@/test/progressFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const lessonId = (index: number) => `aaaaaaaa-aaaa-4aaa-8aaa-00000000000${String(index)}`;

const lesson = (index: number): WeakLessonResult => ({
  lessonId: lessonId(index),
  lessonName: `Lesson ${String(index)}`,
  subjectId: physicsSubjectId,
  subjectName: 'Physics',
  servableCount: 5,
  masteredCount: 1,
  seenCount: 3,
  masteryPercent: 20,
});

const objective = (index: number): WeakObjectiveResult => ({
  objectiveId: `bbbbbbbb-bbbb-4bbb-8bbb-00000000000${String(index)}`,
  text: `Objective ${String(index)}`,
  lessonId: lessonId(index),
  lessonName: `Lesson ${String(index)}`,
  subjectId: physicsSubjectId,
  subjectName: 'Physics',
  servableCount: 2,
  masteredCount: 0,
  seenCount: 1,
  masteryPercent: 0,
});

const openProgress = (weak: WeakSpotsResult = weakSpots()) => {
  server.use(
    getGetMasteryOverviewMockHandler(masteryOverview()),
    getGetSubjectProgressMockHandler(subjectProgress()),
    getGetWeakSpotsMockHandler(weak),
    getGetSessionHistoryMockHandler(sessionHistoryPage([])),
  );
  renderApp('/student/progress', { session: testSessions.student });
};

const fiveLessons = () => weakSpots({ lessons: [1, 2, 3, 4, 5].map(lesson), objectives: [] });

describe('WeakSpotList', () => {
  it('shows the first three weak lessons and a Show all button when there are more', async () => {
    openProgress(fiveLessons());

    expect(await screen.findAllByRole('link', { name: 'Train now' })).toHaveLength(3);
    expect(screen.queryByText('Lesson 4')).toBeNull();
    expect(screen.getByRole('button', { name: 'Show all (5)' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('shows every weak lesson after Show all and the first three again after Show less', async () => {
    openProgress(fiveLessons());
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Show all (5)' }));

    expect(screen.getAllByRole('link', { name: 'Train now' })).toHaveLength(5);
    const less = screen.getByRole('button', { name: 'Show less' });
    expect(less).toHaveAttribute('aria-expanded', 'true');

    await user.click(less);

    expect(screen.getAllByRole('link', { name: 'Train now' })).toHaveLength(3);
  });

  it('shows no Show all button when there are three or fewer', async () => {
    openProgress();

    await screen.findByText('Weakest lessons');

    expect(screen.queryByRole('button', { name: /Show all/ })).toBeNull();
  });

  it('caps weak objectives at three as well', async () => {
    openProgress(weakSpots({ lessons: [], objectives: [1, 2, 3, 4].map(objective) }));

    expect(await screen.findAllByRole('link', { name: 'Train now' })).toHaveLength(3);
    expect(screen.getByRole('button', { name: 'Show all (4)' })).toBeInTheDocument();
  });

  it('puts the name, meta, mastery and Train now in one row', async () => {
    openProgress();

    await screen.findByText('Weakest lessons');
    const row = screen.getAllByRole('listitem').find((item) => within(item).queryByText("Ohm's law"));
    if (!row) {
      throw new Error("No row shows Ohm's law.");
    }

    expect(within(row).getByText('Physics')).toBeInTheDocument();
    expect(within(row).getByText('Mastery 20%')).toBeInTheDocument();
    expect(within(row).getByRole('progressbar', { name: "Ohm's law mastery" })).toHaveAttribute('value', '20');
    expect(within(row).getByRole('link', { name: 'Train now' })).toHaveAttribute(
      'href',
      `/student/lesson/${weakLessonId}/practice`,
    );
  });
});
