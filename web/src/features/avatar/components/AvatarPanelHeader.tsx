import { ArrowLeft, History, Sparkles, X } from 'lucide-react';
import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { useAvatar } from '../hooks/useAvatar';

export interface AvatarPanelHeaderProps {
  status?: AvatarStatusResult | undefined;
}

export function AvatarPanelHeader({ status }: AvatarPanelHeaderProps) {
  const { t } = useTranslation('avatar');
  const { state, dispatch } = useAvatar();

  return (
    <div className="flex items-center justify-between gap-2">
      <Dialog.Title className="inline-flex items-center gap-2 font-display text-h3 font-semibold">
        <Sparkles aria-hidden strokeWidth={1.8} className="size-5 text-accent" />
        {t('panel.title')}
      </Dialog.Title>
      <div className="flex items-center gap-1">
        {state.view === 'chat' && status !== undefined && !status.examInProgress ? (
          <Button
            variant="ghost"
            className="min-w-11 px-0"
            aria-label={t('panel.history')}
            onClick={() => {
              dispatch({ type: 'showHistory' });
            }}
          >
            <History aria-hidden className="size-5" />
          </Button>
        ) : null}
        {state.view === 'history' ? (
          <Button
            variant="ghost"
            onClick={() => {
              dispatch({ type: 'showChat' });
            }}
          >
            <ArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
            {t('panel.backToChat')}
          </Button>
        ) : null}
        <Dialog.Close asChild>
          <Button variant="ghost" className="min-w-11 px-0" aria-label={t('panel.close')}>
            <X aria-hidden className="size-5" />
          </Button>
        </Dialog.Close>
      </div>
    </div>
  );
}
