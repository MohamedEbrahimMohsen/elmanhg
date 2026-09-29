import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import {
  getGetPaymentLogMockHandler,
  getRefundPaymentMockHandler,
  getResolvePaymentReviewMockHandler,
} from '@/shared/api/generated/payments/payments.msw';
import { server } from '@/test/msw/server';
import { adminPayment, paymentLogPage } from '@/test/paymentFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/u;

function serveLog(current: () => AdminPaymentResult) {
  server.use(
    getGetPaymentLogMockHandler(({ request }) =>
      new URL(request.url).searchParams.get('pageSize') === '1' ? paymentLogPage([]) : paymentLogPage([current()]),
    ),
  );
}

async function openPayments() {
  const rendered = renderApp('/admin/payments', { session: testSessions.admin, lng: 'en' });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/payments']);
  return rendered;
}

describe('PaymentLogPage actions', () => {
  it('refunds a payment with a reason', async () => {
    let payment = adminPayment();
    const sent: { reason: unknown; key: string | null }[] = [];
    serveLog(() => payment);
    server.use(
      getRefundPaymentMockHandler(async ({ request }) => {
        const body = (await request.json()) as { reason?: unknown };
        sent.push({ reason: body.reason, key: request.headers.get('Idempotency-Key') });
        payment = adminPayment({
          status: 'Refunded',
          canRefund: false,
          refundedAt: '2026-09-30T09:00:00Z',
          refundReason: 'Duplicate charge',
        });
        return payment;
      }),
    );
    const user = userEvent.setup();
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    await user.click(within(row).getByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Refund reason'), 'Duplicate charge');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm refund' }));

    expect(await screen.findByText('Payment refunded.')).toBeInTheDocument();
    expect(sent).toHaveLength(1);
    expect(sent[0]?.reason).toBe('Duplicate charge');
    expect(sent[0]?.key).toMatch(uuidPattern);
    const refundedRow = await screen.findByRole('row', { name: /Refunded on/ });
    expect(within(refundedRow).getByText('Refunded')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('requires a reason before refunding', async () => {
    let requests = 0;
    serveLog(() => adminPayment());
    server.use(
      getRefundPaymentMockHandler(() => {
        requests += 1;
        return adminPayment({ status: 'Refunded' });
      }),
    );
    const user = userEvent.setup();
    await openPayments();

    await user.click(await screen.findByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm refund' }));

    expect(await within(dialog).findByText('Enter a refund reason.')).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('shows the Paymob decline inside the dialog', async () => {
    serveLog(() => adminPayment());
    server.use(
      http.post('*/api/payments/:paymentId/refund', () =>
        HttpResponse.json({ code: 'PAYMENT_REFUND_DECLINED' }, { status: 400 }),
      ),
    );
    const user = userEvent.setup();
    await openPayments();

    await user.click(await screen.findByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Refund reason'), 'Duplicate charge');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm refund' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Paymob declined the refund. Check the transaction in the Paymob dashboard.',
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('hides the refund action when the payment cannot be refunded', async () => {
    serveLog(() => adminPayment({ status: 'Failed', canRefund: false }));
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });

    expect(within(row).queryByRole('button', { name: 'Refund' })).not.toBeInTheDocument();
  });

  it('keeps a flagged payment after confirming', async () => {
    let payment = adminPayment({ needsReview: true, reviewReason: 'AskTeacherWithoutBase' });
    let resolved = 0;
    serveLog(() => payment);
    server.use(
      getResolvePaymentReviewMockHandler(() => {
        resolved += 1;
        payment = adminPayment({ reviewReason: 'AskTeacherWithoutBase', reviewResolvedAt: '2026-09-30T09:00:00Z' });
        return payment;
      }),
    );
    const user = userEvent.setup();
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    expect(within(row).getByText('Needs review')).toBeInTheDocument();
    await user.click(within(row).getByRole('button', { name: 'Keep payment' }));
    const dialog = await screen.findByRole('dialog', { name: 'Keep this payment?' });
    await user.click(within(dialog).getByRole('button', { name: 'Keep payment' }));

    expect(await screen.findByText('Review closed.')).toBeInTheDocument();
    expect(resolved).toBe(1);
    await waitFor(() => {
      expect(within(screen.getByRole('row', { name: /Mona Ali/ })).queryByText('Needs review')).not.toBeInTheDocument();
    });
  });
});
