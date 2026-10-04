import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useGetMasteryOverview } from '@/shared/api/generated/mastery/mastery';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import type { AvatarContextInput } from '../api/avatarContext';
import { globalContext } from '../hooks/assistantReducer';
import { useAssistantLessonOptions } from '../hooks/useAssistantLessonOptions';

export interface AssistantContextPickerProps {
  context: AvatarContextInput;
  onChange: (context: AvatarContextInput) => void;
}

export function AssistantContextPicker({ context, onChange }: AssistantContextPickerProps) {
  const { t } = useTranslation('assistant');
  const [subjectId, setSubjectId] = useState('');
  const overview = useGetMasteryOverview();
  const lessons = useAssistantLessonOptions(subjectId);
  const hintId = useId();
  const subjectFieldId = useId();
  const lessonFieldId = useId();
  const lessonName = (id: string) =>
    lessons.groups.flatMap((group) => group.lessons).find((lesson) => lesson.id === id)?.name;

  if (overview.isError || lessons.isError) {
    return (
      <div className="flex flex-col items-start gap-2">
        <p role="alert" className="text-ui text-danger">
          {t('picker.error')}
        </p>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => {
            if (overview.isError) void overview.refetch();
            else lessons.retry();
          }}
        >
          {t('avatar:panel.retry')}
        </Button>
      </div>
    );
  }

  if (overview.isPending) {
    return (
      <p role="status" aria-busy="true" className="text-caption text-text-muted">
        {t('picker.loading')}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="grid gap-3 md:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={subjectFieldId}>{t('picker.subject')}</Label>
          <Select
            id={subjectFieldId}
            value={subjectId}
            aria-describedby={hintId}
            onChange={(event) => {
              setSubjectId(event.target.value);
              onChange(globalContext);
            }}
          >
            <option value="">{t('picker.general')}</option>
            {overview.data.subjects.map((subject) => (
              <option key={subject.subjectId} value={subject.subjectId}>
                {subject.name}
              </option>
            ))}
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={lessonFieldId}>{t('picker.lesson')}</Label>
          <Select
            id={lessonFieldId}
            value={context.entryPoint === 'Lesson' ? (context.lessonId ?? '') : ''}
            disabled={subjectId === '' || lessons.isPending}
            aria-describedby={hintId}
            onChange={(event) => {
              const id = event.target.value;
              const name = lessonName(id);
              onChange(
                id === '' || name === undefined ? globalContext : { entryPoint: 'Lesson', lessonId: id, title: name },
              );
            }}
          >
            <option value="">{t('picker.noLesson')}</option>
            {lessons.groups.map((group) => (
              <optgroup key={group.unitId} label={group.unitName}>
                {group.lessons.map(({ id, name, isLocked }) => (
                  <option key={id} value={id} disabled={isLocked}>
                    {isLocked ? t('picker.locked', { name }) : name}
                  </option>
                ))}
              </optgroup>
            ))}
          </Select>
          {subjectId !== '' && lessons.isPending ? (
            <p role="status" aria-busy="true" className="text-caption text-text-muted">
              {t('picker.loading')}
            </p>
          ) : null}
        </div>
      </div>
      <p id={hintId} className="text-caption text-text-muted">
        {t('picker.hint')}
      </p>
    </div>
  );
}
