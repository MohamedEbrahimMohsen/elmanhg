import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { SubjectProgressResult } from '@/shared/api/generated/model';

export interface StudentSubjectCardProps {
  subject: SubjectProgressResult;
}

const cellClassName = 'px-2.5 py-2.25 text-caption';
const headerKeys = ['unit', 'unitMastery', 'bestExam'] as const;

export function StudentSubjectCard({ subject }: StudentSubjectCardProps) {
  const { t } = useTranslation('users');
  const id = useId();
  const percent = Number(subject.masteryPercent);

  return (
    <article
      aria-labelledby={id}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h3 id={id} className="font-display text-h3 font-semibold">
        {subject.name}
      </h3>
      <MasteryBar percent={percent} label={t('progress.barLabel', { name: subject.name })} />
      <p className="text-caption text-text-muted">
        {t('progress.mastery', { percent })} ·{' '}
        {t('progress.counts', {
          mastered: Number(subject.masteredCount),
          servable: Number(subject.servableCount),
          seen: Number(subject.seenCount),
        })}
      </p>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <caption className="sr-only">{t('progress.unitsCaption', { name: subject.name })}</caption>
          <thead>
            <tr>
              {headerKeys.map((key) => (
                <th
                  key={key}
                  scope="col"
                  className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
                >
                  {t(`progress.${key}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {subject.units.map((unit) => (
              <tr key={unit.unitId} className="border-t border-border">
                <td className={cellClassName}>{unit.name}</td>
                <td className={cellClassName}>
                  {t('progress.unitMasteryValue', { percent: Number(unit.masteryPercent) })}
                </td>
                <td className={cellClassName}>
                  {unit.bestExamScorePercent == null
                    ? t('progress.noExam')
                    : t('progress.bestExamValue', { score: Math.round(Number(unit.bestExamScorePercent)) })}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </article>
  );
}
