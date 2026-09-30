import { createFileRoute } from '@tanstack/react-router';
import { TrainingExportPage, trainingExportSearchSchema } from '@/features/trainingExport';

export const Route = createFileRoute('/admin/export')({
  validateSearch: trainingExportSearchSchema,
  component: TrainingExportPage,
});
