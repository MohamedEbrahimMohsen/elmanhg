import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAvatar } from '../hooks/useAvatar';
import { AvatarPanel } from './AvatarPanel';

export function AvatarDock() {
  const { t } = useTranslation('avatar');
  const { state, open } = useAvatar();

  return (
    <>
      {state.isOpen ? null : (
        <button
          type="button"
          onClick={() => {
            open({ entryPoint: 'Global' });
          }}
          className="fixed start-4 bottom-24 z-10 inline-flex h-12 items-center gap-2 rounded-pill bg-text px-5 text-ui font-semibold text-surface shadow-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden lg:bottom-6"
        >
          <Sparkles aria-hidden strokeWidth={1.8} className="size-4" />
          {t('dock.open')}
        </button>
      )}
      <AvatarPanel />
    </>
  );
}
