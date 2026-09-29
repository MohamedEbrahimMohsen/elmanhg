import { createFileRoute } from '@tanstack/react-router';
import { SubjectPage } from '@/features/browse';

export const Route = createFileRoute('/student/subject/$subjectId')({
  component: SubjectRoute,
});

function SubjectRoute() {
  const { subjectId } = Route.useParams();
  return <SubjectPage subjectId={subjectId} />;
}
