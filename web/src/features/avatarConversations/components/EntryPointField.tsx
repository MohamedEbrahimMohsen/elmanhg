import { useId } from 'react';
import { useController } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import type { AvatarConversationFiltersValues } from '../schemas/avatarConversationFiltersSchema';
import { avatarEntryPoints } from '../schemas/avatarConversationSearchSchema';

export function EntryPointField() {
  const { t } = useTranslation('avatarConversations');
  const id = useId();
  const {
    field: { ref, name, value, onChange, onBlur },
  } = useController<AvatarConversationFiltersValues, 'entryPoint'>({ name: 'entryPoint' });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('filters.entryPoint')}</Label>
      <select
        id={id}
        ref={ref}
        name={name}
        value={value}
        onChange={onChange}
        onBlur={onBlur}
        className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <option value="">{t('filters.allEntryPoints')}</option>
        {avatarEntryPoints.map((entryPoint) => (
          <option key={entryPoint} value={entryPoint}>
            {t(`entryPoint.${entryPoint}`)}
          </option>
        ))}
      </select>
    </div>
  );
}
