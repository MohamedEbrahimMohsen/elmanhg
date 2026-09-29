import { keepPreviousData } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ContentListSkeleton } from '@/features/content';
import { PaywallDialog, paywallReason } from '@/features/subscription';
import { usePreviewMultiUnitExam } from '@/shared/api/generated/exams/exams';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { useStartMultiExam } from '../hooks/useStartMultiExam';
import { ExamBlueprintSummary } from './ExamBlueprintSummary';
import { MultiExamUnitShares } from './MultiExamUnitShares';

export interface MultiExamPreviewProps {
  subjectId: string;
  unitIds: string[];
  size: number;
  canStart: boolean;
}

const errorTextKey = (code: string) => [`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'];

export function MultiExamPreview({ subjectId, unitIds, size, canStart }: MultiExamPreviewProps) {
  const { t } = useTranslation('exam');
  const { data, error, isPending, isError } = usePreviewMultiUnitExam(
    subjectId,
    { unitIds, size },
    { query: { placeholderData: keepPreviousData } },
  );
  const starter = useStartMultiExam();

  if (isPending) {
    return <ContentListSkeleton label={t('multi.previewLoading')} />;
  }
  if (isError) {
    return (
      <p role="alert" className="text-ui text-danger">
        {t(errorTextKey(error instanceof ApiError ? error.code : unhandledErrorCode))}
      </p>
    );
  }
  return (
    <div className="flex flex-col gap-3">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('multi.previewTitle')}</h2>
      <ExamBlueprintSummary blueprint={data.blueprint} />
      <MultiExamUnitShares units={data.units} />
      {!data.isAvailable ? (
        <div className="flex flex-col items-start gap-2 rounded-lg border border-warning bg-warning-soft p-4">
          <p className="text-ui font-semibold text-text">{t('start.shortfall')}</p>
        </div>
      ) : canStart ? (
        <div className="flex flex-col items-start gap-2">
          <Button
            variant="primary"
            disabled={starter.isPending}
            onClick={() => {
              starter.start(subjectId, unitIds, size);
            }}
          >
            {t('start.start')}
          </Button>
          {starter.errorCode && paywallReason(starter.errorCode) === null ? (
            <p role="alert" className="text-caption text-danger">
              {t(errorTextKey(starter.errorCode))}
            </p>
          ) : null}
          <PaywallDialog reason={paywallReason(starter.errorCode)} onClose={starter.reset} />
        </div>
      ) : null}
    </div>
  );
}
