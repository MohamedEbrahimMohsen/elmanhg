import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { SubjectResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { trainingExportSources } from '../api/trainingExportParams';
import { useRequestTrainingExport } from '../hooks/useRequestTrainingExport';
import { trainingExportRequestSchema, type TrainingExportRequestValues } from '../schemas/trainingExportRequestSchema';
import { TrainingExportSelectField } from './TrainingExportSelectField';

export interface TrainingExportFormProps {
  subjects: SubjectResult[];
}

const defaultValues: TrainingExportRequestValues = { source: 'TeacherThreads', subjectId: '', from: '', to: '' };

const serverErrorFields = {
  TRAINING_EXPORT_DATE_RANGE_INVALID: 'to',
  TRAINING_EXPORT_DATE_RANGE_TOO_WIDE: 'to',
  TRAINING_EXPORT_SOURCE_INVALID: 'source',
  SUBJECT_NOT_FOUND: 'subjectId',
} as const;

export function TrainingExportForm({ subjects }: TrainingExportFormProps) {
  const { t } = useTranslation('trainingExport');
  const { request } = useRequestTrainingExport();
  const form = useForm<TrainingExportRequestValues>({
    resolver: zodResolver(trainingExportRequestSchema),
    defaultValues,
  });

  const submit = async (values: TrainingExportRequestValues) => {
    await request(values);
    form.reset(defaultValues);
  };

  return (
    <Form form={form} onSubmit={submit} serverErrorFields={serverErrorFields}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <TrainingExportSelectField
          name="source"
          label={t('form.source')}
          options={trainingExportSources.map((source) => ({ value: source, label: t(`source.${source}`) }))}
        />
        <TrainingExportSelectField
          name="subjectId"
          label={t('form.subject')}
          allLabel={t('form.allSubjects')}
          options={subjects.map((subject) => ({ value: subject.id, label: subject.name }))}
        />
        <TextField<TrainingExportRequestValues> name="from" label={t('form.from')} type="date" />
        <TextField<TrainingExportRequestValues> name="to" label={t('form.to')} type="date" />
      </div>
      <FormRootError />
      <div>
        <SubmitButton>{t('form.submit')}</SubmitButton>
      </div>
    </Form>
  );
}
