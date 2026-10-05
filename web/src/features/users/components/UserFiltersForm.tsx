import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useController, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { userFiltersSchema, type UserFiltersValues } from '../schemas/userFiltersSchema';
import type { UsersSearch } from '../schemas/usersSearchSchema';

export interface UserFiltersFormProps {
  search: UsersSearch;
  onApply: (values: UserFiltersValues) => void;
  onClear: () => void;
}

function StatusSelect() {
  const { t } = useTranslation('users');
  const id = useId();
  const {
    field: { ref, name, value, onChange, onBlur },
  } = useController<UserFiltersValues, 'status'>({ name: 'status' });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('filters.status')}</Label>
      <Select id={id} ref={ref} name={name} value={value} onChange={onChange} onBlur={onBlur}>
        <option value="">{t('filters.all')}</option>
        <option value="Active">{t('status.Active')}</option>
        <option value="Suspended">{t('status.SuspendedStudent')}</option>
      </Select>
    </div>
  );
}

export function UserFiltersForm({ search, onApply, onClear }: UserFiltersFormProps) {
  const { t } = useTranslation('users');
  const form = useForm<UserFiltersValues>({
    resolver: zodResolver(userFiltersSchema),
    defaultValues: { q: search.q ?? '', status: search.status ?? '' },
  });

  return (
    <Form form={form} onSubmit={onApply}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <div className="md:col-span-2">
          <TextField<UserFiltersValues> name="q" label={t('filters.search')} description={t('filters.searchHint')} />
        </div>
        <StatusSelect />
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
