import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { WeakLessonResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface WeakLessonListProps {
  lessons: WeakLessonResult[];
}

export function WeakLessonList({ lessons }: WeakLessonListProps) {
  const { t } = useTranslation('progress');

  return (
    <ul className="flex flex-col gap-2">
      {lessons.map((lesson) => {
        const percent = Number(lesson.masteryPercent);
        return (
          <li
            key={lesson.lessonId}
            className="flex flex-col gap-1.5 rounded-md border border-border bg-surface px-3.5 py-3"
          >
            <p className="text-ui font-semibold">{lesson.lessonName}</p>
            <p className="text-caption text-text-muted">{lesson.subjectName}</p>
            <p className="text-caption text-text-muted">{t('weakSpots.mastery', { percent })}</p>
            <MasteryBar percent={percent} label={t('weakSpots.barLabel', { name: lesson.lessonName })} />
            <div>
              <Button asChild size="sm" variant="secondary">
                <Link to="/student/lesson/$lessonId/practice" params={{ lessonId: lesson.lessonId }}>
                  {t('weakSpots.train')}
                </Link>
              </Button>
            </div>
          </li>
        );
      })}
    </ul>
  );
}
