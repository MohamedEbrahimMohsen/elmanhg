import { createFileRoute } from '@tanstack/react-router';
import { ConfigurationPage } from '@/features/configuration';

export const Route = createFileRoute('/admin/configuration')({
  component: ConfigurationPage,
});
