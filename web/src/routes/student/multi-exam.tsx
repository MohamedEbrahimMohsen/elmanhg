import { createFileRoute } from '@tanstack/react-router';
import { MultiExamBuilderPage, multiExamSearchSchema } from '@/features/exam';

export const Route = createFileRoute('/student/multi-exam')({
  validateSearch: multiExamSearchSchema,
  component: MultiExamBuilderPage,
});
