import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { questionTypes } from '@/features/questions';
import type { ExamTypeCountResult } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Input } from '@/shared/ui/input';
import { countsFromValues } from '../api/blueprintValues';
import type { ExamBlueprintValues } from '../schemas/examBlueprintSchema';

export interface TypeCountsTableProps {
  available: readonly ExamTypeCountResult[];
}

const cellClass = 'border-b border-border px-3 py-2 text-start';

export function TypeCountsTable({ available }: TypeCountsTableProps) {
  const { t } = useTranslation('blueprints');
  const {
    register,
    control,
    getValues,
    formState: { errors },
  } = useFormContext<ExamBlueprintValues>();
  const counts = useWatch({ control, name: 'counts' });
  const required = countsFromValues({ ...getValues(), counts });
  const countOf = (entries: readonly ExamTypeCountResult[], type: string) =>
    Number(entries.find((entry) => entry.type === type)?.count ?? 0);
  const total = required.reduce((sum, entry) => sum + Number(entry.count), 0);
  const rowErrors = questionTypes.flatMap((type) => {
    const message = errors.counts?.[type]?.message;
    return message ? [{ type, message }] : [];
  });

  return (
    <div className="flex flex-col gap-1.5">
      <div className="overflow-x-auto">
        <table className="w-full text-ui">
          <thead>
            <tr className="text-caption text-text-muted">
              <th scope="col" className={cellClass}>
                {t('editor.type')}
              </th>
              <th scope="col" className={cellClass}>
                {t('editor.required')}
              </th>
              <th scope="col" className={cellClass}>
                {t('editor.available')}
              </th>
            </tr>
          </thead>
          <tbody>
            {questionTypes.map((type) => {
              const typeLabel = t(`questions:types.${type}`);
              const isShort = countOf(required, type) > countOf(available, type);
              return (
                <tr key={type} className={cn(isShort && 'bg-warning-soft')}>
                  <th scope="row" className={cn(cellClass, 'font-normal')}>
                    {typeLabel}
                  </th>
                  <td className={cellClass}>
                    <Input
                      dir="ltr"
                      inputMode="numeric"
                      aria-label={t('editor.countFor', { type: typeLabel })}
                      aria-invalid={errors.counts?.[type] ? true : undefined}
                      className="w-24"
                      {...register(`counts.${type}`)}
                    />
                  </td>
                  <td className={cellClass}>{t('editor.number', { value: countOf(available, type) })}</td>
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="font-semibold">
              <th scope="row" className={cellClass}>
                {t('editor.total')}
              </th>
              <td className={cellClass}>{t('editor.number', { value: total })}</td>
              <td className={cellClass} />
            </tr>
          </tfoot>
        </table>
      </div>
      {errors.counts?.message ? <p className="text-caption text-danger">{t(errors.counts.message)}</p> : null}
      {rowErrors.map(({ type, message }) => (
        <p key={type} className="text-caption text-danger">
          {t(`questions:types.${type}`)}: {t(message)}
        </p>
      ))}
    </div>
  );
}
