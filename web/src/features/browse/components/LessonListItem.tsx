import { Link } from '@tanstack/react-router';
import { Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { StudentLessonSummaryResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface LessonListItemProps {
  lesson: StudentLessonSummaryResult;
}

export function LessonListItem({ lesson }: LessonListItemProps) {
  const { t } = useTranslation('browse');
  const percent = Number(lesson.masteryPercent);

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border bg-surface px-3.5 py-3 shadow-1">
      {lesson.isLocked ? (
        <>
          <span className="text-ui font-bold break-words text-text-muted">{lesson.name}</span>
          <span className="inline-flex items-center gap-1 self-start rounded-full bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
            <Lock aria-hidden className="size-4" />
            {t('unit.locked')}
          </span>
        </>
      ) : (
        <Link
          to="/student/lesson/$lessonId"
          params={{ lessonId: lesson.id }}
          className="rounded-sm text-ui font-bold break-words text-text hover:text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {lesson.name}
        </Link>
      )}
      <MasteryBar percent={percent} label={t('mastery.barLabel', { name: lesson.name })} />
      <p className="text-caption text-text-muted">
        {t('unit.lessonMeta', { percent, questions: Number(lesson.servableCount) })}
      </p>
      {lesson.isLocked ? (
        <Button asChild size="sm" variant="secondary" className="self-start">
          <Link to="/student/subscription">{t('unit.unlock')}</Link>
        </Button>
      ) : null}
    </li>
  );
}
