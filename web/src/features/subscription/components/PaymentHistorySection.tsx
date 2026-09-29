import { useEffect, useEffectEvent, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Pagination } from '@/shared/components/Pagination';
import { usePaymentHistory } from '../hooks/usePaymentHistory';
import { useSubscriptionSearch } from '../hooks/useSubscriptionSearch';
import { PaymentHistoryTable } from './PaymentHistoryTable';

export function PaymentHistorySection() {
  const { t } = useTranslation('subscription');
  const headingId = useId();
  const { search, setPage } = useSubscriptionSearch();
  const { data, error, isPending, isError, refetch } = usePaymentHistory(search);
  const items = data?.items ?? [];
  const totalPages = Number(data?.totalPages ?? 0);
  const pageNumber = Number(data?.pageNumber ?? 1);
  const pageOutOfRange = !isPending && !isError && items.length === 0 && totalPages > 0 && pageNumber > totalPages;
  const resetPage = useEffectEvent(() => {
    setPage(1);
  });

  useEffect(() => {
    if (pageOutOfRange) {
      resetPage();
    }
  }, [pageOutOfRange]);

  if (isPending || pageOutOfRange) {
    return <ContentListSkeleton label={t('payments.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('payments.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (items.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('payments.title')}
      </h2>
      <PaymentHistoryTable items={items} />
      {totalPages > 1 ? <Pagination page={pageNumber} totalPages={totalPages} onPageChange={setPage} /> : null}
    </section>
  );
}
