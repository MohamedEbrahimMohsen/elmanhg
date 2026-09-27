import { createFileRoute } from '@tanstack/react-router';
import { LoginPage, loginSearchSchema, redirectSignedIn } from '@/features/session';

export const Route = createFileRoute('/login')({
  validateSearch: loginSearchSchema,
  beforeLoad: ({ context, search }) => {
    redirectSignedIn(context.sessionStore.get(), search.redirect);
  },
  component: LoginPage,
});
