import { createFileRoute } from '@tanstack/react-router';
import { CheckoutResultPage } from '@/features/subscription';

export const Route = createFileRoute('/student/checkout-result/$paymentId')({
  component: CheckoutResultRoute,
});

function CheckoutResultRoute() {
  const { paymentId } = Route.useParams();
  return <CheckoutResultPage paymentId={paymentId} />;
}
