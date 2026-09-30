import { createFileRoute } from '@tanstack/react-router';
import { GradeReviewDetailPage } from '@/features/gradeReview';

export const Route = createFileRoute('/teacher/grade/$subjectId/$kind/$gradeId')({
  component: GradeReviewDetailPage,
});
