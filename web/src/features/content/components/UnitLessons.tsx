import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetLessons } from '@/shared/api/generated/lessons/lessons';
import { useLessonCreate } from '../hooks/useLessonCreate';
import { ContentEmptyState } from './ContentEmptyState';
import { ContentErrorState } from './ContentErrorState';
import { ContentListSkeleton } from './ContentListSkeleton';
import { LessonStateBadge } from './LessonStateBadge';
import { NameForm } from './NameForm';

export interface UnitLessonsProps {
  subjectId: string;
  unitId: string;
  unitName: string;
  id: string;
}

const lessonNameErrorFields = { LESSON_NAME_REQUIRED: 'name', LESSON_NAME_TOO_LONG: 'name' } as const;

export function UnitLessons({ subjectId, unitId, unitName, id }: UnitLessonsProps) {
  const { t } = useTranslation('content');
  const { data, error, isPending, isError, refetch } = useGetLessons({ unitId });
  const create = useLessonCreate(subjectId);

  const renderLessons = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('lessons.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('lessons.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.length === 0) {
      return <ContentEmptyState message={t('lessons.empty')} />;
    }
    return (
      <ol aria-label={t('lessons.listLabel', { name: unitName })} className="flex flex-col gap-2">
        {data.map((lesson) => (
          <li key={lesson.id} className="flex flex-wrap items-center gap-2">
            <Link
              to="/admin/lesson/$lessonId"
              params={{ lessonId: lesson.id }}
              className="text-ui font-semibold text-accent underline"
            >
              {lesson.name}
            </Link>
            <LessonStateBadge state={lesson.state} />
          </li>
        ))}
      </ol>
    );
  };

  return (
    <div id={id} className="flex flex-col gap-3 border-t border-border pt-3">
      {renderLessons()}
      <NameForm
        label={t('lessons.nameLabel')}
        submitLabel={t('lessons.add')}
        serverErrorFields={lessonNameErrorFields}
        onSubmit={(name) => create(unitId, name)}
      />
    </div>
  );
}
