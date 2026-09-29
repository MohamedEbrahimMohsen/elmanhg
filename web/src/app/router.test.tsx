import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { getGetPlanCatalogueMockHandler } from '@/shared/api/generated/plans/plans.msw';
import { getGetServableQuestionCountMockHandler } from '@/shared/api/generated/questions/questions.msw';
import { getGetSubjectInterestsMockHandler } from '@/shared/api/generated/students/students.msw';
import { server } from '@/test/msw/server';
import { subjectInterests } from '@/test/onboardingFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { onboardingStudent, testSessions } from '@/test/sessions';
import { planCatalogue } from '@/test/subscriptionFixtures';

describe('createAppRouter', () => {
  it('shows the landing page to an anonymous visitor at /', async () => {
    server.use(
      getGetServableQuestionCountMockHandler({ count: 1234 }),
      getGetPlanCatalogueMockHandler(planCatalogue()),
    );
    renderApp('/');

    expect(await screen.findByRole('link', { name: 'Start for free' })).toBeInTheDocument();
  });

  it('sends a student who still needs onboarding to onboarding', async () => {
    server.use(getGetSubjectInterestsMockHandler(subjectInterests()));
    renderApp('/student', { session: onboardingStudent });

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
  });

  it('sends a signed-in admin at / to the admin home', async () => {
    renderApp('/', { session: testSessions.admin });

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });

  it('shows the not-found page with a link back for an unknown path', async () => {
    renderApp('/nope');

    expect(
      await screen.findByRole('heading', { name: 'This page does not exist or is not available.' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back' })).toHaveAttribute('href', '/');
  });
});
