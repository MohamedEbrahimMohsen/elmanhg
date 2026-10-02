import { useTranslation } from 'react-i18next';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';

export interface ExamPeriodRowProps {
  examPeriod: ExamPeriodResult;
  onEdit: (examPeriod: ExamPeriodResult) => void;
  onDelete: (examPeriod: ExamPeriodResult) => void;
}

export function ExamPeriodRow({ examPeriod, onEdit, onDelete }: ExamPeriodRowProps) {
  const { t, i18n } = useTranslation('configuration');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const format = (date: string) => formatDate(new Date(`${date}T00:00:00`), lng, 'latin', { dateStyle: 'medium' });
  const name = examPeriod.name;

  return (
    <li className="flex flex-wrap items-center justify-between gap-3 py-3">
      <div className="flex flex-col gap-1">
        <p className="text-ui font-semibold text-text">{name}</p>
        <p className="text-caption text-text-muted">
          {t('examPeriods.range', { start: format(examPeriod.startDate), end: format(examPeriod.endDate) })}
        </p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="secondary"
          size="sm"
          aria-label={t('examPeriods.editLabel', { name })}
          onClick={() => {
            onEdit(examPeriod);
          }}
        >
          {t('examPeriods.edit')}
        </Button>
        <Button
          variant="danger"
          size="sm"
          aria-label={t('examPeriods.deleteLabel', { name })}
          onClick={() => {
            onDelete(examPeriod);
          }}
        >
          {t('examPeriods.delete')}
        </Button>
      </div>
    </li>
  );
}
