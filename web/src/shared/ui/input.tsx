import type { ComponentProps } from 'react';
import { cn } from '@/shared/lib/utils';

export type InputProps = ComponentProps<'input'>;

export function Input({ className, ...props }: InputProps) {
  return (
    <input
      data-slot="input"
      className={cn(
        'h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text placeholder:text-text-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-invalid:border-danger',
        className,
      )}
      {...props}
    />
  );
}
