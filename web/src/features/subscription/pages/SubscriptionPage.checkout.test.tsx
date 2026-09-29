import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { CheckoutResult, EntitlementResult, StartCheckoutCommand } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import {
  getGetMyEntitlementMockHandler,
  getGetMyPaymentMockHandler,
  getGetMyPaymentsMockHandler,
  getStartCheckoutMockHandler,
} from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { redirectToExternal } from '@/shared/lib/redirect';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import {
  baseEntitlement,
  checkoutPaymentId,
  checkoutResult,
  freeEntitlement,
  payment,
  paymentsPage,
  planCatalogue,
} from '@/test/subscriptionFixtures';

vi.mock('@/shared/lib/redirect', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/shared/lib/redirect')>()),
  redirectToExternal: vi.fn(),
}));

const paymobUrl = 'https://accept.paymob.com/unifiedcheckout/?publicKey=pk&clientSecret=cs';

async function openSubscription(entitlement: EntitlementResult = freeEntitlement()) {
  server.use(
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMyEntitlementMockHandler(entitlement),
    getGetMyPaymentsMockHandler(paymentsPage([])),
    getGetServableQuestionCountMockHandler({ count: 1234 }),
    getGetMyPaymentMockHandler(payment({ id: checkoutPaymentId, status: 'Pending', completedAt: null })),
  );
  const rendered = renderApp('/student/subscription', { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/subscription']);
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/fake-checkout/$paymentId']);
  return rendered;
}

const baseCard = () => screen.findByRole('article', { name: 'Base' });
const askTeacherCard = () => screen.findByRole('article', { name: 'Ask a Teacher' });

describe('SubscriptionPage checkout', () => {
  it('shows a subscribe button for each Base period for a free student', async () => {
    await openSubscription();

    const base = await baseCard();
    expect(within(base).getByRole('button', { name: 'Subscribe monthly' })).toBeEnabled();
    expect(within(base).getByRole('button', { name: 'Subscribe for a term' })).toBeEnabled();
    expect(within(base).getByRole('button', { name: 'Subscribe yearly' })).toBeEnabled();
  });

  it('disables Ask a Teacher subscribe with a hint when the student has no Base', async () => {
    await openSubscription();

    const button = within(await askTeacherCard()).getByRole('button', { name: 'Subscribe' });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription('Subscribe to Base first');
  });

  it('enables Ask a Teacher subscribe for a Base student', async () => {
    await openSubscription(baseEntitlement());

    expect(within(await askTeacherCard()).getByRole('button', { name: 'Subscribe' })).toBeEnabled();
    expect(within(await baseCard()).queryByRole('button')).toBeNull();
  });

  it('hides subscribe buttons for active plans', async () => {
    await openSubscription(baseEntitlement({ withAskTeacher: true }));

    await baseCard();
    expect(screen.queryByRole('button', { name: /Subscribe/ })).toBeNull();
  });

  it('opens the simulated checkout after choosing a period', async () => {
    const user = userEvent.setup();
    let sent: StartCheckoutCommand | undefined;
    server.use(
      getStartCheckoutMockHandler(async ({ request }) => {
        sent = (await request.json()) as StartCheckoutCommand;
        return checkoutResult();
      }),
    );
    await openSubscription();

    await user.click(within(await baseCard()).getByRole('button', { name: 'Subscribe for a term' }));

    expect(await screen.findByRole('heading', { name: 'Paymob checkout (simulation)' })).toBeInTheDocument();
    expect(sent).toEqual({ plan: 'Base', period: 'Termly' });
  });

  it('redirects to Paymob when checkout returns an external URL', async () => {
    const user = userEvent.setup();
    server.use(getStartCheckoutMockHandler((): CheckoutResult => checkoutResult(paymobUrl)));
    await openSubscription();

    await user.click(within(await baseCard()).getByRole('button', { name: 'Subscribe monthly' }));

    await waitFor(() => {
      expect(redirectToExternal).toHaveBeenCalledWith(paymobUrl);
    });
  });

  it('shows an error toast when checkout fails', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/subscriptions/checkout', () =>
        HttpResponse.json({ code: 'PAYMENT_GATEWAY_UNAVAILABLE' }, { status: 503 }),
      ),
    );
    await openSubscription();

    await user.click(within(await baseCard()).getByRole('button', { name: 'Subscribe monthly' }));

    expect(await screen.findByText('The payment service is unavailable. Try again in a moment.')).toBeInTheDocument();
    await waitFor(() => {
      expect(
        within(screen.getByRole('article', { name: 'Base' })).getByRole('button', { name: 'Subscribe monthly' }),
      ).toBeEnabled();
    });
  });
});
