import { useTranslation } from 'react-i18next';
import type { AvatarMessageContext } from '@/shared/api/generated/model';

export interface AvatarLoggedContextProps {
  context: AvatarMessageContext;
}

export function AvatarLoggedContext({ context }: AvatarLoggedContextProps) {
  const { t } = useTranslation('avatarConversations');
  const { bundle, sources } = context;
  const fields = [
    { key: 'subject', value: bundle.subject?.name },
    { key: 'unit', value: bundle.unit?.name },
    { key: 'lesson', value: bundle.lesson?.name },
    { key: 'explanation', value: bundle.lesson?.explanation },
    { key: 'summaryText', value: bundle.lesson?.summary },
    { key: 'question', value: bundle.question?.stem },
    { key: 'studentAnswer', value: bundle.question?.studentAnswer },
    { key: 'correctAnswer', value: bundle.question?.correctAnswer },
    { key: 'questionExplanation', value: bundle.question?.explanation },
    { key: 'subjects', value: bundle.subjects.length > 0 ? bundle.subjects.join('، ') : undefined },
  ].filter((field) => field.value !== undefined && field.value !== null && field.value !== '');
  const objectives = bundle.lesson?.objectives ?? [];

  return (
    <details className="rounded-md border border-border p-3 text-caption">
      <summary className="cursor-pointer text-accent">{t('context.summary')}</summary>
      <dl className="mt-3 flex flex-col gap-2">
        {fields.map((field) => (
          <div key={field.key}>
            <dt className="font-semibold text-text-muted">{t(`context.${field.key}`)}</dt>
            <dd className="whitespace-pre-wrap text-text">{field.value}</dd>
          </div>
        ))}
        {objectives.length > 0 ? (
          <div>
            <dt className="font-semibold text-text-muted">{t('context.objectives')}</dt>
            <dd>
              <ul className="list-disc ps-5">
                {objectives.map((objective) => (
                  <li key={objective}>{objective}</li>
                ))}
              </ul>
            </dd>
          </div>
        ) : null}
        <div>
          <dt className="font-semibold text-text-muted">{t('context.sources')}</dt>
          <dd>
            {sources.length === 0 ? (
              t('context.none')
            ) : (
              <ul className="flex flex-col gap-2">
                {sources.map((source) => (
                  <li key={source.reference}>
                    <p className="font-semibold text-text">{source.title}</p>
                    <p className="whitespace-pre-wrap text-text">{source.content}</p>
                  </li>
                ))}
              </ul>
            )}
          </dd>
        </div>
      </dl>
    </details>
  );
}
