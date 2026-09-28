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
import { examItem, sessionHistoryPage, subjectProgress, weakSpots } from '@/test/progressFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const earlierExamId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

describe('ProgressPage best score', () => {
  it('marks the best exam sitting in the history', async () => {
    server.use(
      getGetMasteryOverviewMockHandler(masteryOverview()),
      getGetSubjectProgressMockHandler(subjectProgress()),
      getGetWeakSpotsMockHandler(weakSpots()),
      getGetSessionHistoryMockHandler(
        sessionHistoryPage([
          { ...examItem, isBestScore: true },
          { ...examItem, id: earlierExamId, scorePercent: 60, isBestScore: false },
        ]),
      ),
    );
    const rendered = renderApp('/student/progress', { session: testSessions.student });
    await rendered.router.loadRouteChunk(rendered.router.routesById['/student/progress']);

    const table = await screen.findByRole('table', { name: 'Your sessions, newest first' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent(/90%\s*Best/);
    expect(rows[1]).toHaveTextContent('60%');
    expect(rows[1]).not.toHaveTextContent('Best');
  });
});
