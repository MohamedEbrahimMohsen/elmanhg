import { useController, type FieldValues, type Path } from 'react-hook-form';

export interface CheckboxFieldProps<TValues extends FieldValues> {
  name: Path<TValues>;
  label: string;
}

export function CheckboxField<TValues extends FieldValues>({ name, label }: CheckboxFieldProps<TValues>) {
  const {
    field: { ref, name: fieldName, value, onChange, onBlur },
  } = useController<TValues>({ name });

  return (
    <label className="flex min-h-11 items-center gap-2.5 text-ui">
      <input
        type="checkbox"
        ref={ref}
        name={fieldName}
        checked={value === true}
        onChange={(event) => {
          onChange(event.target.checked);
        }}
        onBlur={onBlur}
        className="size-4.5 accent-text"
      />
      {label}
    </label>
  );
}
