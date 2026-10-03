import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { SubjectProgressResult, WeakSpotsResult } from '@/shared/api/generated/model';
import {
  getGetSessionHistoryMockHandler,
  getGetSubjectProgressMockHandler,
  getGetWeakSpotsMockHandler,
} from '@/shared/api/generated/progress/progress.msw';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { sessionHistoryPage, subjectProgress, weakSpots } from '@/test/progressFixtures';
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

describe('ProgressPage dates', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows session dates in readable Arabic with Latin digits', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-03T12:00:00Z'));
    openProgress({}, 'ar');

    const row = await screen.findByRole('row', { name: /Ohm's law/ });
    const date = within(row).getByText(/^20 سبتمبر 2026، \d{1,2}:\d{2} [صم]$/u);
    expect(date.textContent).not.toMatch(/[٠-٩۰-۹]/u);
  });

  it('shows a session from the last day as relative time', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-09-20T11:00:00Z'));
    openProgress({}, 'ar');

    const row = await screen.findByRole('row', { name: /Ohm's law/ });
    expect(within(row).getByText('قبل ساعتين').getAttribute('title')).toMatch(/^20 سبتمبر 2026، /u);
  });
});
