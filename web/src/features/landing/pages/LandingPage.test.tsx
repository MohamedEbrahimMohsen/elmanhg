import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RecordFunnelEventRequest } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { planCatalogue } from '@/test/subscriptionFixtures';

const serverError = () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 });
const spaces = { normalizer: (text: string) => text.replace(/\s+/gu, ' ') };

describe('LandingPage', () => {
  beforeEach(() => {
    server.use(
      getGetServableQuestionCountMockHandler({ count: 1234 }),
      getGetPlanCatalogueMockHandler(planCatalogue()),
    );
  });

  it('shows the live question count in the hero', async () => {
    renderApp('/');

    expect(
      await screen.findByRole('heading', { level: 1, name: '1,234 questions checked by real teachers' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Our goal is 100,000 questions\./u)).toBeInTheDocument();
  });

  it('shows the headline without a number while the count is unavailable', async () => {
    server.use(http.get('*/api/questions/servable-count', serverError));
    renderApp('/');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Questions checked by real teachers' }),
    ).toBeInTheDocument();
  });

  it('shows the plans after loading', async () => {
    renderApp('/');

    expect(await screen.findByRole('status', { name: 'Loading plans…' })).toBeInTheDocument();
    const base = await screen.findByRole('article', { name: 'Base' });
    expect(screen.getByRole('heading', { name: 'Free' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ask a Teacher' })).toBeInTheDocument();
    expect(within(base).getByText('EGP 199/month', spaces)).toBeInTheDocument();
  });

  it('shows an error and retries the plans', async () => {
    server.use(http.get('*/api/plans', serverError));
    const user = userEvent.setup();
    renderApp('/');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the plans.');
    server.resetHandlers();
    server.use(
      getGetServableQuestionCountMockHandler({ count: 1234 }),
      getGetPlanCatalogueMockHandler(planCatalogue()),
    );
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Base' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Free' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ask a Teacher' })).toBeInTheDocument();
  });

  it('starts sign-up from the call to action', async () => {
    const user = userEvent.setup();
    renderApp('/');

    await user.click(await screen.findByRole('link', { name: 'Start for free' }));

    expect(await screen.findByRole('heading', { name: 'Create account' })).toBeInTheDocument();
  });

  it('offers sign-in from the top bar', async () => {
    renderApp('/');

    expect(await screen.findByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login');
  });

  it('records a landing view', async () => {
    const types: string[] = [];
    server.use(
      http.post('*/api/analytics/funnel-events', async ({ request }) => {
        types.push(((await request.json()) as RecordFunnelEventRequest).type);
        return new HttpResponse(null, { status: 200 });
      }),
    );
    renderApp('/');

    await waitFor(() => {
      expect(types).toEqual(['LandingViewed']);
    });
  });

  it('renders right-to-left in Arabic', async () => {
    renderApp('/', { lng: 'ar' });

    expect(await screen.findByRole('link', { name: 'ابدأ مجانًا' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/');

    await screen.findByRole('article', { name: 'Base' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
