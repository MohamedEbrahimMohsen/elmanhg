import { useTranslation } from 'react-i18next';
import { Toaster as Sonner } from 'sonner';

export function Toaster() {
  const { i18n } = useTranslation();

  return (
    <Sonner
      dir={i18n.dir(i18n.resolvedLanguage)}
      position="top-center"
      toastOptions={{
        classNames: { toast: 'rounded-lg border border-border bg-surface font-sans text-ui text-text shadow-1' },
      }}
    />
  );
}
