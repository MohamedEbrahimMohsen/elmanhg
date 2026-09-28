import type { ComponentProps } from 'react';
import { Dialog as DialogPrimitive } from 'radix-ui';
import { cn } from '@/shared/lib/utils';

export const Dialog = DialogPrimitive.Root;

export interface DialogContentProps extends ComponentProps<typeof DialogPrimitive.Content> {
  title: string;
}

export function DialogContent({ title, children, className, ...props }: DialogContentProps) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 bg-overlay" />
      <DialogPrimitive.Content
        aria-describedby={undefined}
        className={cn(
          'fixed inset-0 m-auto flex h-fit w-full max-w-105 flex-col gap-4 rounded-lg bg-surface p-5 shadow-2 focus-visible:outline-hidden',
          className,
        )}
        {...props}
      >
        <DialogPrimitive.Title className="font-display text-h3 font-semibold">{title}</DialogPrimitive.Title>
        {children}
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}
