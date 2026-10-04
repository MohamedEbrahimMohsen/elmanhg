import { useTranslation } from 'react-i18next';
import type { WeakLessonResult } from '@/shared/api/generated/model';
import { WeakSpotList } from './WeakSpotList';

export interface WeakLessonListProps {
  lessons: WeakLessonResult[];
}

export function WeakLessonList({ lessons }: WeakLessonListProps) {
  const { t } = useTranslation('progress');

  return (
    <WeakSpotList
      items={lessons.map((lesson) => ({
        id: lesson.lessonId,
        title: lesson.lessonName,
        meta: lesson.subjectName,
        percent: Number(lesson.masteryPercent),
        barLabel: t('weakSpots.barLabel', { name: lesson.lessonName }),
        lessonId: lesson.lessonId,
      }))}
    />
  );
}
