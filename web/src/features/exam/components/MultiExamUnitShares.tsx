import { useTranslation } from 'react-i18next';
import type { MultiUnitExamUnitShareResult } from '@/shared/api/generated/model';

export interface MultiExamUnitSharesProps {
  units: MultiUnitExamUnitShareResult[];
}

export function MultiExamUnitShares({ units }: MultiExamUnitSharesProps) {
  const { t } = useTranslation('exam');

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h3 className="text-ui font-semibold text-text">{t('multi.unitShares')}</h3>
      <ul className="flex flex-col gap-1">
        {units.map((unit) => (
          <li key={unit.unitId} className="text-ui text-text">
            {t('multi.unitShare', { unit: unit.name, count: Number(unit.questionCount) })}
          </li>
        ))}
      </ul>
    </div>
  );
}
