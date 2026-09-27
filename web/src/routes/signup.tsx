import { createFileRoute } from '@tanstack/react-router';
import { redirectSignedIn, SignUpPage } from '@/features/session';

export const Route = createFileRoute('/signup')({
  beforeLoad: ({ context }) => {
    redirectSignedIn(context.sessionStore.get(), undefined);
  },
  component: SignUpPage,
});
