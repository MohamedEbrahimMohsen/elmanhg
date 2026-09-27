import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export type SignInMethod = 'phone' | 'email';

export interface MethodSwitchProps {
  value: SignInMethod;
  onChange: (method: SignInMethod) => void;
}

const methods = [
  { method: 'phone', labelKey: 'signIn.phoneTab' },
  { method: 'email', labelKey: 'signIn.emailTab' },
] as const;

export function MethodSwitch({ value, onChange }: MethodSwitchProps) {
  const { t } = useTranslation('session');

  return (
    <div role="group" aria-label={t('signIn.methodLabel')} className="flex gap-2">
      {methods.map(({ method, labelKey }) => {
        const active = method === value;
        return (
          <Button
            key={method}
            type="button"
            variant={active ? 'primary' : 'secondary'}
            aria-pressed={active}
            onClick={() => {
              onChange(method);
            }}
          >
            {t(labelKey)}
          </Button>
        );
      })}
    </div>
  );
}
