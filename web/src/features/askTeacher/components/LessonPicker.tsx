import { useId, useState } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMasteryOverview, useGetSubjectMastery } from '@/shared/api/generated/mastery/mastery';
import { Label } from '@/shared/ui/label';
import type { AskTeacherFormValues } from '../schemas/askTeacherFormSchema';

const selectClassName =
  'h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger';

export function LessonPicker() {
  const { t } = useTranslation('askTeacher');
  const { t: tCommon } = useTranslation();
  const subjectFieldId = useId();
  const lessonFieldId = useId();
  const errorId = `${lessonFieldId}-error`;
  const [subjectId, setSubjectId] = useState('');
  const overview = useGetMasteryOverview();
  const subject = useGetSubjectMastery(subjectId, { query: { enabled: subjectId !== '' } });
  const {
    field: { ref, name, value, onChange, onBlur },
    fieldState,
  } = useController<AskTeacherFormValues, 'lessonId'>({ name: 'lessonId' });

  if (overview.isPending) {
    return <ContentListSkeleton label={t('context.loading')} />;
  }
  if (overview.isError || subject.isError) {
    const failed = overview.isError ? overview : subject;
    return (
      <ContentErrorState
        title={t('context.errorTitle')}
        error={failed.error}
        onRetry={() => {
          void failed.refetch();
        }}
      />
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={subjectFieldId}>{t('picker.subject')}</Label>
        <select
          id={subjectFieldId}
          value={subjectId}
          onChange={(event) => {
            setSubjectId(event.target.value);
            onChange('');
          }}
          className={selectClassName}
        >
          <option value="">{t('picker.chooseSubject')}</option>
          {overview.data.subjects.map((item) => (
            <option key={item.subjectId} value={item.subjectId}>
              {item.name}
            </option>
          ))}
        </select>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={lessonFieldId}>{t('picker.lesson')}</Label>
        <select
          id={lessonFieldId}
          ref={ref}
          name={name}
          value={value}
          onChange={onChange}
          onBlur={onBlur}
          disabled={subjectId === '' || subject.isPending}
          aria-invalid={fieldState.invalid}
          aria-describedby={fieldState.error ? errorId : undefined}
          className={selectClassName}
        >
          <option value="">{t('picker.chooseLesson')}</option>
          {subject.data?.units.map((unit) => (
            <optgroup key={unit.unitId} label={unit.name}>
              {unit.lessons.map((lesson) => (
                <option key={lesson.lessonId} value={lesson.lessonId}>
                  {lesson.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        {fieldState.error ? (
          <p id={errorId} className="text-caption text-danger">
            {tCommon([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'])}
          </p>
        ) : null}
      </div>
    </div>
  );
}
