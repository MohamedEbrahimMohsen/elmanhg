import { createFileRoute } from '@tanstack/react-router';
import { OnboardingPage } from '@/features/onboarding';
import { requireRole } from '@/features/session';

export const Route = createFileRoute('/onboarding')({
  beforeLoad: ({ context, location }) => {
    requireRole(context.sessionStore.get(), 'student', location.href);
  },
  component: OnboardingPage,
});
