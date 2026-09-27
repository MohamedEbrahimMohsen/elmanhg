import { createFileRoute } from '@tanstack/react-router';
import { DevSignInPage, loginSearchSchema, redirectSignedIn } from '@/features/session';

export const Route = createFileRoute('/login')({
  validateSearch: loginSearchSchema,
  beforeLoad: ({ context, search }) => {
    redirectSignedIn(context.sessionStore.get(), search.redirect);
  },
  component: DevSignInPage,
});
