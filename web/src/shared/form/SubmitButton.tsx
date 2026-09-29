import type { ReactNode } from 'react';
import { useFormState } from 'react-hook-form';
import { Button } from '@/shared/ui/button';

export interface SubmitButtonProps {
  children: ReactNode;
  variant?: 'primary' | 'accent';
  disabled?: boolean;
}

export function SubmitButton({ children, variant = 'primary', disabled = false }: SubmitButtonProps) {
  const { isSubmitting } = useFormState();

  return (
    <Button type="submit" variant={variant} disabled={isSubmitting || disabled} aria-busy={isSubmitting}>
      {children}
    </Button>
  );
}
