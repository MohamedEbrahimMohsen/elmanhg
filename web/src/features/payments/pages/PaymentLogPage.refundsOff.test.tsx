import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import {
  getGetPaymentLogMockHandler,
  getGetPaymentSettingsMockHandler,
} from '@/shared/api/generated/payments/payments.msw';
import type { Language } from '@/app/i18n';
import { server } from '@/test/msw/server';
import { adminPayment, paymentLogPage, paymentSettings } from '@/test/paymentFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

function serveLog(current: () => AdminPaymentResult) {
  server.use(
    getGetPaymentLogMockHandler(({ request }) =>
      new URL(request.url).searchParams.get('pageSize') === '1' ? paymentLogPage([]) : paymentLogPage([current()]),
    ),
  );
}

async function openPayments(lng: Language = 'en') {
  const rendered = renderApp('/admin/payments', { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/payments']);
  return rendered;
}

describe('PaymentLogPage refunds off', () => {
  it('disables the refund button and explains why when refunds are off', async () => {
    serveLog(() => adminPayment());
    server.use(getGetPaymentSettingsMockHandler(paymentSettings({ refundsEnabled: false })));
    await openPayments();

    const note = await screen.findByRole('note');
    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    const refund = within(row).getByRole('button', { name: 'Refund' });

    expect(note).toHaveTextContent('Refunds are turned off');
    expect(refund).toBeDisabled();
    expect(refund).toHaveAccessibleDescription(/Refunds are turned off/);
  });

  it('enables the refund button with no notice when refunds are on', async () => {
    serveLog(() => adminPayment());
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });

    await waitFor(() => {
      expect(within(row).getByRole('button', { name: 'Refund' })).toBeEnabled();
    });
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows the refunds-off error in the dialog and then the notice when the server refuses', async () => {
    let refundsEnabled = true;
    serveLog(() => adminPayment());
    server.use(
      getGetPaymentSettingsMockHandler(() => paymentSettings({ refundsEnabled })),
      http.post('*/api/payments/:paymentId/refund', () => {
        refundsEnabled = false;
        return HttpResponse.json({ code: 'PAYMENT_REFUNDS_DISABLED' }, { status: 400 });
      }),
    );
    const user = userEvent.setup();
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    await waitFor(() => {
      expect(within(row).getByRole('button', { name: 'Refund' })).toBeEnabled();
    });
    await user.click(within(row).getByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Refund reason'), 'Duplicate charge');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm refund' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Refunds are turned off. An admin can turn them on in Configuration.',
    );
    await user.click(within(dialog).getByRole('button', { name: 'Back' }));
    expect(await screen.findByRole('note')).toHaveTextContent('Refunds are turned off');
    expect(
      within(screen.getByRole('row', { name: /Mona Ali/ })).getByRole('button', { name: 'Refund' }),
    ).toBeDisabled();
  });

  it('keeps the refund button disabled with no notice while the setting is loading', async () => {
    serveLog(() => adminPayment());
    server.use(
      http.get('*/api/payments/settings', async () => {
        await delay('infinite');
        return HttpResponse.json(paymentSettings());
      }),
    );
    await openPayments();

    const row = await screen.findByRole('row', { name: /Mona Ali/ });

    expect(within(row).getByRole('button', { name: 'Refund' })).toBeDisabled();
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('keeps the refund button disabled and offers a retry when the setting fails to load', async () => {
    serveLog(() => adminPayment());
    server.use(
      http.get('*/api/payments/settings', () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 })),
    );
    const user = userEvent.setup();
    await openPayments();

    const alert = await screen.findByRole('alert');
    const row = await screen.findByRole('row', { name: /Mona Ali/ });

    expect(alert).toHaveTextContent('Could not check whether refunds are turned on.');
    expect(within(row).getByRole('button', { name: 'Refund' })).toBeDisabled();
    expect(screen.queryByRole('note')).not.toBeInTheDocument();

    server.use(getGetPaymentSettingsMockHandler(paymentSettings({ refundsEnabled: true })));
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    await waitFor(() => {
      expect(within(row).getByRole('button', { name: 'Refund' })).toBeEnabled();
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders the refunds-off notice in Arabic', async () => {
    serveLog(() => adminPayment());
    server.use(getGetPaymentSettingsMockHandler(paymentSettings({ refundsEnabled: false })));
    await openPayments('ar');

    const note = await screen.findByRole('note');
    const row = await screen.findByRole('row', { name: /Mona Ali/ });

    expect(note).toHaveTextContent('الاسترداد متوقف');
    expect(within(row).getByRole('button', { name: 'استرداد' })).toBeDisabled();
  });
});
