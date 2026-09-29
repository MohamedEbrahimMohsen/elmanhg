import { createFileRoute } from '@tanstack/react-router';
import { PaymentLogPage, paymentLogSearchSchema } from '@/features/payments';

export const Route = createFileRoute('/admin/payments')({
  validateSearch: paymentLogSearchSchema,
  component: PaymentLogPage,
});
