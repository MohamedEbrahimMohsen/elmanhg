import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import type { AvatarContextInput } from '../api/avatarContext';
import { useAvatar } from '../hooks/useAvatar';

export interface AskAvatarButtonProps {
  context: AvatarContextInput;
  label?: 'default' | 'lesson';
}

export function AskAvatarButton({ context, label = 'default' }: AskAvatarButtonProps) {
  const { t } = useTranslation('avatar');
  const { open } = useAvatar();

  return (
    <Button
      variant="secondary"
      className="self-start"
      onClick={() => {
        open(context);
      }}
    >
      <Sparkles aria-hidden strokeWidth={1.8} className="size-4 text-accent" />
      {t(label === 'lesson' ? 'ask.lesson' : 'ask.default')}
    </Button>
  );
}
