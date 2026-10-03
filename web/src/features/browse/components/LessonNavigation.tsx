import { Link } from '@tanstack/react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { StudentLessonResult } from '@/shared/api/generated/model';

export interface LessonNavigationProps {
  lesson: StudentLessonResult;
}

const linkClassName =
  'inline-flex min-h-11 items-center gap-1 rounded-sm text-ui text-accent wrap-anywhere focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export function LessonNavigation({ lesson }: LessonNavigationProps) {
  const { t } = useTranslation('browse');
  const { previousLesson, nextLesson } = lesson;

  return (
    <nav aria-label={t('lesson.navLabel')} className="flex flex-col gap-2 md:flex-row md:justify-between">
      {previousLesson ? (
        <Link to="/student/lesson/$lessonId" params={{ lessonId: previousLesson.id }} className={linkClassName}>
          <ChevronLeft aria-hidden className="size-4 shrink-0 rtl:rotate-180" />
          {t('lesson.previous', { name: previousLesson.name })}
        </Link>
      ) : null}
      {nextLesson ? (
        <Link to="/student/lesson/$lessonId" params={{ lessonId: nextLesson.id }} className={linkClassName}>
          {t('lesson.next', { name: nextLesson.name })}
          <ChevronRight aria-hidden className="size-4 shrink-0 rtl:rotate-180" />
        </Link>
      ) : (
        <Link to="/student/unit/$unitId" params={{ unitId: lesson.unitId }} className={linkClassName}>
          {t('lesson.backToUnit', { name: lesson.unitName })}
          <ChevronRight aria-hidden className="size-4 shrink-0 rtl:rotate-180" />
        </Link>
      )}
    </nav>
  );
}
