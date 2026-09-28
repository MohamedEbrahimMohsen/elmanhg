import { useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toSafeVideoUrl } from '../api/lessonValues';
import type { LessonValues } from '../schemas/lessonSchema';
import { RichTextViewer } from './RichTextViewer';

const sectionHeading = 'font-display text-h3 font-semibold';

export function LessonPreview() {
  const { t } = useTranslation('content');
  const values = useWatch<LessonValues>();
  const objectives = (values.objectives ?? []).filter((objective) => (objective.text ?? '').trim() !== '');
  const videoUrl = toSafeVideoUrl(values.videoUrl ?? '');

  return (
    <section
      aria-label={t('lessonEditor.preview.title')}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 className="font-display text-h2 font-bold">{values.name}</h2>
      <h3 className={sectionHeading}>{t('lessonEditor.fields.explanation')}</h3>
      <RichTextViewer html={values.explanation ?? ''} />
      <h3 className={sectionHeading}>{t('lessonEditor.fields.objectives')}</h3>
      <ol className="list-decimal ps-6">
        {objectives.map((objective) => (
          <li key={`${String(objective.objectiveId)}-${objective.text ?? ''}`}>{objective.text}</li>
        ))}
      </ol>
      <h3 className={sectionHeading}>{t('lessonEditor.fields.summary')}</h3>
      <RichTextViewer html={values.summary ?? ''} />
      {videoUrl ? (
        <a
          href={videoUrl}
          target="_blank"
          rel="noopener noreferrer"
          dir="ltr"
          className="text-ui text-accent underline"
        >
          {t('lessonEditor.preview.video')}
        </a>
      ) : null}
    </section>
  );
}
