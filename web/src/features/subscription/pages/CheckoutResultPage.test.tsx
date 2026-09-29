import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Language } from '@/app/i18n';
import type { PaymentResult, PaymentStatus } from '@/shared/api/generated/model';
import { getGetMyPaymentMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { checkoutPaymentId, payment } from '@/test/subscriptionFixtures';
import { checkoutConfirmationTimeoutMs, checkoutPollIntervalMs } from '../api/checkoutPolling';

const checkoutPayment = (status: PaymentStatus): PaymentResult =>
  payment({ id: checkoutPaymentId, status, completedAt: status === 'Pending' ? null : '2026-09-29T12:00:00Z' });

interface OpenOptions {
  lng?: Language;
  failFirst?: boolean;
  slow?: boolean;
}

async function openResult(
  respond: () => PaymentStatus,
  { lng = 'en', failFirst = false, slow = false }: OpenOptions = {},
) {
  server.use(
    getGetMyPaymentMockHandler(async () => {
      if (slow) {
        await delay(300);
      }
      return checkoutPayment(respond());
    }),
  );
  if (failFirst) {
    server.use(
      http.get(
        '*/api/subscriptions/payments/:paymentId',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        { once: true },
      ),
    );
  }
  const rendered = renderApp(`/student/checkout-result/${checkoutPaymentId}`, { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/checkout-result/$paymentId']);
  return rendered;
}

async function waitOutConfirmationWindow() {
  for (let elapsed = 0; elapsed <= checkoutConfirmationTimeoutMs; elapsed += checkoutPollIntervalMs) {
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs);
  }
}

describe('CheckoutResultPage', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows loading then the success message for a succeeded payment', async () => {
    await openResult(() => 'Succeeded', { slow: true });

    expect(await screen.findByRole('status', { name: 'Loading payment…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Done' })).toHaveAttribute('href', '/student/subscription');
  });

  it('shows the failure message with try again for a failed payment', async () => {
    await openResult(() => 'Failed');

    expect(await screen.findByRole('heading', { name: 'Payment failed' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Try again' })).toBeInTheDocument();
  });

  it('shows the refunded state for a refunded payment', async () => {
    await openResult(() => 'Refunded');

    expect(await screen.findByRole('heading', { name: 'This payment was refunded' })).toBeInTheDocument();
    expect(screen.getByText('The amount was returned to you.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Done' })).toHaveAttribute('href', '/student/subscription');
  });

  it('polls a pending payment until it succeeds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let status: PaymentStatus = 'Pending';
    let requests = 0;
    await openResult(() => {
      requests += 1;
      return status;
    });

    expect(await screen.findByRole('heading', { name: 'Confirming your payment…' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Confirming your payment…');
    status = 'Succeeded';
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs);

    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
    const requestsAtSuccess = requests;
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs * 5);
    expect(requests).toBe(requestsAtSuccess);
  });

  it('shows the still-confirming message after the confirmation window', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let requests = 0;
    await openResult(() => {
      requests += 1;
      return 'Pending';
    });
    await screen.findByRole('heading', { name: 'Confirming your payment…' });

    await waitOutConfirmationWindow();

    expect(await screen.findByRole('heading', { name: 'Confirmation has not arrived yet' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check again' })).toBeInTheDocument();
    const requestsAtTimeout = requests;
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs * 5);
    expect(requests).toBe(requestsAtTimeout);
  });

  it('check again shows the result once confirmed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    let status: PaymentStatus = 'Pending';
    await openResult(() => status);
    await screen.findByRole('heading', { name: 'Confirming your payment…' });
    await waitOutConfirmationWindow();
    const checkAgain = await screen.findByRole('button', { name: 'Check again' });
    status = 'Succeeded';

    await user.click(checkAgain);

    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
  });

  it('check again starts a new polling window', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    let requests = 0;
    await openResult(() => {
      requests += 1;
      return 'Pending';
    });
    await screen.findByRole('heading', { name: 'Confirming your payment…' });
    await waitOutConfirmationWindow();

    await user.click(await screen.findByRole('button', { name: 'Check again' }));

    expect(await screen.findByRole('heading', { name: 'Confirming your payment…' })).toBeInTheDocument();
    const requestsAfterClick = requests;
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs * 5);
    expect(requests).toBeGreaterThanOrEqual(requestsAfterClick + 5);
    await waitOutConfirmationWindow();
    expect(await screen.findByRole('heading', { name: 'Confirmation has not arrived yet' })).toBeInTheDocument();
    const requestsAtSecondTimeout = requests;
    await vi.advanceTimersByTimeAsync(checkoutPollIntervalMs * 5);
    expect(requests).toBe(requestsAtSecondTimeout);
  });

  it('shows an error with retry when the payment cannot be loaded', async () => {
    const user = userEvent.setup();
    await openResult(() => 'Succeeded', { failFirst: true });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load the payment.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Payment successful' })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    await openResult(() => 'Succeeded', { lng: 'ar' });

    expect(await screen.findByRole('heading', { name: 'تم الدفع بنجاح' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = await openResult(() => 'Pending');

    await screen.findByRole('heading', { name: 'Confirming your payment…' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
