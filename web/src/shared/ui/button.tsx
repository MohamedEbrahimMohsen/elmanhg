import type { ComponentProps } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { Slot } from 'radix-ui';
import { cn } from '@/shared/lib/utils';

const actionFillClassName =
  'bg-action text-text hover:not-disabled:not-active:bg-action-hover active:bg-action-pressed';

export const buttonVariants = cva(
  'inline-flex shrink-0 items-center justify-center gap-2 rounded-pill font-sans text-label font-bold whitespace-nowrap transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45',
  {
    variants: {
      variant: {
        primary: actionFillClassName,
        secondary:
          'border-2 border-accent bg-transparent text-accent-text hover:not-disabled:not-aria-pressed:bg-soft active:bg-soft aria-pressed:bg-accent-soft',
        danger: 'border-2 border-danger bg-transparent text-danger hover:not-disabled:bg-danger-soft',
        ghost: 'bg-transparent text-text hover:not-disabled:bg-soft',
      },
      size: {
        default: 'min-h-11 px-5',
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
