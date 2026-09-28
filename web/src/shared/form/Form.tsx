import type { ReactNode } from 'react';
import { FormProvider, type FieldValues, type UseFormReturn } from 'react-hook-form';
import { cn } from '@/shared/lib/utils';
import { applyServerErrors, type ServerErrorFields } from './applyServerErrors';

export interface FormProps<TValues extends FieldValues, TTransformed extends FieldValues = TValues> {
  form: UseFormReturn<TValues, unknown, TTransformed>;
  onSubmit: (values: TTransformed) => Promise<void> | void;
  serverErrorFields?: ServerErrorFields<TValues>;
  children: ReactNode;
  className?: string;
}

export function Form<TValues extends FieldValues, TTransformed extends FieldValues = TValues>({
  form,
  onSubmit,
  serverErrorFields,
  children,
  className,
}: FormProps<TValues, TTransformed>) {
  const submit = async (values: TTransformed) => {
    try {
      await onSubmit(values);
    } catch (error) {
      applyServerErrors(form, error, serverErrorFields ?? {});
    }
  };

  return (
    <FormProvider {...form}>
      <form
        noValidate
        className={cn('flex flex-col gap-3', className)}
        onSubmit={(event) => {
          event.stopPropagation();
          void form.handleSubmit(submit)(event);
        }}
      >
        {children}
      </form>
    </FormProvider>
  );
}
