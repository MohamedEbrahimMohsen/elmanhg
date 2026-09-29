import { Sparkles, X } from 'lucide-react';
import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';
import { useGetAvatarStatus } from '@/shared/api/generated/avatar/avatar';
import { Button } from '@/shared/ui/button';
import { useAvatar } from '../hooks/useAvatar';
import { AvatarComposer } from './AvatarComposer';
import { AvatarConversation } from './AvatarConversation';

export function AvatarPanel() {
  const { t } = useTranslation('avatar');
  const { state, close } = useAvatar();
  const status = useGetAvatarStatus({ query: { enabled: state.isOpen } });

  return (
    <Dialog.Root
      open={state.isOpen}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 bg-overlay" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed inset-y-0 start-0 flex h-dvh w-full max-w-95 flex-col gap-3 rounded-e-lg bg-surface p-4 shadow-2 focus-visible:outline-hidden motion-safe:transition-transform motion-safe:duration-(--ds-motion-base-duration) motion-safe:ease-(--ds-motion-base-easing) motion-safe:starting:ltr:-translate-x-full motion-safe:starting:rtl:translate-x-full"
        >
          <div className="flex items-center justify-between gap-2">
            <Dialog.Title className="inline-flex items-center gap-2 font-display text-h3 font-semibold">
              <Sparkles aria-hidden strokeWidth={1.8} className="size-5 text-accent" />
              {t('panel.title')}
            </Dialog.Title>
            <Dialog.Close asChild>
              <Button variant="ghost" className="min-w-11 px-0" aria-label={t('panel.close')}>
                <X aria-hidden className="size-5" />
              </Button>
            </Dialog.Close>
          </div>
          <p className="text-caption text-text-muted">
            {t('panel.context', { title: state.context.title ?? t('panel.contextGlobal') })}
          </p>
          {status.isPending ? (
            <div role="status" aria-busy="true" className="flex-1 text-ui text-text-muted">
              {t('panel.loading')}
            </div>
          ) : status.isError ? (
            <div className="flex flex-1 flex-col items-start gap-2">
              <p role="alert" className="text-ui text-danger">
                {t('panel.statusError')}
              </p>
              <Button
                variant="secondary"
                onClick={() => {
                  void status.refetch();
                }}
              >
                {t('panel.retry')}
              </Button>
            </div>
          ) : (
            <>
              {status.data.tier === 'Free' ? (
                <p className="text-caption text-text-muted">
                  {t('panel.quota', { used: status.data.messagesUsedToday, limit: status.data.dailyMessageLimit })}
                </p>
              ) : null}
              <AvatarConversation status={status.data} />
              <AvatarComposer status={status.data} />
            </>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
