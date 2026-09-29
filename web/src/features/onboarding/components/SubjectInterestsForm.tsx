import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from '@tanstack/react-router';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { SubjectInterestsResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { Button } from '@/shared/ui/button';
import { useSubjectInterestsSave } from '../hooks/useSubjectInterestsSave';
import { subjectInterestsSchema, type SubjectInterestsValues } from '../schemas/subjectInterestsSchema';

export interface SubjectInterestsFormProps {
  interests: SubjectInterestsResult;
}

export function SubjectInterestsForm({ interests }: SubjectInterestsFormProps) {
  const { t } = useTranslation('onboarding');
  const errorId = useId();
  const { save, skip, isPending } = useSubjectInterestsSave(interests.needsOnboarding);
  const form = useForm<SubjectInterestsValues>({
    resolver: zodResolver(subjectInterestsSchema),
    defaultValues: {
      subjectIds: interests.subjects.filter((subject) => subject.isSelected).map((subject) => subject.subjectId),
    },
  });
  const error = form.formState.errors.subjectIds?.message;

  return (
    <Form form={form} onSubmit={(values) => save(values.subjectIds)}>
      <FormRootError />
      <fieldset aria-describedby={error ? errorId : undefined} className="flex flex-col gap-2">
        <legend className="text-caption text-text-muted">{t('form.legend')}</legend>
        <div className="grid grid-cols-1 gap-2 md:grid-cols-2">
          {interests.subjects.map((subject) => (
            <label
              key={subject.subjectId}
              className="flex min-h-12 items-center gap-3 rounded-md border border-border-strong bg-surface px-3.5 py-3 text-ui has-checked:border-text has-checked:bg-soft"
            >
              <input
                type="checkbox"
                value={subject.subjectId}
                {...form.register('subjectIds')}
                className="size-5 accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
              />
              {subject.name}
            </label>
          ))}
        </div>
        {error ? (
          <p id={errorId} role="alert" className="text-caption text-danger">
            {t([error, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
        ) : null}
      </fieldset>
      <div className="flex flex-wrap gap-2">
        <SubmitButton>{t('actions.continue')}</SubmitButton>
        {interests.needsOnboarding ? (
          <Button variant="secondary" disabled={isPending} onClick={skip}>
            {t('actions.skip')}
          </Button>
        ) : (
          <Button asChild variant="secondary">
            <Link to="/student">{t('actions.backHome')}</Link>
          </Button>
        )}
      </div>
    </Form>
  );
}
