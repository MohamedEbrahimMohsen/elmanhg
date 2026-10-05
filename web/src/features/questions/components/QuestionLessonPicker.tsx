import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import type { PickerOption, QuestionLessonPickerState } from '../hooks/useQuestionLessonPicker';

export interface QuestionLessonPickerProps {
  picker: QuestionLessonPickerState;
}

export function QuestionLessonPicker({ picker }: QuestionLessonPickerProps) {
  const { t } = useTranslation('questions');
  const id = useId();

  if (picker.isError) {
    return <ContentErrorState title={t('list.add.errorTitle')} error={picker.error} onRetry={picker.retry} />;
  }
  if (picker.isLoadingSubjects) {
    return <ContentListSkeleton label={t('list.add.loading')} />;
  }

  const fields: {
    key: string;
    value: string;
    options: PickerOption[];
    disabled: boolean;
    onChange: (value: string) => void;
  }[] = [
    {
      key: 'subject',
      value: picker.subjectId,
      options: picker.subjects,
      disabled: false,
      onChange: picker.chooseSubject,
    },
    {
      key: 'unit',
      value: picker.unitId,
      options: picker.units,
      disabled: picker.subjectId === '' || picker.isLoadingUnits,
      onChange: picker.chooseUnit,
    },
    {
      key: 'lesson',
      value: picker.lessonId,
      options: picker.lessons,
      disabled: picker.unitId === '' || picker.isLoadingLessons || picker.hasNoLessons,
      onChange: picker.chooseLesson,
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      {fields.map((field) => (
        <div key={field.key} className="flex flex-col gap-1.5">
          <Label htmlFor={`${id}-${field.key}`}>{t(`list.add.${field.key}`)}</Label>
          <Select
            id={`${id}-${field.key}`}
            value={field.value}
            disabled={field.disabled}
            onChange={(event) => {
              field.onChange(event.target.value);
            }}
          >
            <option value="">{t(`list.add.choose.${field.key}`)}</option>
            {field.options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </Select>
        </div>
      ))}
      {picker.isLoadingUnits || picker.isLoadingLessons ? (
        <p role="status" aria-busy="true" className="text-caption text-text-muted">
          {t('list.add.loading')}
        </p>
      ) : null}
      {picker.hasNoLessons ? <p className="text-caption text-text-muted">{t('list.add.noLessons')}</p> : null}
    </div>
  );
}
