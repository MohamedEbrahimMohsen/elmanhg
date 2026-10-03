import { useId } from 'react';
import { useController } from 'react-hook-form';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import type { PaymentLogFiltersValues } from '../schemas/paymentLogFiltersSchema';

export interface PaymentLogSelectFieldProps {
  name: 'status' | 'plan';
  label: string;
  allLabel: string;
  options: { value: string; label: string }[];
}

export function PaymentLogSelectField({ name, label, allLabel, options }: PaymentLogSelectFieldProps) {
  const id = useId();
  const {
    field: { ref, name: fieldName, value, onChange, onBlur },
  } = useController<PaymentLogFiltersValues, 'status' | 'plan'>({ name });

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select id={id} ref={ref} name={fieldName} value={value} onChange={onChange} onBlur={onBlur}>
        <option value="">{allLabel}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>
    </div>
  );
}
