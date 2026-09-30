import { createFileRoute } from '@tanstack/react-router';
import { MorePage } from '@/features/shell';

export const Route = createFileRoute('/teacher/more')({
  component: () => <MorePage role="teacher" />,
});
