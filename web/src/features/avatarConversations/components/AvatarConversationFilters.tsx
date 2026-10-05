import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import {
  avatarConversationFiltersSchema,
  type AvatarConversationFiltersValues,
} from '../schemas/avatarConversationFiltersSchema';
import type { AvatarConversationSearch } from '../schemas/avatarConversationSearchSchema';
import { EntryPointField } from './EntryPointField';

export interface AvatarConversationFiltersProps {
  search: AvatarConversationSearch;
  onApply: (values: AvatarConversationFiltersValues) => void;
  onClear: () => void;
}

export function AvatarConversationFilters({ search, onApply, onClear }: AvatarConversationFiltersProps) {
  const { t } = useTranslation('avatarConversations');
  const form = useForm<AvatarConversationFiltersValues>({
    resolver: zodResolver(avatarConversationFiltersSchema),
    defaultValues: {
      search: search.search ?? '',
      entryPoint: search.entryPoint ?? '',
      from: search.from ?? '',
      to: search.to ?? '',
    },
  });

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <TextField<AvatarConversationFiltersValues>
          name="search"
          label={t('filters.search')}
          description={t('filters.searchHint')}
        />
        <EntryPointField />
        <TextField<AvatarConversationFiltersValues> name="from" label={t('filters.from')} type="date" />
        <TextField<AvatarConversationFiltersValues> name="to" label={t('filters.to')} type="date" />
      </div>
      <div className="flex flex-wrap gap-3">
        <SubmitButton variant="secondary">{t('filters.apply')}</SubmitButton>
        <Button variant="ghost" onClick={onClear}>
          {t('filters.clear')}
        </Button>
      </div>
    </Form>
  );
}
