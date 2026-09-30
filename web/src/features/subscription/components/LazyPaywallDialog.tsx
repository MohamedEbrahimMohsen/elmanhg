import { lazy, Suspense } from 'react';
import type { PaywallDialogProps } from './PaywallDialog';

const PaywallDialog = lazy(async () => {
  const module = await import('./PaywallDialog');
  return { default: module.PaywallDialog };
});

export function LazyPaywallDialog({ reason, onClose }: PaywallDialogProps) {
  if (reason === null) {
    return null;
  }

  return (
    <Suspense fallback={null}>
      <PaywallDialog reason={reason} onClose={onClose} />
    </Suspense>
  );
}
