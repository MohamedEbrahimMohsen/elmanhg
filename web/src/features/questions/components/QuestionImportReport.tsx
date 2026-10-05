import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { QuestionImportPreviewResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { QuestionImportErrorTable } from './QuestionImportErrorTable';

export interface QuestionImportReportProps {
  preview: QuestionImportPreviewResult;
  onConfirm: () => void;
  isConfirming: boolean;
}

export function QuestionImportReport({ preview, onConfirm, isConfirming }: QuestionImportReportProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const headingId = useId();
  const validRows = Number(preview.validRows);

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"
    >
      <h2 id={headingId} className="font-display text-h2 font-bold">
        {t('import.report.title')}
      </h2>
      <p className="text-ui text-text">
        {t('import.report.summary', {
          total: Number(preview.totalRows),
          valid: validRows,
          errors: preview.errors.length,
        })}
      </p>
      <h3 className="font-display text-h3 font-bold">{t('import.report.types')}</h3>
      <ul className="flex flex-col gap-1 text-caption text-text">
        {preview.types.map(({ type, count }) => (
          <li key={type}>{`${t(`types.${type}`)}: ${formatNumber(Number(count), lng)}`}</li>
        ))}
      </ul>
      {preview.errors.length > 0 ? (
        <>
          <QuestionImportErrorTable errors={preview.errors} />
          <p className="rounded-md border border-warning bg-warning-soft px-3.5 py-3 text-caption text-text">
            {t('import.report.fixAndRetry')}
          </p>
        </>
      ) : (
        <>
          <p className="text-caption text-text-muted">{t('import.report.pendingNote')}</p>
          <div>
            <Button onClick={onConfirm} disabled={isConfirming} aria-busy={isConfirming}>
              {t('import.report.confirm', { count: validRows })}
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
