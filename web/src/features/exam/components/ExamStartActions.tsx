import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { PaywallDialog, paywallReason } from '@/features/subscription';
import type { UnitExamOverviewResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { useStartExam } from '../hooks/useStartExam';

export interface ExamStartActionsProps {
  overview: UnitExamOverviewResult;
}

const warningClassName = 'flex flex-col items-start gap-2 rounded-lg border border-warning bg-warning-soft p-4';

export function ExamStartActions({ overview }: ExamStartActionsProps) {
  const { t } = useTranslation('exam');
  const { start, isPending, errorCode, reset } = useStartExam(overview.unitId);
  const inProgress = overview.inProgressExam;

  if (inProgress?.isThisUnit) {
    return (
      <Button asChild variant="primary" className="self-start">
        <Link to="/student/exam/$sessionId" params={{ sessionId: inProgress.sessionId }}>
          {t('start.continue')}
        </Link>
      </Button>
    );
  }
  if (inProgress) {
    return (
      <div className={warningClassName}>
        <p className="text-ui text-text">{t('start.otherInProgress')}</p>
        <Link
          to="/student/exam/$sessionId"
          params={{ sessionId: inProgress.sessionId }}
          className="inline-flex min-h-11 items-center rounded-sm text-ui text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('start.openOther')}
        </Link>
      </div>
    );
  }
  if (Number(overview.unopenedLessonCount) > 0) {
    return (
      <div className={warningClassName}>
        <p className="text-ui text-text">
          {t('start.lessonsNotOpened', { count: Number(overview.unopenedLessonCount) })}
        </p>
        <Link
          to="/student/unit/$unitId"
          params={{ unitId: overview.unitId }}
          className="inline-flex min-h-11 items-center rounded-sm text-ui text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('start.toUnit')}
        </Link>
      </div>
    );
  }
  if (overview.blueprint && !overview.isAvailable) {
    return (
      <div className={warningClassName}>
        <p className="text-ui font-bold text-text">{t('start.shortfall')}</p>
      </div>
    );
  }
  if (!overview.blueprint) {
    return null;
  }
  return (
    <div className="flex flex-col items-start gap-2">
      <Button variant="primary" disabled={isPending} onClick={start}>
        {t('start.start')}
      </Button>
      {errorCode && paywallReason(errorCode) === null ? (
        <p role="alert" className="text-caption text-danger">
          {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
      <PaywallDialog reason={paywallReason(errorCode)} onClose={reset} />
    </div>
  );
}
