import { createFileRoute } from '@tanstack/react-router';
import { MorePage } from '@/features/shell';

export const Route = createFileRoute('/student/more')({
  component: () => <MorePage role="student" />,
});
