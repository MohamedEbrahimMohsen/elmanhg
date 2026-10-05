import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { PageDataOfAdminPaymentResult } from '@/shared/api/generated/model';
import { getGetPaymentLogMockHandler } from '@/shared/api/generated/payments/payments.msw';
import { axe } from '@/test/axe';
import { mintButtons } from '@/test/mintButtons';
import { server } from '@/test/msw/server';
import { adminPayment, adminPaymentStudentId, paymentLogPage } from '@/test/paymentFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const searchParam = (request: Request, key: string) => new URL(request.url).searchParams.get(key);

const isReviewCount = (request: Request) => searchParam(request, 'pageSize') === '1';

const normalizeSpaces = (text: string) => text.replace(/\s+/gu, ' ');

function respondWith(respond: (request: Request) => PageDataOfAdminPaymentResult, reviewCount = 0) {
  server.use(
    getGetPaymentLogMockHandler(({ request }) =>
      isReviewCount(request) ? { ...paymentLogPage([]), totalItems: reviewCount } : respond(request),
    ),
  );
}

async function openPayments(path = '/admin/payments', lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp(path, { session: testSessions.admin, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/admin/payments']);
  return rendered;
}

describe('PaymentLogPage', () => {
  it('shows payments after loading', async () => {
    respondWith(() => paymentLogPage([adminPayment()]));
    await openPayments();

    expect(await screen.findByRole('status', { name: 'Loading payments…' })).toBeInTheDocument();
    const row = await screen.findByRole('row', { name: /Mona Ali/ });
    expect(within(row).getByText(/EGP\s?199/u, { normalizer: normalizeSpaces })).toBeInTheDocument();
    expect(within(row).getByText('Successful')).toBeInTheDocument();
  });

  it('shows the empty state when there are no payments', async () => {
    respondWith(() => paymentLogPage([]));
    await openPayments();

    expect(await screen.findByText('No payments yet.')).toBeInTheDocument();
  });

  it('offers clear filters when filters match nothing', async () => {
    respondWith((request) => paymentLogPage(searchParam(request, 'status') === null ? [adminPayment()] : []));
    const user = userEvent.setup();
    const { router } = await openPayments('/admin/payments?status=Failed');

    expect(await screen.findByText('No payments match these filters.')).toBeInTheDocument();
    expect(mintButtons()).toHaveLength(1);
    const emptyStateClear = screen.getAllByRole('button', { name: 'Clear filters' }).at(-1);
    if (!emptyStateClear) {
      throw new Error('The empty state does not offer Clear filters.');
    }
    await user.click(emptyStateClear);

    expect(await screen.findByRole('row', { name: /Mona Ali/ })).toBeInTheDocument();
    expect(router.state.location.search).not.toHaveProperty('status');
  });

  it('shows an error and recovers on retry', async () => {
    server.use(
      http.get('*/api/payments', ({ request }) =>
        isReviewCount(request)
          ? HttpResponse.json(paymentLogPage([]))
          : HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
      ),
    );
    const user = userEvent.setup();
    await openPayments();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load payments.');
    respondWith(() => paymentLogPage([adminPayment()]));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('row', { name: /Mona Ali/ })).toBeInTheDocument();
  });

  it('applies status, plan and reference filters to the request', async () => {
    respondWith((request) =>
      paymentLogPage([
        adminPayment({
          studentName: [searchParam(request, 'status'), searchParam(request, 'plan'), searchParam(request, 'reference')]
            .map((value) => value ?? 'none')
            .join('/'),
        }),
      ]),
    );
    const user = userEvent.setup();
    const { router } = await openPayments();

    await screen.findByRole('row', { name: /none\/none\/none/ });
    await user.selectOptions(screen.getByLabelText('Status'), 'Refunded');
    await user.selectOptions(screen.getByLabelText('Plan'), 'AskTeacher');
    await user.type(screen.getByLabelText('Reference'), '192036465');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(await screen.findByRole('row', { name: /Refunded\/AskTeacher\/192036465/ })).toBeInTheDocument();
    expect(router.state.location.search).toEqual({
      page: 1,
      status: 'Refunded',
      plan: 'AskTeacher',
      reference: '192036465',
    });
  });

  it('shows the review queue with its count', async () => {
    respondWith(
      (request) =>
        paymentLogPage(
          searchParam(request, 'needsReview') === 'true'
            ? [
                adminPayment({
                  studentName: 'Flagged Student',
                  needsReview: true,
                  reviewReason: 'AskTeacherWithoutBase',
                }),
              ]
            : [adminPayment()],
        ),
      2,
    );
    const user = userEvent.setup();
    await openPayments();

    await screen.findByRole('row', { name: /Mona Ali/ });
    const reviewTab = await screen.findByRole('button', { name: /^Needs review\s*2$/u });
    await user.click(reviewTab);

    const row = await screen.findByRole('row', { name: /Flagged Student/ });
    expect(reviewTab).toHaveAttribute('aria-pressed', 'true');
    expect(within(row).getByText('Needs review')).toBeInTheDocument();
    expect(within(row).getByText('Ask a Teacher paid without Base')).toBeInTheDocument();
  });

  it('shows the review empty state when nothing needs review', async () => {
    respondWith(() => paymentLogPage([]));
    await openPayments('/admin/payments?view=review');

    expect(await screen.findByText('No payments need review.')).toBeInTheDocument();
  });

  it('filters by a student from the row', async () => {
    respondWith((request) =>
      paymentLogPage([adminPayment({ studentName: searchParam(request, 'studentId') ? 'Filtered Mona' : 'Mona Ali' })]),
    );
    const user = userEvent.setup();
    const { router } = await openPayments();

    await user.click(await screen.findByRole('button', { name: "Show Mona Ali's payments" }));

    expect(await screen.findByRole('row', { name: /Filtered Mona/ })).toBeInTheDocument();
    expect(screen.getByText("Showing one student's payments.")).toBeInTheDocument();
    expect(router.state.location.search).toMatchObject({ studentId: adminPaymentStudentId });
  });

  it('moves to the next page', async () => {
    respondWith((request) => {
      const pageNumber = Number(searchParam(request, 'pageNumber') ?? '1');
      return paymentLogPage(
        [adminPayment({ id: `p${String(pageNumber)}`, studentName: `Page ${String(pageNumber)} Student` })],
        pageNumber,
        2,
      );
    });
    const user = userEvent.setup();
    await openPayments();

    expect(await screen.findByText('Page 1 of 2')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(await screen.findByRole('row', { name: /Page 2 Student/ })).toBeInTheDocument();
  });

  it('shows an inline error when the end date is before the start date', async () => {
    const requested: string[] = [];
    respondWith((request) => {
      requested.push(request.url);
      return paymentLogPage([adminPayment()]);
    });
    const user = userEvent.setup();
    const { router } = await openPayments();

    await screen.findByRole('row', { name: /Mona Ali/ });
    await user.type(screen.getByLabelText('From'), '2026-09-10');
    await user.type(screen.getByLabelText('To'), '2026-09-01');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(await screen.findByText('The end date is before the start date.')).toBeInTheDocument();
    expect(router.state.location.search).toEqual({});
    expect(requested.some((url) => url.includes('from='))).toBe(false);
  });

  it('renders right-to-left in Arabic', async () => {
    respondWith(() => paymentLogPage([adminPayment()]));
    await openPayments('/admin/payments', 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'المدفوعات' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    respondWith(
      () =>
        paymentLogPage([
          adminPayment({ needsReview: true, reviewReason: 'PartialRefundAtProvider' }),
          adminPayment({
            id: 'refunded',
            status: 'Refunded',
            canRefund: false,
            refundedAt: '2026-09-30T09:00:00Z',
            refundReason: 'Duplicate charge',
          }),
        ]),
      1,
    );
    const { container } = await openPayments();

    await screen.findByRole('table');

    expect((await axe(container)).violations).toEqual([]);
  });
});
