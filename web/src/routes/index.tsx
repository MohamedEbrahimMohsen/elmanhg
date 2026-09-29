import { createFileRoute } from '@tanstack/react-router';
import { LandingPage } from '@/features/landing';
import { redirectSignedIn } from '@/features/session';

export const Route = createFileRoute('/')({
  beforeLoad: ({ context }) => {
    redirectSignedIn(context.sessionStore.get(), undefined);
  },
  component: LandingPage,
});
