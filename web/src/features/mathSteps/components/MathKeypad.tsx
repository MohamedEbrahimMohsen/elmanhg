import { ArrowLeft, ArrowRight, Delete, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { mathKeyRows, type MathKeyId } from '../api/mathKeys';

export interface MathKeypadProps {
  onKey: (keyId: MathKeyId) => void;
}

const keyIcons: Partial<Record<MathKeyId, LucideIcon>> = { left: ArrowLeft, right: ArrowRight, backspace: Delete };

export function MathKeypad({ onKey }: MathKeypadProps) {
  const { t } = useTranslation('mathSteps');

  return (
    <div
      role="group"
      aria-label={t('keypad.label')}
      dir="ltr"
      className="grid grid-cols-6 gap-1 rounded-md border border-border bg-soft p-2"
    >
      {mathKeyRows.flat().map((key) => {
        const Icon = keyIcons[key.id];
        return (
          <button
            type="button"
            key={key.id}
            aria-label={t(`keys.${key.id}`)}
            onPointerDown={(event) => {
              event.preventDefault();
            }}
            onClick={() => {
              onKey(key.id);
            }}
            className="flex min-h-11 items-center justify-center rounded-sm border border-border-strong bg-surface font-sans text-ui font-bold text-text hover:bg-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {Icon ? <Icon aria-hidden className="size-5" /> : (key.glyph ?? t('keypad.textGlyph'))}
          </button>
        );
      })}
    </div>
  );
}
