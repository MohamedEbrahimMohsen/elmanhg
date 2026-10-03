import type { ComponentProps } from 'react';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/shared/lib/utils';

export type SelectProps = ComponentProps<'select'>;

export function Select({ className, ...props }: SelectProps) {
  return (
    <span className={cn('relative block w-full', className)}>
      <select
        data-slot="select"
        className="h-11 w-full appearance-none rounded-sm border border-border-strong bg-surface px-3 pe-10 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-invalid:border-danger"
        {...props}
      />
      <span
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 end-0 flex items-center px-3 text-text-muted"
      >
        <ChevronDown className="size-4" strokeWidth={1.8} />
      </span>
    </span>
  );
}
