import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { StudentLessonSummaryResult } from '@/shared/api/generated/model';

export interface LessonListItemProps {
  lesson: StudentLessonSummaryResult;
}

export function LessonListItem({ lesson }: LessonListItemProps) {
  const { t } = useTranslation('browse');
  const percent = Number(lesson.masteryPercent);

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border bg-surface px-3.5 py-3 shadow-1">
      <Link
        to="/student/lesson/$lessonId"
        params={{ lessonId: lesson.id }}
        className="rounded-sm text-ui font-semibold break-words text-text hover:text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        {lesson.name}
      </Link>
      <MasteryBar percent={percent} label={t('mastery.barLabel', { name: lesson.name })} />
      <p className="text-caption text-text-muted">
        {t('unit.lessonMeta', { percent, questions: Number(lesson.servableCount) })}
      </p>
    </li>
  );
}
