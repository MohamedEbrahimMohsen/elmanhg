import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { optionCardClassName, optionCardIdleClassName } from '@/shared/ui/optionCard';

export interface MultiExamSizePickerProps {
  sizes: number[];
  value: number;
  onChange: (size: number) => void;
}

export function MultiExamSizePicker({ sizes, value, onChange }: MultiExamSizePickerProps) {
  const { t } = useTranslation('exam');
  const name = useId();

  return (
    <fieldset className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <legend className="text-ui font-bold text-text">{t('multi.size')}</legend>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        {sizes.map((size) => (
          <label key={size} className={cn(optionCardClassName, optionCardIdleClassName)}>
            <input
              type="radio"
              name={name}
              className="size-4.5 shrink-0 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
              checked={size === value}
              onChange={() => {
                onChange(size);
              }}
            />
            {t('multi.sizeOption', { size })}
          </label>
        ))}
      </div>
    </fieldset>
  );
}
