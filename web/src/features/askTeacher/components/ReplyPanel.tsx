import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ReplyForm } from './ReplyForm';
import { VoiceReplyPanel } from './VoiceReplyPanel';

export interface ReplyPanelProps {
  threadId: string;
}

type ReplyKind = 'text' | 'voice';

const replyKinds: readonly ReplyKind[] = ['text', 'voice'];

export function ReplyPanel({ threadId }: ReplyPanelProps) {
  const { t } = useTranslation('askTeacher');
  const [kind, setKind] = useState<ReplyKind>('text');
  const name = useId();

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('inboxThread.replyTitle')}</h2>
      <fieldset className="flex flex-wrap gap-4">
        <legend className="sr-only">{t('replyKind.label')}</legend>
        {replyKinds.map((option) => (
          <label key={option} className="flex min-h-11 items-center gap-2 text-ui text-text">
            <input
              type="radio"
              name={name}
              className="size-5 accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
              checked={kind === option}
              onChange={() => {
                setKind(option);
              }}
            />
            {t(`replyKind.${option}`)}
          </label>
        ))}
      </fieldset>
      {kind === 'text' ? <ReplyForm threadId={threadId} /> : <VoiceReplyPanel threadId={threadId} />}
    </div>
  );
}
