import { createMemoryHistory, createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/renderWithProviders';
import { freeUsage } from '@/test/subscriptionFixtures';
import type { PaywallReason } from '../api/paywall';
import { PaywallDialog } from './PaywallDialog';

function renderDialog(reason: PaywallReason, onClose = vi.fn(), lng: Language = 'en') {
  const rootRoute = createRootRoute({ component: () => <PaywallDialog reason={reason} onClose={onClose} /> });
  const router = createRouter({ routeTree: rootRoute, history: createMemoryHistory({ initialEntries: ['/'] }) });
  return renderWithProviders(<RouterProvider router={router} />, { lng });
}

describe('PaywallDialog', () => {
  it('shows the daily limit with the configured count and a subscribe link', async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    renderDialog('dailyQuiz');

    expect(await screen.findByRole('dialog', { name: 'Free limit reached' })).toBeInTheDocument();
    expect(await screen.findByText(/10 practice questions/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subscribe' })).toHaveAttribute('href', '/student/subscription');
  });

  it('shows the subscribers-only message for a locked lesson', async () => {
    renderDialog('lesson');

    expect(await screen.findByRole('dialog', { name: 'For subscribers' })).toBeInTheDocument();
    expect(screen.getByText('This lesson is for subscribers only.')).toBeInTheDocument();
  });

  it('calls onClose when Later is pressed', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    renderDialog('exam', onClose);

    await user.click(await screen.findByRole('button', { name: 'Later' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders in Arabic', async () => {
    server.use(getGetMyUsageMockHandler(freeUsage()));
    renderDialog('dailyQuiz', vi.fn(), 'ar');

    expect(await screen.findByRole('dialog', { name: 'انتهى الحد المجاني' })).toBeInTheDocument();
  });
});
