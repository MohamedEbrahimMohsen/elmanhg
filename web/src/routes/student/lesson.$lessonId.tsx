import { createFileRoute } from '@tanstack/react-router';
import { LessonPage } from '@/features/browse';
import { getGetStudentLessonQueryOptions } from '@/shared/api/generated/browse/browse';

export const Route = createFileRoute('/student/lesson/$lessonId')({
  loader: ({ context, params, preload }) => {
    if (preload) {
      context.queryClient.query(getGetStudentLessonQueryOptions(params.lessonId)).catch(() => undefined);
    }
  },
  component: LessonRoute,
});

function LessonRoute() {
  const { lessonId } = Route.useParams();
  return <LessonPage lessonId={lessonId} />;
}
