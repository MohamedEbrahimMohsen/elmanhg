import { useTranslation } from 'react-i18next';
import type { TrainingExportResult } from '@/shared/api/generated/model';
import { formatDate, formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { TrainingExportStatusBadge } from './TrainingExportStatusBadge';

export interface TrainingExportRowProps {
  item: TrainingExportResult;
  subjectName: string;
  downloading: boolean;
  onDownload: (item: TrainingExportResult) => void;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';
const kilobyte = 1024;

export function TrainingExportRow({ item, subjectName, downloading, onDownload }: TrainingExportRowProps) {
  const { t, i18n } = useTranslation('trainingExport');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = (value: string) => formatDate(new Date(value), lng, 'latin', { dateStyle: 'medium' });
  const lastDay = new Date(new Date(item.to).getTime() - 1).toISOString();

  const renderAction = () => {
    if (item.status === 'Completed') {
      return (
        <div className="flex flex-col items-start gap-1">
          <Button
            variant="secondary"
            size="sm"
            disabled={downloading}
            aria-busy={downloading}
            onClick={() => {
              onDownload(item);
            }}
          >
            {t('list.download')}
          </Button>
          {item.expiresAt ? (
            <span className="text-caption text-text-muted">
              {t('list.availableUntil', { date: date(item.expiresAt) })}
            </span>
          ) : null}
        </div>
      );
    }
    if (item.status === 'Pending') {
      return <span className="text-text-muted">{t('list.preparing')}</span>;
    }
    if (item.status === 'Failed') {
      return (
        <span dir="ltr" className="font-mono text-mono">
          {item.lastErrorCode}
        </span>
      );
    }
    return <span className="text-text-muted">{t('list.expired')}</span>;
  };

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cellClassName}>{t(`source.${item.source}`)}</td>
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        {t('list.period', { from: date(item.from), to: date(lastDay) })}
      </td>
      <td className={cellClassName}>{subjectName}</td>
      <td className={cellClassName}>
        <TrainingExportStatusBadge status={item.status} />
      </td>
      <td className={cellClassName}>
        {item.rowCount === null ? '—' : formatNumber(Number(item.rowCount), lng, 'latin')}
      </td>
      <td className={cellClassName}>
        {item.fileSizeBytes === null
          ? '—'
          : t('list.sizeKb', {
              size: formatNumber(Number(item.fileSizeBytes) / kilobyte, lng, 'latin', { maximumFractionDigits: 1 }),
            })}
      </td>
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        {formatDate(new Date(item.requestedAt), lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' })}
      </td>
      <td className={cellClassName}>{renderAction()}</td>
    </tr>
  );
}
