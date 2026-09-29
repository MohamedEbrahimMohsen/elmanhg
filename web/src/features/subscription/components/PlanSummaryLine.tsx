import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetMyUsage } from '@/shared/api/generated/subscriptions/subscriptions';
import { Button } from '@/shared/ui/button';
import { planLabelKey } from '../api/entitlement';

export function PlanSummaryLine() {
  const { t } = useTranslation('subscription');
  const { data } = useGetMyUsage();
  if (!data) {
    return null;
  }

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <span className="text-ui text-text">{t('usage.plan', { plan: t(`current.${planLabelKey(data)}`) })}</span>
      {data.dailyQuizQuestionLimit == null ? null : (
        <>
          <span className="text-ui text-text-muted">
            {t('usage.homeCounter', {
              used: Number(data.quizQuestionsUsedToday),
              limit: Number(data.dailyQuizQuestionLimit),
            })}
          </span>
          <Button asChild size="sm" variant="primary">
            <Link to="/student/subscription">{t('usage.subscribe')}</Link>
          </Button>
        </>
      )}
    </div>
  );
}
