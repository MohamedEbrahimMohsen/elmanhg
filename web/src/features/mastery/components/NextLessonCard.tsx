import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { NextLessonResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { MasteryBar } from './MasteryBar';

export interface NextLessonCardProps {
  lesson: NextLessonResult;
}

export function NextLessonCard({ lesson }: NextLessonCardProps) {
  const { t } = useTranslation('mastery');
  const percent = Number(lesson.masteryPercent);

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <p className="text-caption text-text-muted">{t('nextLesson.label')}</p>
      <p className="text-ui font-semibold">{lesson.lessonName}</p>
      <p className="text-caption text-text-muted">{lesson.subjectName}</p>
      <p className="text-caption text-text-muted">{t('nextLesson.mastery', { percent })}</p>
      <MasteryBar percent={percent} label={t('nextLesson.barLabel', { name: lesson.lessonName })} />
      <div>
        <Button asChild>
          <Link to="/student/lesson/$lessonId/practice" params={{ lessonId: lesson.lessonId }}>
            {t('nextLesson.train')}
          </Link>
        </Button>
      </div>
    </div>
  );
}
