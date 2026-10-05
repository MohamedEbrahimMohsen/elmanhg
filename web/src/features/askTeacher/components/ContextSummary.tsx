import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { TeacherThreadContextResult } from '@/shared/api/generated/model';

export interface ContextSummaryProps {
  context: TeacherThreadContextResult;
}

export function ContextSummary({ context }: ContextSummaryProps) {
  const { t } = useTranslation('askTeacher');

  return (
    <div className="flex flex-col gap-2 rounded-md border border-border bg-soft px-3.5 py-3">
      <p className="text-caption text-text-muted">{t('context.label')}</p>
      <p className="text-ui font-bold text-text">
        {t('context.path', { subject: context.subjectName, unit: context.unitName, lesson: context.lessonName })}
      </p>
      {context.questionStem ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-bold text-text">{t('context.question')}</p>
          <div className="text-ui text-text">
            <RichTextViewer html={context.questionStem} />
          </div>
        </div>
      ) : null}
    </div>
  );
}
