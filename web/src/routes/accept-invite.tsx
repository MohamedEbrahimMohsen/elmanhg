import { createFileRoute } from '@tanstack/react-router';
import { AcceptInvitePage, redirectSignedIn } from '@/features/session';

export const Route = createFileRoute('/accept-invite')({
  beforeLoad: ({ context }) => {
    redirectSignedIn(context.sessionStore.get(), undefined);
  },
  component: AcceptInvitePage,
});
