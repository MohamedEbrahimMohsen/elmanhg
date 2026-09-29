import { useTranslation } from 'react-i18next';
import { useGetStudentLesson } from '@/shared/api/generated/browse/browse';

export interface LessonObjectivesTabProps {
  lessonId: string;
}

export function LessonObjectivesTab({ lessonId }: LessonObjectivesTabProps) {
  const { t } = useTranslation('browse');
  const { data } = useGetStudentLesson(lessonId);
  if (!data) {
    return null;
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      {data.objectives.length === 0 ? (
        <p className="text-ui text-text-muted">{t('lesson.noObjectives')}</p>
      ) : (
        <ol className="flex list-decimal flex-col gap-1 ps-6 text-ui break-words">
          {data.objectives.map((objective) => (
            <li key={objective.id}>{objective.text}</li>
          ))}
        </ol>
      )}
    </div>
  );
}
