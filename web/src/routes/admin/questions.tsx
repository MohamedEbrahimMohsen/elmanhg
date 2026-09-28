import { createFileRoute } from '@tanstack/react-router';
import { QuestionListPage, questionListSearchSchema } from '@/features/questions';

export const Route = createFileRoute('/admin/questions')({
  validateSearch: questionListSearchSchema,
  component: QuestionListPage,
});
