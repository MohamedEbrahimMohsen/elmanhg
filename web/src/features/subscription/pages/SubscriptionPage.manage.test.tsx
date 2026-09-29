import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import type { EntitlementResult, StartCheckoutCommand } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import {
  getCancelSubscriptionMockHandler,
  getGetMyEntitlementMockHandler,
  getGetMyPaymentMockHandler,
  getGetMyPaymentsMockHandler,
  getStartCheckoutMockHandler,
} from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import {
  baseEntitlement,
  checkoutPaymentId,
  checkoutResult,
  payment,
  paymentsPage,
  planCatalogue,
} from '@/test/subscriptionFixtures';

async function openSubscription(entitlement: EntitlementResult, lng: Language = 'en') {
  server.use(
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMyEntitlementMockHandler(entitlement),
    getGetMyPaymentsMockHandler(paymentsPage([])),
    getGetServableQuestionCountMockHandler({ count: 1234 }),
    getGetMyPaymentMockHandler(payment({ id: checkoutPaymentId, status: 'Pending', completedAt: null })),
  );
  const rendered = renderApp('/student/subscription', { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/subscription']);
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/fake-checkout/$paymentId']);
  return rendered;
}

const baseCard = () => screen.findByRole('article', { name: 'Base' });

async function openCancelDialog(user: ReturnType<typeof userEvent.setup>, name = 'Cancel Base') {
  await user.click(await screen.findByRole('button', { name }));
  return screen.findByRole('dialog');
}

describe('SubscriptionPage plan management', () => {
  it('shows renew buttons for each Base period when renewal is open', async () => {
    await openSubscription(baseEntitlement({ canRenew: true }));

    const base = await baseCard();
    expect(within(base).getByRole('button', { name: 'Renew monthly' })).toBeEnabled();
    expect(within(base).getByRole('button', { name: 'Renew for a term' })).toBeEnabled();
    expect(within(base).getByRole('button', { name: 'Renew yearly' })).toBeEnabled();
    expect(within(base).queryByRole('button', { name: /Subscribe/ })).toBeNull();
  });

  it('starts a Base renewal checkout for the chosen period', async () => {
    const user = userEvent.setup();
    let sent: StartCheckoutCommand | undefined;
    server.use(
      getStartCheckoutMockHandler(async ({ request }) => {
        sent = (await request.json()) as StartCheckoutCommand;
        return checkoutResult();
      }),
    );
    await openSubscription(baseEntitlement({ canRenew: true }));

    await user.click(within(await baseCard()).getByRole('button', { name: 'Renew yearly' }));

    expect(await screen.findByRole('heading', { name: 'Paymob checkout (simulation)' })).toBeInTheDocument();
    expect(sent).toEqual({ plan: 'Base', period: 'Yearly' });
  });

  it('shows a renew button for Ask a Teacher when its renewal is open', async () => {
    await openSubscription(baseEntitlement({ withAskTeacher: true, canRenew: true }));

    const askTeacher = await screen.findByRole('article', { name: 'Ask a Teacher' });
    expect(within(askTeacher).getByRole('button', { name: 'Renew' })).toBeEnabled();
  });

  it('cancels a plan after confirming in the dialog', async () => {
    const user = userEvent.setup();
    server.use(getCancelSubscriptionMockHandler(baseEntitlement({ status: 'Cancelled' })));
    await openSubscription(baseEntitlement());

    const dialog = await openCancelDialog(user);
    expect(within(dialog).getByRole('heading', { name: 'Cancel Base?' })).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Cancel subscription' }));

    expect(await screen.findByText('Subscription cancelled')).toBeInTheDocument();
    expect(await screen.findByText(/Base — cancelled, available until/)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
  });

  it('keeps the plan when the dialog is dismissed', async () => {
    const user = userEvent.setup();
    await openSubscription(baseEntitlement());

    const dialog = await openCancelDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'Keep subscription' }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull();
    });
    expect(screen.getByText(/Base — active until/)).toBeInTheDocument();
  });

  it('shows an error toast when cancelling fails', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/subscriptions/:subscriptionId/cancel', () =>
        HttpResponse.json({ code: 'SUBSCRIPTION_ENDED' }, { status: 400 }),
      ),
    );
    await openSubscription(baseEntitlement());

    const dialog = await openCancelDialog(user);
    await user.click(within(dialog).getByRole('button', { name: 'Cancel subscription' }));

    expect(await screen.findByText('This subscription has ended or was cancelled.')).toBeInTheDocument();
  });

  it('hides the cancel button for a cancelled plan', async () => {
    await openSubscription(baseEntitlement({ status: 'Cancelled' }));

    expect(await screen.findByText(/Base — cancelled, available until/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel Base' })).toBeNull();
  });

  it('shows the grace wording for an active plan past its period end', async () => {
    await openSubscription(baseEntitlement({ inGracePeriod: true }));

    expect(await screen.findByText(/Base — period ended, available until/)).toBeInTheDocument();
  });

  it('renders the cancel dialog right-to-left in Arabic', async () => {
    const user = userEvent.setup();
    await openSubscription(baseEntitlement(), 'ar');

    const dialog = await openCancelDialog(user, 'إلغاء الأساسية');

    expect(within(dialog).getByRole('heading', { name: 'إلغاء الأساسية؟' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations with the cancel dialog open', async () => {
    const user = userEvent.setup();
    await openSubscription(baseEntitlement());

    const dialog = await openCancelDialog(user);

    expect((await axe(dialog)).violations).toEqual([]);
  });
});
