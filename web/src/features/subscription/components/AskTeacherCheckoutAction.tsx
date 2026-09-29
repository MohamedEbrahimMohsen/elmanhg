import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface AskTeacherCheckoutActionProps {
  hasBase: boolean;
  disabled: boolean;
  onCheckout: () => void;
}

export function AskTeacherCheckoutAction({ hasBase, disabled, onCheckout }: AskTeacherCheckoutActionProps) {
  const { t } = useTranslation('subscription');
  const hintId = useId();

  return (
    <>
      <Button
        variant="accent"
        className="w-full"
        disabled={disabled || !hasBase}
        aria-describedby={hasBase ? undefined : hintId}
        onClick={onCheckout}
      >
        {t('checkout.subscribeAskTeacher')}
      </Button>
      {!hasBase && (
        <p id={hintId} className="text-caption text-text-muted">
          {t('checkout.requiresBaseHint')}
        </p>
      )}
    </>
  );
}
