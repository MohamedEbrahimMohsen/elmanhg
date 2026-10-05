import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { useExamPeriodMutations } from '../hooks/useExamPeriodMutations';
import { useExamPeriods } from '../hooks/useExamPeriods';
import { ConfigurationSkeleton } from './ConfigurationSkeleton';
import { DeleteExamPeriodDialog } from './DeleteExamPeriodDialog';
import { ExamPeriodDialog } from './ExamPeriodDialog';
import { ExamPeriodRow } from './ExamPeriodRow';

export function ExamPeriodsSection() {
  const { t } = useTranslation('configuration');
  const { data, error, isPending, isError, refetch } = useExamPeriods();
  const mutations = useExamPeriodMutations();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<ExamPeriodResult | null>(null);
  const [deleting, setDeleting] = useState<ExamPeriodResult | null>(null);

  const closeForm = (open: boolean) => {
    if (!open) {
      setCreating(false);
      setEditing(null);
    }
  };

  const renderBody = () => {
    if (isPending) {
      return <ConfigurationSkeleton label={t('examPeriods.loading')} />;
    }
    if (isError) {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-bold text-danger">{t('examPeriods.errorTitle')}</p>
          <p className="text-caption text-text">{t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>
          <Button variant="secondary" onClick={() => void refetch()}>
            {t('common:actions.retry')}
          </Button>
        </div>
      );
    }
    if (data.length === 0) {
      return <p className="text-ui text-text-muted">{t('examPeriods.empty')}</p>;
    }
    return (
      <ul className="flex flex-col divide-y divide-border">
        {data.map((examPeriod) => (
          <ExamPeriodRow key={examPeriod.id} examPeriod={examPeriod} onEdit={setEditing} onDelete={setDeleting} />
        ))}
      </ul>
    );
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('examPeriods.title')}</h2>
          <p className="text-caption text-text-muted">{t('examPeriods.description')}</p>
        </div>
        <Button
          size="sm"
          onClick={() => {
            setCreating(true);
          }}
        >
          {t('examPeriods.add')}
        </Button>
      </div>
      {renderBody()}
      <ExamPeriodDialog
        key={editing?.id ?? (creating ? 'create' : 'closed')}
        open={creating || editing !== null}
        examPeriod={editing}
        onOpenChange={closeForm}
        mutations={mutations}
      />
      <DeleteExamPeriodDialog
        examPeriod={deleting}
        onOpenChange={(open) => {
          if (!open) {
            setDeleting(null);
          }
        }}
        mutations={mutations}
      />
    </div>
  );
}
