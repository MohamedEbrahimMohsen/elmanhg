import { useTranslation } from 'react-i18next';
import { formatDate, formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { isCountdownUrgent, splitCountdown } from '../api/examSession';
import type { ExamSaveStatus } from '../hooks/useExamAnswers';

export interface ExamHeaderProps {
  title: string;
  remainingMilliseconds: number | null;
  status: ExamSaveStatus;
  lastSavedAt: Date | null;
}

export function ExamHeader({ title, remainingMilliseconds, status, lastSavedAt }: ExamHeaderProps) {
  const { t, i18n } = useTranslation('exam');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const urgent = remainingMilliseconds !== null && isCountdownUrgent(remainingMilliseconds);
  const countdown = remainingMilliseconds === null ? null : splitCountdown(remainingMilliseconds);
  const statusText =
    status === 'saving'
      ? t('exam.saving')
      : status === 'saved' && lastSavedAt
        ? t('exam.savedAt', { time: formatDate(lastSavedAt, lng, 'arabic-indic', { timeStyle: 'short' }) })
        : status === 'error'
          ? t('exam.saveFailed')
          : t('exam.autoSaved');

  return (
    <div className="sticky top-16 z-10 flex flex-wrap items-center justify-between gap-2 rounded-md border border-border bg-surface p-4 shadow-1">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{title}</h1>
      {countdown ? (
        <p role="timer" aria-live="off" className={cn('font-display text-h3 font-bold', urgent && 'text-danger')}>
          {t('exam.timeLeft', {
            minutes: formatNumber(countdown.minutes, lng),
            seconds: formatNumber(countdown.seconds, lng, 'arabic-indic', { minimumIntegerDigits: 2 }),
          })}
        </p>
      ) : null}
      {urgent ? (
        <p role="status" className="sr-only">
          {t('exam.timeUrgent')}
        </p>
      ) : null}
      <p aria-live="polite" className={cn('text-caption text-text-muted', status === 'error' && 'text-danger')}>
        {statusText}
      </p>
    </div>
  );
}
