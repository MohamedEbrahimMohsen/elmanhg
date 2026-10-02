import type { ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

export type ConfigurationBadgeTone = 'ok' | 'accent' | 'warning' | 'neutral';

export interface ConfigurationBadgeProps {
  tone: ConfigurationBadgeTone;
  children: ReactNode;
}

const toneClasses: Record<ConfigurationBadgeTone, string> = {
  ok: 'bg-success text-surface',
  accent: 'bg-accent-soft text-accent',
  warning: 'bg-warning-soft text-warning',
  neutral: 'bg-soft text-text-muted',
};

export function ConfigurationBadge({ tone, children }: ConfigurationBadgeProps) {
  return (
    <span className={cn('inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold', toneClasses[tone])}>
      {children}
    </span>
  );
}
