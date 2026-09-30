import { createFileRoute } from '@tanstack/react-router';
import { DashboardPage, dashboardSearchSchema } from '@/features/dashboard';

export const Route = createFileRoute('/admin/')({
  validateSearch: dashboardSearchSchema,
  component: DashboardPage,
});
