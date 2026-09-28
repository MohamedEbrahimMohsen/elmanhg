import { createFileRoute } from '@tanstack/react-router';
import { blueprintsSearchSchema, ExamBlueprintsPage } from '@/features/blueprints';

export const Route = createFileRoute('/admin/blueprints')({
  validateSearch: blueprintsSearchSchema,
  component: ExamBlueprintsPage,
});
