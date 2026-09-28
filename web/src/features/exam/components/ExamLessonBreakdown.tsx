import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ExamLessonResult } from '@/shared/api/generated/model';

export interface ExamLessonBreakdownProps {
  lessons: ExamLessonResult[];
}

const headerKeys = ['lesson', 'percentHeader'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function ExamLessonBreakdown({ lessons }: ExamLessonBreakdownProps) {
  const { t } = useTranslation('exam');

  if (lessons.length === 0) {
    return null;
  }
  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.byLesson')}</h2>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              {headerKeys.map((key) => (
                <th
                  key={key}
                  scope="col"
                  className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
                >
                  {t(`result.${key}`)}
                </th>
              ))}
              <th scope="col" className="px-2.5 py-2.25">
                <span className="sr-only">{t('result.train')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {lessons.map((lesson) => (
              <tr key={lesson.lessonId} className="border-t border-border">
                <td className={cellClassName}>{lesson.name ?? t('result.unknownLesson')}</td>
                <td className={cellClassName}>
                  {t('result.percent', { percent: Math.round(Number(lesson.scorePercent)) })}
                </td>
                <td className={cellClassName}>
                  <Link
                    to="/student/lesson/$lessonId/practice"
                    params={{ lessonId: lesson.lessonId }}
                    className="rounded-sm text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
                  >
                    {t('result.train')}
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
