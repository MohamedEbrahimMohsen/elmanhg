import { useTranslation } from 'react-i18next';
import type { ExamObjectiveResult } from '@/shared/api/generated/model';

export interface ExamWeakestObjectivesProps {
  objectives: ExamObjectiveResult[];
}

export function ExamWeakestObjectives({ objectives }: ExamWeakestObjectivesProps) {
  const { t } = useTranslation('exam');

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.weakest')}</h2>
      {objectives.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {objectives.map((objective) => (
            <li key={objective.objectiveId} className="text-ui text-text">
              {objective.text}
              <span className="text-caption text-text-muted">
                {' — '}
                {objective.lessonName ?? t('result.unknownLesson')}
                {' · '}
                {t('result.percent', { percent: Math.round(Number(objective.scorePercent)) })}
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-ui text-text-muted">{t('result.noWeak')}</p>
      )}
    </div>
  );
}
