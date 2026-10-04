import type { ReactNode } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { ExamBlueprintInput, ExamTypeCountResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { countsFromValues, findShortfall, toBlueprintInput } from '../api/blueprintValues';
import { examBlueprintSchema, type ExamBlueprintValues } from '../schemas/examBlueprintSchema';
import { DifficultyMixFields } from './DifficultyMixFields';
import { ShortfallNotice } from './ShortfallNotice';
import { TypeCountsTable } from './TypeCountsTable';

const serverErrorFields = {
  EXAM_BLUEPRINT_PASS_MARK_INVALID: 'passMark',
  EXAM_BLUEPRINT_TIME_LIMIT_INVALID: 'timeLimitMinutes',
} as const;

export interface BlueprintEditorProps {
  title: string;
  caption?: string | undefined;
  defaults: ExamBlueprintValues;
  available: readonly ExamTypeCountResult[];
  onSave: (input: ExamBlueprintInput) => Promise<void>;
  onCancel?: (() => void) | undefined;
  actions?: ReactNode;
}

export function BlueprintEditor({
  title,
  caption,
  defaults,
  available,
  onSave,
  onCancel,
  actions,
}: BlueprintEditorProps) {
  const { t } = useTranslation('blueprints');
  const form = useForm<ExamBlueprintValues>({
    resolver: zodResolver(examBlueprintSchema),
    defaultValues: defaults,
    mode: 'onBlur',
  });
  const counts = useWatch({ control: form.control, name: 'counts' });
  const shortfalls = findShortfall(countsFromValues({ ...form.getValues(), counts }), available);

  const submit = async (values: ExamBlueprintValues) => {
    if (findShortfall(countsFromValues(values), available).length > 0) {
      form.setError('root.server', { type: 'client', message: 'errors.EXAM_BLUEPRINT_SHORTFALL' });
      return;
    }
    await onSave(toBlueprintInput(values));
  };

  return (
    <section aria-label={title} className="rounded-lg border border-border bg-surface p-4 shadow-1">
      <Form form={form} serverErrorFields={serverErrorFields} onSubmit={submit}>
        <div className="flex flex-col gap-1">
          <h2 className="font-display text-h3 font-bold">{title}</h2>
          {caption ? <p className="text-caption text-text-muted">{caption}</p> : null}
        </div>
        <TypeCountsTable available={available} />
        <div className="grid gap-3 md:grid-cols-2">
          <TextField<ExamBlueprintValues>
            name="timeLimitMinutes"
            label={t('editor.timeLimit')}
            description={t('editor.timeLimitHint')}
            dir="ltr"
          />
          <TextField<ExamBlueprintValues> name="passMark" label={t('editor.passMark')} dir="ltr" />
        </div>
        <DifficultyMixFields />
        <ShortfallNotice title={t('editor.currentShortfall')} shortfalls={shortfalls} />
        <FormRootError />
        <div className="flex flex-wrap items-center gap-2">
          {actions}
          {onCancel ? (
            <Button variant="secondary" onClick={onCancel}>
              {t('editor.cancel')}
            </Button>
          ) : null}
          <SubmitButton variant="secondary">{t('editor.save')}</SubmitButton>
        </div>
      </Form>
    </section>
  );
}
