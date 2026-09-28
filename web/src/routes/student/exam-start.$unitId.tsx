import { createFileRoute } from '@tanstack/react-router';
import { ExamStartPage } from '@/features/exam';

export const Route = createFileRoute('/student/exam-start/$unitId')({
  component: ExamStartRoute,
});

function ExamStartRoute() {
  const { unitId } = Route.useParams();
  return <ExamStartPage unitId={unitId} />;
}
