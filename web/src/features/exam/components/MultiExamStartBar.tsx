import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { PaywallDialog, paywallReason } from '@/features/subscription';
import { Button } from '@/shared/ui/button';
import { useMultiExamBlockReason, type MultiExamBlockReason } from '../hooks/useMultiExamBlockReason';
import { useStartMultiExam } from '../hooks/useStartMultiExam';

export interface MultiExamStartBarProps {
  subjectId: string;
  unitIds: string[];
  size: number;
  inProgress: boolean;
}

const reasonKeys = {
  inProgress: 'multi.reasonInProgress',
  chooseTwo: 'multi.chooseTwo',
  shortfall: 'start.shortfall',
} as const satisfies Record<MultiExamBlockReason, string>;

export function MultiExamStartBar({ subjectId, unitIds, size, inProgress }: MultiExamStartBarProps) {
  const { t } = useTranslation('exam');
  const reasonId = useId();
  const reason = useMultiExamBlockReason(subjectId, unitIds, size, inProgress);
  const starter = useStartMultiExam();

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 md:flex-row md:items-center md:justify-between lg:p-5">
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-ui font-bold text-text">{t('multi.summary', { units: unitIds.length, size })}</p>
        <p id={reasonId} aria-live="polite" className="text-caption text-text-muted">
          {reason ? t(reasonKeys[reason]) : null}
        </p>
        {starter.errorCode && paywallReason(starter.errorCode) === null ? (
          <p role="alert" className="text-caption text-danger">
            {t([`common:errors.${starter.errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
        ) : null}
      </div>
      <Button
        variant="primary"
        className="w-full md:w-auto"
        disabled={reason !== null || starter.isPending}
        aria-describedby={reason ? reasonId : undefined}
        onClick={() => {
          starter.start(subjectId, unitIds, size);
        }}
      >
        {t('start.start')}
      </Button>
      <PaywallDialog reason={paywallReason(starter.errorCode)} onClose={starter.reset} />
    </div>
  );
}
