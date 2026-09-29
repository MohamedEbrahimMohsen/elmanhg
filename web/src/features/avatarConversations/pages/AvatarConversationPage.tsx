import { Link } from '@tanstack/react-router';
import { ChevronLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { AvatarConversationSkeleton } from '../components/AvatarConversationSkeleton';
import { AvatarConversationSummary } from '../components/AvatarConversationSummary';
import { AvatarLoggedMessage } from '../components/AvatarLoggedMessage';
import { useAvatarConversation } from '../hooks/useAvatarConversation';

export interface AvatarConversationPageProps {
  conversationId: string;
}

export function AvatarConversationPage({ conversationId }: AvatarConversationPageProps) {
  const { t } = useTranslation('avatarConversations');
  const { data, error, isPending, isError, refetch } = useAvatarConversation(conversationId);
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  const renderContent = () => {
    if (isPending) {
      return <AvatarConversationSkeleton label={t('detail.loading')} />;
    }
    if (isError) {
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-semibold text-danger">{t('detail.error')}</p>
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
    const messages = [...data.messages].sort((left, right) => Number(left.position) - Number(right.position));
    return (
      <>
        <AvatarConversationSummary conversation={data} />
        <ol className="flex flex-col gap-3">
          {messages.map((message) => (
            <li key={message.id}>
              <AvatarLoggedMessage message={message} />
            </li>
          ))}
        </ol>
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/admin/avatar-conversations"
        className="flex w-fit items-center gap-1 rounded-sm text-caption text-accent underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <ChevronLeft aria-hidden className="size-4 rtl:rotate-180" />
        {t('detail.back')}
      </Link>
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('detail.title')}</h1>
      {renderContent()}
    </section>
  );
}
