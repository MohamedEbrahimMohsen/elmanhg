import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { masteryOverview } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { baseUsage, freeUsage } from '@/test/subscriptionFixtures';

const openHome = () => {
  server.use(getGetMasteryOverviewMockHandler(masteryOverview()));
  return renderApp('/student', { session: testSessions.student });
};

describe('StudentHomePage plan line', () => {
  it("shows the free plan, today's counter and a subscribe link", async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    openHome();

    expect(await screen.findByText('Your plan: Free')).toBeInTheDocument();
    expect(screen.getByText('Practice questions today: 3 / 10')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
  });

  it('shows the base plan without a counter', async () => {
    server.use(getGetMyUsageMockHandler(baseUsage()));
    openHome();

    expect(await screen.findByText('Your plan: Base')).toBeInTheDocument();
    expect(screen.queryByText(/Practice questions today/)).toBeNull();
  });
});
