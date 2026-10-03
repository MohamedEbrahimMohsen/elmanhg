import { useId } from 'react';
import { useTranslation } from 'react-i18next';

export interface MultiExamSizePickerProps {
  sizes: number[];
  value: number;
  onChange: (size: number) => void;
}

export function MultiExamSizePicker({ sizes, value, onChange }: MultiExamSizePickerProps) {
  const { t } = useTranslation('exam');
  const name = useId();

  return (
    <fieldset className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <legend className="px-1 text-ui font-semibold text-text">{t('multi.size')}</legend>
      <div className="flex flex-wrap gap-4">
        {sizes.map((size) => (
          <label key={size} className="flex min-h-11 items-center gap-2 text-ui text-text">
            <input
              type="radio"
              name={name}
              className="size-5 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
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
