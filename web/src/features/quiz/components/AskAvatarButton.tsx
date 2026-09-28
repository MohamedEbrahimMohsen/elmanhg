import { useId } from 'react';
import { Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export function AskAvatarButton() {
  const { t } = useTranslation('quiz');
  const hintId = useId();

  return (
    <div className="flex flex-col items-start gap-1">
      <Button variant="secondary" disabled aria-describedby={hintId}>
        <Sparkles aria-hidden strokeWidth={1.8} className="size-4 text-accent" />
        {t('avatar.ask')}
      </Button>
      <p id={hintId} className="text-caption text-text-muted">
        {t('avatar.soon')}
      </p>
    </div>
  );
}
