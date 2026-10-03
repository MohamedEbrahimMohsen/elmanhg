import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useController, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import {
  acceptedSpreadsheetTypes,
  questionImportSchema,
  type QuestionImportValues,
} from '../schemas/questionImportSchema';

export interface QuestionImportFormProps {
  onCheck: (file: File) => Promise<void>;
  onFileChange: () => void;
}

const serverErrorFields = {
  QUESTION_IMPORT_FILE_REQUIRED: 'file',
  QUESTION_IMPORT_FILE_TYPE_INVALID: 'file',
  QUESTION_IMPORT_FILE_TOO_LARGE: 'file',
  SPREADSHEET_UNREADABLE: 'file',
  QUESTION_IMPORT_EMPTY: 'file',
  QUESTION_IMPORT_TOO_MANY_ROWS: 'file',
} as const;

export function QuestionImportForm({ onCheck, onFileChange }: QuestionImportFormProps) {
  const { t } = useTranslation('questions');
  const form = useForm<QuestionImportValues>({ resolver: zodResolver(questionImportSchema) });
  const {
    field: { ref, onChange, onBlur },
    fieldState,
  } = useController<QuestionImportValues, 'file'>({ name: 'file', control: form.control });
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  return (
    <section className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold">{t('import.form.title')}</h2>
      <Form
        form={form}
        serverErrorFields={serverErrorFields}
        onSubmit={async (values) => {
          await onCheck(values.file);
        }}
      >
        <div className="flex flex-col gap-1.5">
          <label htmlFor={id} className="text-caption text-text-muted">
            {t('import.form.file')}
          </label>
          <input
            id={id}
            type="file"
            accept={acceptedSpreadsheetTypes}
            aria-invalid={fieldState.invalid}
            aria-describedby={fieldState.error ? `${hintId} ${errorId}` : hintId}
            onChange={(event) => {
              onChange(event.target.files?.[0]);
              onFileChange();
            }}
            onBlur={onBlur}
            ref={ref}
            className="min-h-11 text-ui"
          />
          <p id={hintId} className="text-caption text-text-muted">
            {t('import.form.hint')}
          </p>
          {fieldState.error ? (
            <p id={errorId} className="text-caption text-danger">
              {t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}
            </p>
          ) : null}
        </div>
        <FormRootError />
        <div>
          <SubmitButton>{t('import.form.check')}</SubmitButton>
        </div>
      </Form>
    </section>
  );
}
