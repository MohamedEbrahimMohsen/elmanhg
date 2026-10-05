import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { AdminSubscriptionResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';

export interface StudentSubscriptionsCardProps {
  subscriptions: AdminSubscriptionResult[];
  studentName: string;
}

const headerKeys = ['plan', 'period', 'status', 'start', 'end'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function StudentSubscriptionsCard({ subscriptions, studentName }: StudentSubscriptionsCardProps) {
  const { t, i18n } = useTranslation('users');
  const headingId = useId();
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = (value: string) => formatDateTime(value, lng, 'date');

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('subscriptions.title')}
      </h2>
      {subscriptions.length === 0 ? (
        <p className="text-ui text-text-muted">{t('subscriptions.empty')}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
          <table className="w-full border-collapse">
            <caption className="sr-only">{t('subscriptions.caption', { name: studentName })}</caption>
            <thead>
              <tr>
                {headerKeys.map((key) => (
                  <th
                    key={key}
                    scope="col"
                    className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted"
                  >
                    {t(`subscriptions.${key}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {subscriptions.map((subscription) => (
                <tr key={subscription.id} className="border-t border-border">
                  <td className={cellClassName}>
                    {t(`plan.${subscription.plan}`)}
                    {subscription.isComplimentary ? (
                      <span className="ms-2 rounded-pill bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
                        {t('subscriptions.complimentary')}
                      </span>
                    ) : null}
                  </td>
                  <td className={cellClassName}>{t(`period.${subscription.period}`)}</td>
                  <td className={cellClassName}>{t(`subscriptionStatus.${subscription.status}`)}</td>
                  <td className={cellClassName}>{date(subscription.currentPeriodStart)}</td>
                  <td className={cellClassName}>{date(subscription.currentPeriodEnd)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
