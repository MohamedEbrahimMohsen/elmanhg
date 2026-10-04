import { useTranslation } from 'react-i18next';
import { Pagination } from '@/shared/components/Pagination';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { hasActiveFilters } from '../api/avatarConversationParams';
import { AvatarConversationEmptyState } from '../components/AvatarConversationEmptyState';
import { AvatarConversationFilters } from '../components/AvatarConversationFilters';
import { AvatarConversationSkeleton } from '../components/AvatarConversationSkeleton';
import { AvatarConversationTable } from '../components/AvatarConversationTable';
import { useAvatarConversations } from '../hooks/useAvatarConversations';
import { useAvatarConversationSearch } from '../hooks/useAvatarConversationSearch';
import { registerAvatarConversationsLocales } from '../locales';

registerAvatarConversationsLocales();

export function AvatarConversationsPage() {
  const { t } = useTranslation('avatarConversations');
  const { search, applyFilters, setPage, clearFilters } = useAvatarConversationSearch();
  const { data, error, isPending, isError, refetch } = useAvatarConversations(search);
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  const renderContent = () => {
    if (isPending) {
      return <AvatarConversationSkeleton label={t('page.loading')} />;
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
        <AvatarConversationEmptyState
          variant={hasActiveFilters(search) ? 'no-results' : 'no-data'}
          onClear={clearFilters}
        />
      );
    }
    return (
      <>
        <AvatarConversationTable items={data.items} />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-ui text-text-muted">{t('page.intro')}</p>
      <AvatarConversationFilters
        key={JSON.stringify([search.search, search.entryPoint, search.from, search.to])}
        search={search}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
    </section>
  );
}
