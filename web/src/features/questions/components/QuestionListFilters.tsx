import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { useGetTeachers } from '@/shared/api/generated/teachers/teachers';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { questionTypes, validationStatuses } from '../api/questionOptions';
import { questionListFiltersSchema, type QuestionListFiltersValues } from '../schemas/questionListFiltersSchema';
import type { QuestionListSearch } from '../schemas/questionListSearchSchema';
import { SelectField } from './SelectField';

export interface QuestionListFiltersProps {
  search: QuestionListSearch;
  onApply: (values: QuestionListFiltersValues) => void;
  onClear: () => void;
}

export function QuestionListFilters({ search, onApply, onClear }: QuestionListFiltersProps) {
  const { t } = useTranslation('questions');
  const { data: subjects = [] } = useGetSubjects();
  const { data: teachers = [] } = useGetTeachers();
  const form = useForm<QuestionListFiltersValues>({
    resolver: zodResolver(questionListFiltersSchema),
    defaultValues: {
      status: search.status ?? '',
      type: search.type ?? '',
      subjectId: search.subjectId ?? '',
      teacherId: search.teacherId ?? '',
      minVersion: search.minVersion === undefined ? '' : String(search.minVersion),
      rejectionReason: search.rejectionReason ?? '',
    },
  });
  const all = t('list.filters.all');

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <SelectField<QuestionListFiltersValues>
          name="status"
          label={t('list.filters.status')}
          placeholder={all}
          options={validationStatuses.map((value) => ({ value, label: t(`statuses.${value}`) }))}
        />
        <SelectField<QuestionListFiltersValues>
          name="type"
          label={t('list.filters.type')}
          placeholder={all}
          options={questionTypes.map((value) => ({ value, label: t(`types.${value}`) }))}
        />
        <SelectField<QuestionListFiltersValues>
          name="subjectId"
          label={t('list.filters.subject')}
          placeholder={all}
          options={subjects.map((subject) => ({ value: subject.id, label: subject.name }))}
        />
        <SelectField<QuestionListFiltersValues>
          name="teacherId"
          label={t('list.filters.teacher')}
          placeholder={all}
          options={teachers.map((teacher) => ({ value: teacher.id, label: teacher.displayName }))}
        />
        <TextField<QuestionListFiltersValues> name="minVersion" label={t('list.filters.minVersion')} dir="ltr" />
        <TextField<QuestionListFiltersValues> name="rejectionReason" label={t('list.filters.rejectionReason')} />
      </div>
      <div className="flex flex-wrap gap-3">
        <SubmitButton>{t('list.filters.apply')}</SubmitButton>
        <Button variant="ghost" onClick={onClear}>
          {t('list.filters.clear')}
        </Button>
      </div>
    </Form>
  );
}
