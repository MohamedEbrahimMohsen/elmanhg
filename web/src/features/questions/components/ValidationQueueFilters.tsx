import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { ValidationQueueFiltersResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { questionDifficulties, questionTypes, validationAgeFilters } from '../api/questionOptions';
import {
  validationQueueFiltersSchema,
  type ValidationQueueFiltersValues,
} from '../schemas/validationQueueFiltersSchema';
import type { ValidationQueueSearch } from '../schemas/validationQueueSearchSchema';
import { SelectField } from './SelectField';

const ageField = validationQueueFiltersSchema.shape.minAgeDays.catch('');

export interface ValidationQueueFiltersProps {
  search: ValidationQueueSearch;
  filters: ValidationQueueFiltersResult | undefined;
  onApply: (values: ValidationQueueFiltersValues) => void;
  onClear: () => void;
}

export function ValidationQueueFilters({ search, filters, onApply, onClear }: ValidationQueueFiltersProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const form = useForm<ValidationQueueFiltersValues>({
    resolver: zodResolver(validationQueueFiltersSchema),
    defaultValues: {
      unitId: search.unitId ?? '',
      lessonId: search.lessonId ?? '',
      type: search.type ?? '',
      difficulty: search.difficulty ?? '',
      minAgeDays: ageField.parse(String(search.minAgeDays ?? '')),
    },
  });
  const unitId = useWatch({ control: form.control, name: 'unitId' });
  const lessons = (filters?.lessons ?? []).filter((lesson) => unitId === '' || lesson.unitId === unitId);
  const all = t('validation.filters.all');

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <SelectField<ValidationQueueFiltersValues>
          name="unitId"
          label={t('validation.filters.unit')}
          placeholder={all}
          options={(filters?.units ?? []).map((unit) => ({ value: unit.id, label: unit.name }))}
        />
        <SelectField<ValidationQueueFiltersValues>
          name="lessonId"
          label={t('validation.filters.lesson')}
          placeholder={all}
          options={lessons.map((lesson) => ({ value: lesson.id, label: lesson.name }))}
        />
        <SelectField<ValidationQueueFiltersValues>
          name="type"
          label={t('validation.filters.type')}
          placeholder={all}
          options={questionTypes.map((value) => ({ value, label: t(`types.${value}`) }))}
        />
        <SelectField<ValidationQueueFiltersValues>
          name="difficulty"
          label={t('validation.filters.difficulty')}
          placeholder={all}
          options={questionDifficulties.map((value) => ({ value, label: t(`difficulties.${value}`) }))}
        />
        <SelectField<ValidationQueueFiltersValues>
          name="minAgeDays"
          label={t('validation.filters.age')}
          placeholder={all}
          options={validationAgeFilters.map((days) => ({
            value: String(days),
            label: t('validation.filters.ageOption', { count: days, formattedCount: formatNumber(days, lng) }),
          }))}
        />
      </div>
      <div className="flex flex-wrap gap-3">
        <SubmitButton>{t('validation.filters.apply')}</SubmitButton>
        <Button variant="ghost" onClick={onClear}>
          {t('validation.filters.clear')}
        </Button>
      </div>
    </Form>
  );
}
