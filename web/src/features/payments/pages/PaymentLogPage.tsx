import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import { Pagination } from '@/shared/components/Pagination';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { hasActiveFilters } from '../api/paymentLogParams';
import { PaymentLogEmptyState } from '../components/PaymentLogEmptyState';
import { PaymentLogFilters } from '../components/PaymentLogFilters';
import { PaymentLogTable } from '../components/PaymentLogTable';
import { PaymentLogTableSkeleton } from '../components/PaymentLogTableSkeleton';
import { PaymentLogTabs } from '../components/PaymentLogTabs';
import { RefundPaymentDialog } from '../components/RefundPaymentDialog';
import { ResolveReviewDialog } from '../components/ResolveReviewDialog';
import { usePaymentLog } from '../hooks/usePaymentLog';
import { usePaymentLogSearch } from '../hooks/usePaymentLogSearch';
import { usePaymentReviewCount } from '../hooks/usePaymentReviewCount';
import { useResolvePaymentReview } from '../hooks/useResolvePaymentReview';

export function PaymentLogPage() {
  const { t } = useTranslation('payments');
  const { search, applyFilters, setView, setPage, filterStudent, clearFilters } = usePaymentLogSearch();
  const { data, error, isPending, isError, refetch } = usePaymentLog(search);
  const { data: reviewCount = 0 } = usePaymentReviewCount();
  const { resolve, isPending: isResolving } = useResolvePaymentReview();
  const [refundTarget, setRefundTarget] = useState<AdminPaymentResult | null>(null);
  const [keepTarget, setKeepTarget] = useState<AdminPaymentResult | null>(null);
  const view = search.view ?? 'all';
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  const renderContent = () => {
    if (isPending) {
      return <PaymentLogTableSkeleton />;
    }
    if (isError) {
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-semibold text-danger">{t('error.title')}</p>
          <p className="text-caption text-text">
            {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              void refetch();
            }}
          >
            {t('common:actions.retry')}
          </Button>
        </div>
      );
    }
    if (data.items.length === 0) {
      const variant = hasActiveFilters(search) ? 'no-results' : view === 'review' ? 'no-review' : 'no-data';
      return <PaymentLogEmptyState variant={variant} onClear={clearFilters} />;
    }
    return (
      <>
        <PaymentLogTable
          items={data.items}
          onRefund={setRefundTarget}
          onKeep={setKeepTarget}
          onStudent={filterStudent}
        />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <PaymentLogTabs view={view} reviewCount={reviewCount} onChange={setView} />
      <PaymentLogFilters
        key={JSON.stringify([search.status, search.plan, search.reference, search.from, search.to])}
        search={search}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
      {refundTarget ? (
        <RefundPaymentDialog
          key={refundTarget.id}
          payment={refundTarget}
          onOpenChange={(open) => {
            if (!open) {
              setRefundTarget(null);
            }
          }}
        />
      ) : null}
      <ResolveReviewDialog
        payment={keepTarget}
        isPending={isResolving}
        onOpenChange={(open) => {
          if (!open) {
            setKeepTarget(null);
          }
        }}
        onConfirm={() => {
          if (keepTarget) {
            resolve(keepTarget.id, () => {
              setKeepTarget(null);
            });
          }
        }}
      />
    </section>
  );
}
