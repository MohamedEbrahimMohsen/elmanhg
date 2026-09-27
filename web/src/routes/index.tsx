import { createFileRoute } from '@tanstack/react-router';
import { redirectToHome } from '@/features/session';

export const Route = createFileRoute('/')({
  beforeLoad: ({ context }) => {
    redirectToHome(context.sessionStore.get());
  },
});
