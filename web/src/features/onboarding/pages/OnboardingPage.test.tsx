import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { getGetMasteryOverviewMockHandler } from '@/shared/api/generated/mastery/mastery.msw';
import type { RecordFunnelEventRequest, SubjectInterestsResult } from '@/shared/api/generated/model';
import { getGetSubjectInterestsMockHandler } from '@/shared/api/generated/students/students.msw';
import { axe } from '@/test/axe';
import { chemistryId, masteryOverview, physicsId } from '@/test/masteryFixtures';
import { server } from '@/test/msw/server';
import { subjectInterests } from '@/test/onboardingFixtures';
import { renderApp } from '@/test/renderWithProviders';
import { onboardingStudent, testSessions } from '@/test/sessions';

const route = '*/api/students/me/subject-interests';

const openOnboarding = (interests: SubjectInterestsResult = subjectInterests(), lng: 'en' | 'ar' = 'en') => {
  server.use(getGetSubjectInterestsMockHandler(interests), getGetMasteryOverviewMockHandler(masteryOverview()));
  const session = interests.needsOnboarding ? onboardingStudent : testSessions.student;
  return renderApp('/onboarding', { session, lng });
};

const captureSaves = (): unknown[] => {
  const bodies: unknown[] = [];
  server.use(
    http.put(route, async ({ request }) => {
      bodies.push(await request.json());
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return bodies;
};

const captureFunnel = (): string[] => {
  const types: string[] = [];
  server.use(
    http.post('*/api/analytics/funnel-events', async ({ request }) => {
      types.push(((await request.json()) as RecordFunnelEventRequest).type);
      return new HttpResponse(null, { status: 200 });
    }),
  );
  return types;
};

describe('OnboardingPage', () => {
  it('shows a loading state then the subjects with the saved choice ticked', async () => {
    openOnboarding(
      subjectInterests({
        subjects: [
          { subjectId: physicsId, name: 'Physics', isSelected: false },
          { subjectId: chemistryId, name: 'Chemistry', isSelected: true },
        ],
      }),
    );

    expect(await screen.findByRole('status', { name: 'Loading subjects…' })).toBeInTheDocument();
    expect(await screen.findByRole('checkbox', { name: 'Chemistry' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Physics' })).not.toBeChecked();
  });

  it('requires one subject before continuing', async () => {
    const saves = captureSaves();
    const user = userEvent.setup();
    openOnboarding();

    await user.click(await screen.findByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose at least one subject.');
    expect(screen.getByRole('checkbox', { name: 'Physics' })).toHaveFocus();
    expect(saves).toEqual([]);
  });

  it('saves the chosen subjects and lands on the home', async () => {
    const saves = captureSaves();
    const user = userEvent.setup();
    openOnboarding();

    await user.click(await screen.findByRole('checkbox', { name: 'Physics' }));
    await user.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
    expect(saves).toEqual([{ subjectIds: [physicsId] }]);
  });

  it('skips with no subjects and lands on the home', async () => {
    const saves = captureSaves();
    const user = userEvent.setup();
    openOnboarding();

    await user.click(await screen.findByRole('button', { name: 'Skip for now' }));

    expect(await screen.findByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
    expect(saves).toEqual([{ subjectIds: [] }]);
  });

  it('records onboarding completed on first completion only', async () => {
    captureSaves();
    const types = captureFunnel();
    const user = userEvent.setup();
    const first = openOnboarding();

    await user.click(await screen.findByRole('button', { name: 'Skip for now' }));
    await screen.findByRole('heading', { name: 'Hello, أحمد' });
    await waitFor(() => {
      expect(types).toEqual(['OnboardingCompleted']);
    });
    first.unmount();
    openOnboarding(subjectInterests({ needsOnboarding: false }));
    await user.click(await screen.findByRole('checkbox', { name: 'Physics' }));
    await user.click(screen.getByRole('button', { name: 'Continue' }));
    await screen.findByRole('heading', { name: 'Hello, أحمد' });

    expect(types).toEqual(['OnboardingCompleted']);
  });

  it('shows the server error when saving fails', async () => {
    server.use(http.put(route, () => HttpResponse.json({ code: 'SUBJECT_NOT_FOUND', message: '' }, { status: 404 })));
    const user = userEvent.setup();
    openOnboarding();

    await user.click(await screen.findByRole('checkbox', { name: 'Physics' }));
    await user.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Subject not found.');
  });

  it('offers the way home instead of skipping when editing', async () => {
    openOnboarding(subjectInterests({ needsOnboarding: false }));

    expect(await screen.findByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/student');
    expect(screen.queryByRole('button', { name: 'Skip for now' })).toBeNull();
  });

  it('continues home when there are no subjects', async () => {
    const saves = captureSaves();
    const user = userEvent.setup();
    openOnboarding(subjectInterests({ subjects: [] }));

    expect(await screen.findByText('No subjects yet.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Continue to home' }));

    expect(await screen.findByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
    expect(saves).toEqual([{ subjectIds: [] }]);
  });

  it('shows an error and retries loading', async () => {
    server.use(getGetMasteryOverviewMockHandler(masteryOverview()));
    server.use(
      http.get(route, () => HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }), { once: true }),
      getGetSubjectInterestsMockHandler(subjectInterests()),
    );
    const user = userEvent.setup();
    renderApp('/onboarding', { session: onboardingStudent });

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not load the subjects.');
    await user.click(within(alert).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('checkbox', { name: 'Physics' })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    openOnboarding(subjectInterests(), 'ar');

    expect(await screen.findByRole('heading', { name: 'اختر موادك' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openOnboarding();

    await screen.findByRole('checkbox', { name: 'Physics' });

    expect((await axe(container)).violations).toEqual([]);
  });

  it('shows the app bar with the logo linking home', async () => {
    openOnboarding();

    const banner = await screen.findByRole('banner');

    expect(within(banner).getByRole('link', { name: 'Elmanhg' })).toHaveAttribute('href', '/');
  });
});
