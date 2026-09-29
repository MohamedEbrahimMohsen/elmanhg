import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { PageDataOfPaymentResult } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import {
  getGetMyEntitlementMockHandler,
  getGetMyPaymentsMockHandler,
} from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { baseEntitlement, payment, paymentsPage, planCatalogue } from '@/test/subscriptionFixtures';

const pageTwoPaymentId = 'd4d4d4d4-d4d4-4d4d-8d4d-d4d4d4d4d4d4';
const failedPaymentId = 'e5e5e5e5-e5e5-4e5e-8e5e-e5e5e5e5e5e5';

const normalizeSpaces = (text: string) => text.replace(/\s+/gu, ' ');

async function openPayments(respond: (request: Request) => PageDataOfPaymentResult, failFirst = false) {
  server.use(
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMyEntitlementMockHandler(baseEntitlement()),
    getGetServableQuestionCountMockHandler({ count: 1234 }),
    getGetMyPaymentsMockHandler(({ request }) => respond(request)),
  );
  if (failFirst) {
    server.use(
      http.get(
        '*/api/subscriptions/payments',
        () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
  }
  const rendered = renderApp('/student/subscription', { session: testSessions.student });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/subscription']);
  return rendered;
}

const paymentsTable = () => screen.findByRole('table', { name: 'Your payments' });

describe('SubscriptionPage payments', () => {
  it('hides the payments section when there are no payments', async () => {
    await openPayments(() => paymentsPage([]));

    await screen.findByRole('article', { name: 'Base' });

    await expect.poll(() => screen.queryByRole('status', { name: 'Loading payments…' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Payments' })).toBeNull();
    expect(screen.queryByRole('table', { name: 'Your payments' })).toBeNull();
  });

  it('lists payments with date, plan, amount and status', async () => {
    await openPayments(() =>
      paymentsPage([
        payment(),
        payment({ id: failedPaymentId, status: 'Failed', amount: { amountMinor: 69900, currency: 'EGP' } }),
      ]),
    );

    const table = await paymentsTable();
    const succeededRow = within(table).getByRole('row', { name: /Successful/ });
    const failedRow = within(table).getByRole('row', { name: /Failed/ });

    expect(screen.getByRole('heading', { level: 2, name: 'Payments' })).toBeInTheDocument();
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(within(succeededRow).getByText('Sep 29, 2026')).toBeInTheDocument();
    expect(within(succeededRow).getByText('Base')).toBeInTheDocument();
    expect(within(succeededRow).getByText('EGP 199', { normalizer: normalizeSpaces })).toBeInTheDocument();
    expect(within(failedRow).getByText('EGP 699', { normalizer: normalizeSpaces })).toBeInTheDocument();
  });

  it('pages through payments', async () => {
    const user = userEvent.setup();
    const { router } = await openPayments((request) =>
      new URL(request.url).searchParams.get('pageNumber') === '2'
        ? paymentsPage([payment({ id: pageTwoPaymentId, plan: 'AskTeacher' })], { pageNumber: 2, totalPages: 2 })
        : paymentsPage([payment()], { totalPages: 2 }),
    );
    await paymentsTable();

    await user.click(screen.getByRole('button', { name: 'Next page' }));

    await expect.poll(() => router.state.location.search).toMatchObject({ paymentsPage: 2 });
    expect(await within(await paymentsTable()).findByText('Ask a Teacher')).toBeInTheDocument();
  });

  it('shows an error with retry when payments fail', async () => {
    const user = userEvent.setup();
    await openPayments(() => paymentsPage([payment()]), true);

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load payments.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(within(await paymentsTable()).getByText('Successful')).toBeInTheDocument();
  });
});
