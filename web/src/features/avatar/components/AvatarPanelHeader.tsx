import { Link } from '@tanstack/react-router';
import { ArrowLeft, History, Maximize2, Sparkles, X } from 'lucide-react';
import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';
import type { AvatarStatusResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import type { AvatarView } from '../hooks/avatarReducer';
import { useAvatar } from '../hooks/useAvatar';

export interface AvatarPanelHeaderProps {
  view: AvatarView;
  status?: AvatarStatusResult | undefined;
}

export function AvatarPanelHeader({ view, status }: AvatarPanelHeaderProps) {
  const { t } = useTranslation('avatar');
  const { state, dispatch } = useAvatar();

  return (
    <div className="flex items-center justify-between gap-2">
      <Dialog.Title className="inline-flex items-center gap-2 font-display text-h3 font-bold">
        <Sparkles aria-hidden strokeWidth={1.8} className="size-5 text-accent-text" />
        {t('panel.title')}
      </Dialog.Title>
      <div className="flex items-center gap-1">
        {view === 'chat' && status !== undefined && !status.examInProgress ? (
          <Button asChild variant="ghost" className="min-w-11 px-0">
            {state.conversationId === null ? (
              <Link
                to="/student/assistant"
                aria-label={t('panel.openFullPage')}
                onClick={() => {
                  dispatch({ type: 'close' });
                }}
              >
                <Maximize2 aria-hidden className="size-5" />
              </Link>
            ) : (
              <Link
                to="/student/assistant/$conversationId"
                params={{ conversationId: state.conversationId }}
                aria-label={t('panel.openFullPage')}
                onClick={() => {
                  dispatch({ type: 'close' });
                }}
              >
                <Maximize2 aria-hidden className="size-5" />
              </Link>
            )}
          </Button>
        ) : null}
        {view === 'chat' && status !== undefined && !status.examInProgress ? (
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
        {view === 'history' ? (
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
