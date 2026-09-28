import { useTranslation } from 'react-i18next';
import { useGetLessons } from '@/shared/api/generated/lessons/lessons';
import { useLessonCreate } from '../hooks/useLessonCreate';
import { ContentEmptyState } from './ContentEmptyState';
import { ContentErrorState } from './ContentErrorState';
import { ContentListSkeleton } from './ContentListSkeleton';
import { LessonItem } from './LessonItem';
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
        {data.map((lesson, index) => (
          <LessonItem
            key={lesson.id}
            lesson={lesson}
            isFirst={index === 0}
            isLast={index === data.length - 1}
            position={index + 1}
          />
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
