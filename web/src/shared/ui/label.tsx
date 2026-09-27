import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export type LabelProps = ComponentProps<'label'>;

export function Label({ className, htmlFor, ...props }: LabelProps) {
  return <label htmlFor={htmlFor} className={cn('text-caption text-text-muted', className)} {...props} />;
}
