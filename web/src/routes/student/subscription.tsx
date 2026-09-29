import { createFileRoute } from '@tanstack/react-router';
import { SubscriptionPage, subscriptionSearchSchema } from '@/features/subscription';

export const Route = createFileRoute('/student/subscription')({
  validateSearch: subscriptionSearchSchema,
  component: SubscriptionPage,
});
