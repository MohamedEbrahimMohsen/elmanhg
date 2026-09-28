import { useTranslation } from 'react-i18next';
import type { UnitProgressResult } from '@/shared/api/generated/model';

export interface UnitProgressTableProps {
  units: UnitProgressResult[];
  subjectName: string;
}

const headerKeys = ['unit', 'unitMastery', 'bestExam'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function UnitProgressTable({ units, subjectName }: UnitProgressTableProps) {
  const { t } = useTranslation('progress');

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('subjects.unitsCaption', { name: subjectName })}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th
                key={key}
                scope="col"
                className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
              >
                {t(`subjects.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {units.map((unit) => (
            <tr key={unit.unitId} className="border-t border-border">
              <td className={cellClassName}>{unit.name}</td>
              <td className={cellClassName}>
                {t('subjects.unitMasteryValue', { percent: Number(unit.masteryPercent) })}
              </td>
              <td className={cellClassName}>
                {unit.bestExamScorePercent == null
                  ? t('subjects.noExam')
                  : t('subjects.bestExamValue', { score: Math.round(Number(unit.bestExamScorePercent)) })}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
