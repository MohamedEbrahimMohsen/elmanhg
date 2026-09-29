import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Language } from '@/app/i18n';
import type { EntitlementResult } from '@/shared/api/generated/model';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import {
  getGetMyEntitlementMockHandler,
  getGetMyPaymentsMockHandler,
} from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';
import { baseEntitlement, freeEntitlement, paymentsPage, planCatalogue } from '@/test/subscriptionFixtures';

interface OpenOptions {
  entitlement?: EntitlementResult;
  lng?: Language;
  failing?: string;
}

const failOnce = (path: string) =>
  http.get(`*${path}`, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), { once: true });

async function openSubscription({ entitlement = freeEntitlement(), lng = 'en', failing }: OpenOptions = {}) {
  server.use(
    getGetPlanCatalogueMockHandler(planCatalogue()),
    getGetMyEntitlementMockHandler(entitlement),
    getGetMyPaymentsMockHandler(paymentsPage([])),
    getGetServableQuestionCountMockHandler({ count: 1234 }),
  );
  if (failing) {
    server.use(failOnce(failing));
  }
  const rendered = renderApp('/student/subscription', { session: testSessions.student, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/student/subscription']);
  return rendered;
}

describe('SubscriptionPage', () => {
  it('shows loading then the header, current plan and plan cards for a free student', async () => {
    await openSubscription();

    expect(await screen.findByRole('status', { name: 'Loading plans…' })).toBeInTheDocument();
    const base = await screen.findByRole('article', { name: 'Base' });
    expect(screen.getByRole('heading', { level: 1, name: 'Subscribe to Elmanhg' })).toBeInTheDocument();
    expect(
      await screen.findByText('1,234 questions approved by real teachers · Unlimited practice · Unit exams'),
    ).toBeInTheDocument();
    const current = screen.getByRole('region', { name: 'Your current plan' });
    expect(within(current).getByText('Free')).toBeInTheDocument();
    const free = screen.getByRole('article', { name: 'Free' });
    expect(within(free).getByText('10 practice questions a day')).toBeInTheDocument();
    expect(within(free).getByText('The first lesson of each unit')).toBeInTheDocument();
    expect(
      within(base).getByText('EGP 199/month', { normalizer: (text) => text.replace(/\s+/gu, ' ') }),
    ).toBeInTheDocument();
    expect(
      within(base).getByText('EGP 699/term', { normalizer: (text) => text.replace(/\s+/gu, ' ') }),
    ).toBeInTheDocument();
    expect(
      within(base).getByText('EGP 1,799/year', { normalizer: (text) => text.replace(/\s+/gu, ' ') }),
    ).toBeInTheDocument();
    const askTeacher = screen.getByRole('article', { name: 'Ask a Teacher' });
    expect(
      within(askTeacher).getByText('EGP 99/month', { normalizer: (text) => text.replace(/\s+/gu, ' ') }),
    ).toBeInTheDocument();
    expect(within(askTeacher).getByText('20 questions a month')).toBeInTheDocument();
    expect(within(askTeacher).getByText('Reply within 24 hours')).toBeInTheDocument();
    expect(screen.queryByText('Active')).toBeNull();
  });

  it('marks Base and Ask a Teacher active for a subscribed student', async () => {
    await openSubscription({ entitlement: baseEntitlement({ withAskTeacher: true }) });

    const base = await screen.findByRole('article', { name: 'Base' });

    expect(
      within(screen.getByRole('region', { name: 'Your current plan' })).getByText('Base + Ask a Teacher'),
    ).toBeInTheDocument();
    expect(within(base).getByText('Active')).toBeInTheDocument();
    expect(within(screen.getByRole('article', { name: 'Ask a Teacher' })).getByText('Active')).toBeInTheDocument();
    expect(within(screen.getByRole('article', { name: 'Free' })).queryByText('Active')).toBeNull();
    expect(screen.getByText('Base — active until Oct 29, 2026')).toBeInTheDocument();
  });

  it('shows the grace line for a past-due subscription', async () => {
    await openSubscription({ entitlement: baseEntitlement({ status: 'PastDue' }) });

    expect(await screen.findByText('Base — payment overdue, available until Nov 1, 2026')).toBeInTheDocument();
  });

  it('shows the cancelled line with the period end', async () => {
    await openSubscription({ entitlement: baseEntitlement({ status: 'Cancelled' }) });

    expect(await screen.findByText('Base — cancelled, available until Oct 29, 2026')).toBeInTheDocument();
  });

  it('shows the tagline without a count when the question count fails', async () => {
    await openSubscription({ failing: '/api/questions/servable-count' });

    expect(
      await screen.findByText('Questions approved by real teachers · Unlimited practice · Unit exams'),
    ).toBeInTheDocument();
    expect(await screen.findByRole('article', { name: 'Base' })).toBeInTheDocument();
  });

  it('shows an error with retry when plans fail to load', async () => {
    const user = userEvent.setup();
    await openSubscription({ failing: '/api/plans' });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Could not load plans.')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('article', { name: 'Base' })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    await openSubscription({ lng: 'ar' });

    expect(await screen.findByRole('heading', { level: 1, name: 'اشترك في المنهج' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(await screen.findByRole('article', { name: 'مجاني' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = await openSubscription({ entitlement: baseEntitlement({ withAskTeacher: true }) });

    await screen.findByRole('article', { name: 'Base' });
    await screen.findByText('1,234 questions approved by real teachers · Unlimited practice · Unit exams');

    expect((await axe(container)).violations).toEqual([]);
  });
});
