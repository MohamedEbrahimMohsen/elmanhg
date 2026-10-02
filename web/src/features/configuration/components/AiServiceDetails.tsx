import { useTranslation } from 'react-i18next';
import type { AiServiceConfigurationResult, AiServiceStatus } from '@/shared/api/generated/model';
import { SecretStatusList } from './SecretStatusList';

export interface AiServiceDetailsProps {
  status: AiServiceStatus;
  aiService: AiServiceConfigurationResult | null;
}

export function AiServiceDetails({ status, aiService }: AiServiceDetailsProps) {
  const { t } = useTranslation('configuration');

  if (status === 'NotUsed') {
    return <p className="text-caption text-text-muted">{t('ai.notUsed')}</p>;
  }
  if (status === 'Unreachable' || aiService === null) {
    return (
      <p role="alert" className="text-caption text-danger">
        {t('ai.unreachable')}
      </p>
    );
  }

  const rows = [
    { key: 'llm', value: aiService.llmProvider },
    { key: 'chat', value: aiService.chatModel },
    { key: 'essay', value: aiService.essayGradingModel },
    { key: 'math', value: aiService.mathStepGradingModel },
    { key: 'embeddings', value: `${aiService.embeddingProvider} · ${aiService.embeddingModel}` },
    { key: 'transcription', value: `${aiService.transcriptionProvider} · ${aiService.transcriptionModel}` },
  ];

  return (
    <div className="flex flex-col gap-3">
      <dl className="grid grid-cols-1 gap-2 md:grid-cols-2">
        {rows.map((row) => (
          <div key={row.key} className="flex flex-col">
            <dt className="text-caption text-text-muted">{t(`ai.${row.key}`)}</dt>
            <dd dir="ltr" className="text-start text-ui text-text">
              {row.value}
            </dd>
          </div>
        ))}
      </dl>
      <SecretStatusList secrets={aiService.secrets} />
    </div>
  );
}
