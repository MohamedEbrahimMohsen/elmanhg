import { useTranslation } from 'react-i18next';
import { Pagination } from '@/shared/components/Pagination';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { hasActiveFilters } from '../api/auditLogParams';
import { AuditLogEmptyState } from '../components/AuditLogEmptyState';
import { AuditLogFilters } from '../components/AuditLogFilters';
import { AuditLogTable } from '../components/AuditLogTable';
import { AuditLogTableSkeleton } from '../components/AuditLogTableSkeleton';
import { useAuditLogs } from '../hooks/useAuditLogs';
import { useAuditLogSearch } from '../hooks/useAuditLogSearch';
import { registerAuditLocales } from '../locales';

registerAuditLocales();

export function AuditLogPage() {
  const { t } = useTranslation('audit');
  const { search, applyFilters, setPage, clearFilters } = useAuditLogSearch();
  const { data, error, isPending, isError, refetch } = useAuditLogs(search);
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  const renderContent = () => {
    if (isPending) {
      return <AuditLogTableSkeleton />;
    }
    if (isError) {
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-bold text-danger">{t('error.title')}</p>
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
      return (
        <AuditLogEmptyState variant={hasActiveFilters(search) ? 'no-results' : 'no-data'} onClear={clearFilters} />
      );
    }
    return (
      <>
        <AuditLogTable items={data.items} />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <AuditLogFilters
        key={JSON.stringify([search.actor, search.resourceType, search.from, search.to])}
        search={search}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
    </section>
  );
}
