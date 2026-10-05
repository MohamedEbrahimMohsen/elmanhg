import { useTranslation } from 'react-i18next';
import type { QuestionImportRowError } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';

export interface QuestionImportErrorTableProps {
  errors: QuestionImportRowError[];
}

const headerKeys = ['sheet', 'row', 'column', 'problem'] as const;

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';

export function QuestionImportErrorTable({ errors }: QuestionImportErrorTableProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('import.report.errorsCaption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                {t(`import.report.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {errors.map(({ sheet, row, column, code }) => (
            <tr key={`${sheet}-${String(row)}-${column ?? ''}-${code}`} className="border-t border-border">
              <td className={cellClassName}>{sheet}</td>
              <td className={cellClassName}>{formatNumber(Number(row), lng)}</td>
              <td className={cellClassName}>{column ?? t('import.report.noColumn')}</td>
              <td className={cellClassName}>
                {t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'], { column: column ?? '' })}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
