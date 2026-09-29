import { createFileRoute } from '@tanstack/react-router';
import { FakeCheckoutPage } from '@/features/subscription';

export const Route = createFileRoute('/student/fake-checkout/$paymentId')({
  component: FakeCheckoutRoute,
});

function FakeCheckoutRoute() {
  const { paymentId } = Route.useParams();
  return <FakeCheckoutPage paymentId={paymentId} />;
}
