import { createFileRoute } from '@tanstack/react-router';
import { GradeReviewQueuePage, gradeReviewSearchSchema } from '@/features/gradeReview';

export const Route = createFileRoute('/teacher/grades')({
  validateSearch: gradeReviewSearchSchema,
  component: GradeReviewQueuePage,
});
