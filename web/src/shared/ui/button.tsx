import type { ComponentProps } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { Slot } from 'radix-ui';
import { cn } from '@/shared/lib/utils';

export const buttonVariants = cva(
  'inline-flex shrink-0 items-center justify-center gap-2 rounded-full font-sans text-ui font-semibold whitespace-nowrap transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:pointer-events-none disabled:opacity-45',
  {
    variants: {
      variant: {
        primary: 'bg-text text-surface hover:opacity-90',
        accent: 'bg-accent text-surface hover:opacity-90',
        secondary: 'border border-border-strong bg-surface text-text hover:bg-soft',
        danger: 'border border-danger bg-surface text-danger hover:bg-danger-soft',
        ghost: 'bg-transparent text-accent hover:bg-accent-soft',
      },
      size: {
        default: 'min-h-11 px-4.5',
        sm: 'min-h-9 px-3',
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
