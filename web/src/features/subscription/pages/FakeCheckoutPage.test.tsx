import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import type { PaymentResult, PaymentStatus } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import {
  getCompleteFakePaymentMockHandler,
  getGetMyEntitlementMockHandler,
  getGetMyPaymentMockHandler,
  getGetMyPaymentsMockHandler,
} from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { checkoutPaymentId, freeEntitlement, payment, paymentsPage, planCatalogue } from '@/test/subscriptionFixtures';

const normalizeSpaces = (text: string) => text.replace(/\s+/gu, ' ');

const checkoutPayment = (status: PaymentStatus): PaymentResult =>
  payment({ id: checkoutPaymentId, status, completedAt: status === 'Pending' ? null : '2026-09-29T12:00:00Z' });

interface OpenOptions {
  status?: PaymentStatus;
  lng?: Language;
  failFirst?: boolean;
  slow?: boolean;
}

async function openFakeCheckout({ status = 'Pending', lng = 'en', failFirst = false, slow = false }: OpenOptions = {}) {
  let current = checkoutPayment(status);
  server.use(
    getGetMyPaymentMockHandler(async () => {
      if (slow) {
        await delay(300);
      }
      return current;
    }),
    getCompleteFakePaymentMockHandler(async ({ request }) => {
      const { succeeded } = (await request.json()) as { succeeded: boolean };
      current = checkoutPayment(succeeded ? 'Succeeded' : 'Failed');
      return current;
    }),
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMyEntitlementMockHandler(freeEntitlement()),
    getGetMyPaymentsMockHandler(paymentsPage([])),
    getGetServableQuestionCountMockHandler({ count: 1234 }),
  );
  if (failFirst) {
    server.use(
      http.get(
        '*/api/subscriptions/payments/:paymentId',
        () => HttpResponse.json({ code: 'PAYMENT_NOT_FOUND' }, { status: 404 }),
        { once: true },
      ),
    );
  }
  const rendered = renderApp(`/student/fake-checkout/${checkoutPaymentId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/fake-checkout/$paymentId']);
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/checkout-result/$paymentId']);
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/subscription']);
  return rendered;
}

const checkoutHeading = () => screen.findByRole('heading', { name: 'Paymob checkout (simulation)' });

describe('FakeCheckoutPage', () => {
  it('shows loading then the simulated checkout with plan and amount', async () => {
    await openFakeCheckout({ slow: true });

    expect(await screen.findByRole('status', { name: 'Loading payment…' })).toBeInTheDocument();
    await checkoutHeading();
    expect(
      screen.getByText('Plan: Base · Monthly — Amount: EGP 199', { normalizer: normalizeSpaces }),
    ).toBeInTheDocument();
  });

  it('shows the success result after a successful simulated payment', async () => {
    const user = userEvent.setup();
    await openFakeCheckout();

    await user.click(await screen.findByRole('button', { name: 'Payment succeeds' }));

    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
    expect(screen.getByText('Your Base plan is active.')).toBeInTheDocument();
  });

  it('shows the failure result after a failed simulated payment', async () => {
    const user = userEvent.setup();
    await openFakeCheckout();

    await user.click(await screen.findByRole('button', { name: 'Payment fails' }));

    expect(await screen.findByRole('heading', { name: 'Payment failed' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Try again' })).toHaveAttribute('href', '/student/subscription');
  });

  it('returns to the subscription page on cancel', async () => {
    const user = userEvent.setup();
    await openFakeCheckout();

    await user.click(await screen.findByRole('link', { name: 'Cancel' }));

    expect(await screen.findByRole('heading', { name: 'Subscribe to Elmanhg' })).toBeInTheDocument();
  });

  it('goes to the result when the payment is no longer pending', async () => {
    await openFakeCheckout({ status: 'Succeeded' });

    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
  });

  it('shows an error with retry when the payment cannot be loaded', async () => {
    const user = userEvent.setup();
    await openFakeCheckout({ failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the payment.')).toBeInTheDocument();
    expect(within(alert).getByText('Payment not found.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await checkoutHeading()).toBeInTheDocument();
  });

  it('shows an error toast when simulated completion is refused', async () => {
    const user = userEvent.setup();
    await openFakeCheckout();
    server.use(
      http.post('*/api/subscriptions/payments/:paymentId/fake-completion', () =>
        HttpResponse.json({ code: 'FAKE_CHECKOUT_UNAVAILABLE' }, { status: 404 }),
      ),
    );

    await user.click(await screen.findByRole('button', { name: 'Payment succeeds' }));

    expect(await screen.findByText('Simulated checkout is not available.')).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    await openFakeCheckout({ lng: 'ar' });

    expect(await screen.findByRole('heading', { name: 'Paymob checkout (محاكاة)' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openFakeCheckout();

    await checkoutHeading();

    expect((await axe(container)).violations).toEqual([]);
  });
});
