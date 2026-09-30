import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { WeakSpotsResult } from '@/shared/api/generated/model';

export interface StudentWeakSpotsProps {
  weakSpots: WeakSpotsResult;
}

export function StudentWeakSpots({ weakSpots }: StudentWeakSpotsProps) {
  const { t } = useTranslation('users');
  const headingId = useId();
  const empty = weakSpots.lessons.length === 0 && weakSpots.objectives.length === 0;

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('weakSpots.title')}
      </h2>
      {empty ? (
        <p className="text-ui text-text-muted">{t('weakSpots.empty')}</p>
      ) : (
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
            <h3 className="font-display text-h3 font-semibold">{t('weakSpots.lessons')}</h3>
            <ul className="flex flex-col gap-2">
              {weakSpots.lessons.map((lesson) => (
                <li key={lesson.lessonId} className="flex flex-col">
                  <span className="text-ui text-text">{lesson.lessonName}</span>
                  <span className="text-caption text-text-muted">
                    {t('weakSpots.lessonLine', { subject: lesson.subjectName, percent: Number(lesson.masteryPercent) })}
                  </span>
                </li>
              ))}
            </ul>
          </div>
          <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
            <h3 className="font-display text-h3 font-semibold">{t('weakSpots.objectives')}</h3>
            <ul className="flex flex-col gap-2">
              {weakSpots.objectives.map((objective) => (
                <li key={objective.objectiveId} className="flex flex-col">
                  <span className="text-ui text-text">{objective.text}</span>
                  <span className="text-caption text-text-muted">
                    {t('weakSpots.objectiveLine', {
                      lesson: objective.lessonName,
                      percent: Number(objective.masteryPercent),
                    })}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
    </section>
  );
}
