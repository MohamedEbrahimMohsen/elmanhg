import { useTranslation } from 'react-i18next';
import type { ExamUnitBreakdownResult } from '@/shared/api/generated/model';

export interface ExamUnitBreakdownProps {
  units: ExamUnitBreakdownResult[];
}

const headerKeys = ['unit', 'percentHeader', 'unitCorrectHeader'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function ExamUnitBreakdown({ units }: ExamUnitBreakdownProps) {
  const { t } = useTranslation('exam');

  if (units.length === 0) {
    return null;
  }
  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.byUnit')}</h2>
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
            </tr>
          </thead>
          <tbody>
            {units.map((unit) => (
              <tr key={unit.unitId} className="border-t border-border">
                <td className={cellClassName}>{unit.name ?? t('result.unknownUnit')}</td>
                <td className={cellClassName}>
                  {t('result.percent', { percent: Math.round(Number(unit.scorePercent)) })}
                </td>
                <td className={cellClassName}>
                  {t('result.unitCorrect', { correct: Number(unit.correctCount), total: Number(unit.questionCount) })}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
