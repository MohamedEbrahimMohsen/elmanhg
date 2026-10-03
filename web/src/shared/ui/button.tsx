import type { ComponentProps } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { Slot } from 'radix-ui';
import { cn } from '@/shared/lib/utils';

const accentFillClassName =
  'bg-accent text-surface hover:not-disabled:not-active:bg-accent-hover active:bg-accent-pressed';

export const buttonVariants = cva(
  'inline-flex shrink-0 items-center justify-center gap-2 rounded-full font-sans text-ui font-semibold whitespace-nowrap transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45',
  {
    variants: {
      variant: {
        primary: accentFillClassName,
        accent: accentFillClassName,
        secondary:
          'border border-border-strong bg-surface text-text hover:not-disabled:not-aria-pressed:bg-soft active:bg-soft aria-pressed:border-accent aria-pressed:bg-accent-soft aria-pressed:text-accent',
        danger: 'border border-border-strong bg-surface text-danger hover:not-disabled:bg-danger-soft',
        ghost: 'bg-transparent text-accent hover:not-disabled:bg-accent-soft',
      },
      size: {
        default: 'min-h-11 px-4.5',
        sm: 'min-h-9 px-3',
        icon: 'size-11',
      },
    },
    defaultVariants: { variant: 'primary', size: 'default' },
  },
);

export type ButtonProps = ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean };

export function Button({ className, variant, size, asChild = false, type, ...props }: ButtonProps) {
  const Comp = asChild ? Slot.Root : 'button';

  return (
    <Comp
      data-slot="button"
      className={cn(buttonVariants({ variant, size }), className)}
      {...(asChild ? {} : { type: type ?? 'button' })}
      {...props}
    />
  );
}
