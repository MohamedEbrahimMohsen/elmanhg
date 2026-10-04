import { useTranslation } from 'react-i18next';
import type { DiagramItemMark } from '../api/diagramReview';
import { DiagramItemChip, type DiagramItemChipProps } from './DiagramItemChip';

type ChipProps = Omit<DiagramItemChipProps, 'item' | 'interactive' | 'mark'>;

export interface DiagramItemBankProps {
  items: { id: string; text: string }[];
  interactive: boolean;
  marks?: Record<string, DiagramItemMark> | undefined;
  chipProps: (itemId: string) => ChipProps;
}

export function DiagramItemBank({ items, interactive, marks, chipProps }: DiagramItemBankProps) {
  const { t } = useTranslation('diagramStudent');

  return (
    <section aria-label={t('bank')} data-diagram-bank className="flex flex-col gap-2">
      <h3 className="text-h3 font-bold">{t('bank')}</h3>
      {items.length === 0 ? (
        <p className="text-caption text-text-muted">{t('bankEmpty')}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {items.map((item) => (
            <li key={item.id}>
              <DiagramItemChip item={item} interactive={interactive} mark={marks?.[item.id]} {...chipProps(item.id)} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
