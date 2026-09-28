import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { useQuestionImportTemplate } from '../hooks/useQuestionImportTemplate';

export function QuestionImportTemplateCard() {
  const { t } = useTranslation('questions');
  const { download, isPending } = useQuestionImportTemplate();

  return (
    <section className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold">{t('import.template.title')}</h2>
      <p className="text-caption text-text-muted">{t('import.template.description')}</p>
      <div>
        <Button variant="secondary" onClick={download} disabled={isPending} aria-busy={isPending}>
          {t('import.template.download')}
        </Button>
      </div>
    </section>
  );
}
