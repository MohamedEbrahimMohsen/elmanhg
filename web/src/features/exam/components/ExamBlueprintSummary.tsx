import { useTranslation } from 'react-i18next';
import type { ExamBlueprintSummaryResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

export interface ExamBlueprintSummaryProps {
  blueprint: ExamBlueprintSummaryResult;
}

const headerKeys = ['type', 'required', 'available'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function ExamBlueprintSummary({ blueprint }: ExamBlueprintSummaryProps) {
  const { t, i18n } = useTranslation('exam');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      {blueprint.isSubjectDefault ? (
        <span className="self-start rounded-full bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
          {t('start.defaultBlueprint')}
        </span>
      ) : null}
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <caption className="sr-only">{t('start.caption')}</caption>
          <thead>
            <tr>
              {headerKeys.map((key) => (
                <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                  {t(`start.${key}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {blueprint.typeCounts.map((row) => (
              <tr key={row.type} className="border-t border-border">
                <td className={cellClassName}>{t(`questions:types.${row.type}`)}</td>
                <td className={cellClassName}>{formatNumber(Number(row.required), lng)}</td>
                <td className={cn(cellClassName, Number(row.available) < Number(row.required) && 'text-danger')}>
                  {formatNumber(Number(row.available), lng)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="text-ui text-text">
        {blueprint.timeLimitMinutes == null
          ? t('start.noTimeLimit')
          : t('start.timeLimit', { minutes: Number(blueprint.timeLimitMinutes) })}
      </p>
      <p className="text-ui text-text">{t('start.passMark', { passMark: Number(blueprint.passMark) })}</p>
      <p className="text-caption text-text-muted">{t('start.note')}</p>
    </div>
  );
}
