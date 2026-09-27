import { useFormState } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

export function FormRootError() {
  const { t } = useTranslation();
  const { errors } = useFormState();
  const message = errors.root?.server?.message;

  if (!message) {
    return null;
  }

  return (
    <p role="alert" className="rounded-md border border-danger bg-danger-soft px-3.5 py-3 text-ui text-danger">
      {t([message, 'errors.UNHANDLED_EXCEPTION'])}
    </p>
  );
}
