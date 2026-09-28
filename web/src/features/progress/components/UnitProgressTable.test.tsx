import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import {
  getGetSessionHistoryMockHandler,
  getGetSubjectProgressMockHandler,
  getGetWeakSpotsMockHandler,
} from '@/shared/api/generated/progress/progress.msw';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { mechanicsUnitId, sessionHistoryPage, subjectProgress, wavesUnitId, weakSpots } from '@/test/progressFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('UnitProgressTable', () => {
  it('links each unit to its exam start', async () => {
    server.use(
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetSubjectProgressMockHandler(subjectProgress()),
      getGetWeakSpotsMockHandler(weakSpots()),
      getGetSessionHistoryMockHandler(sessionHistoryPage([])),
    );
    renderApp('/student/progress', { session: testSessions.student });

    const table = await screen.findByRole('table', { name: 'Physics units' });
    expect(within(table).getByRole('columnheader', { name: 'Exam' })).toBeInTheDocument();
    expect(
      within(table)
        .getAllByRole('link', { name: 'Unit exam' })
        .map((link) => link.getAttribute('href')),
    ).toEqual([`/student/exam-start/${mechanicsUnitId}`, `/student/exam-start/${wavesUnitId}`]);
  });
});
