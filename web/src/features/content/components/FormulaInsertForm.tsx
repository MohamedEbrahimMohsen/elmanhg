import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { formulaSchema, type FormulaValues } from '../schemas/formulaSchema';

export interface FormulaInsertFormProps {
  onInsert: (values: FormulaValues) => void;
  onCancel: () => void;
}

export function FormulaInsertForm({ onInsert, onCancel }: FormulaInsertFormProps) {
  const { t } = useTranslation('content');
  const form = useForm<FormulaValues>({
    resolver: zodResolver(formulaSchema),
    defaultValues: { latex: '', block: false },
  });

  return (
    <Form form={form} onSubmit={onInsert}>
      <TextField<FormulaValues> name="latex" label={t('lessonEditor.formula.latex')} dir="ltr" />
      <label className="flex items-center gap-2 text-ui">
        <input type="checkbox" {...form.register('block')} className="size-5 accent-accent" />
        {t('lessonEditor.formula.block')}
      </label>
      <div className="flex flex-wrap gap-2">
        <SubmitButton>{t('lessonEditor.formula.insert')}</SubmitButton>
        <Button variant="secondary" onClick={onCancel}>
          {t('actions.cancel')}
        </Button>
      </div>
    </Form>
  );
}
