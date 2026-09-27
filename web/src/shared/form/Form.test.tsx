import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { describe, expect, it } from 'vitest';
import { z } from 'zod';
import { ApiError } from '@/shared/lib/apiError';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import type { ServerErrorFields } from './applyServerErrors';
import { Form } from './Form';
import { FormRootError } from './FormRootError';
import { SubmitButton } from './SubmitButton';
import { TextField } from './TextField';

const testSchema = z.object({
  name: z.string().min(1, { error: 'validation.required' }),
  email: z.string().min(1, { error: 'validation.required' }),
});

type TestValues = z.infer<typeof testSchema>;

interface TestFormProps {
  onSubmit?: (values: TestValues) => Promise<void>;
  serverErrorFields?: ServerErrorFields<TestValues>;
}

function TestForm({ onSubmit = () => Promise.resolve(), serverErrorFields = {} }: TestFormProps) {
  const [saved, setSaved] = useState(false);
  const form = useForm<TestValues>({ resolver: zodResolver(testSchema), defaultValues: { name: '', email: '' } });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        await onSubmit(values);
        setSaved(true);
      }}
    >
      <FormRootError />
      <TextField<TestValues> name="name" label="Name" />
      <TextField<TestValues> name="email" label="Email" type="email" />
      <SubmitButton>Save</SubmitButton>
      {saved ? <p>Saved</p> : null}
    </Form>
  );
}

async function fillAndSave(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Name'), 'Ahmed');
  await user.type(screen.getByLabelText('Email'), 'ahmed@example.com');
  await user.click(screen.getByRole('button', { name: 'Save' }));
}

describe('Form base components', () => {
  it('shows the required error inline and marks the field invalid when submitted empty', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TestForm />);

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findAllByText('This field is required.')).toHaveLength(2);
    const nameInput = screen.getByLabelText('Name');
    expect(nameInput).toHaveAttribute('aria-invalid', 'true');
    expect(nameInput).toHaveAccessibleDescription('This field is required.');
  });

  it('focuses the first invalid field on submit', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TestForm />);

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByLabelText('Name')).toHaveFocus();
  });

  it('disables submit while submitting', async () => {
    const user = userEvent.setup();
    let resolveSubmit: () => void = () => undefined;
    const pending = new Promise<void>((resolve) => {
      resolveSubmit = resolve;
    });
    renderWithProviders(<TestForm onSubmit={() => pending} />);

    await fillAndSave(user);

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    resolveSubmit();
    expect(await screen.findByText('Saved')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
  });

  it('submits the parsed values when valid', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TestForm />);

    await fillAndSave(user);

    expect(await screen.findByText('Saved')).toBeInTheDocument();
  });

  it('shows a server error on the mapped field and focuses it', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <TestForm
        onSubmit={() => Promise.reject(new ApiError(422, ['VALIDATION_FAILED'], ''))}
        serverErrorFields={{ VALIDATION_FAILED: 'email' }}
      />,
    );

    await fillAndSave(user);

    expect(await screen.findByText('Some fields are not valid.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveFocus();
  });

  it('shows an unmapped server error in the form alert', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TestForm onSubmit={() => Promise.reject(new ApiError(422, ['VALIDATION_FAILED'], ''))} />);

    await fillAndSave(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields are not valid.');
  });

  it('shows the generic message for a non-API failure', async () => {
    const user = userEvent.setup();
    renderWithProviders(<TestForm onSubmit={() => Promise.reject(new Error('x'))} />);

    await fillAndSave(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
  });

  it('has no axe violations', async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<TestForm />);

    await user.click(screen.getByRole('button', { name: 'Save' }));
    await screen.findAllByText('This field is required.');

    expect((await axe(container)).violations).toEqual([]);
  });
});
