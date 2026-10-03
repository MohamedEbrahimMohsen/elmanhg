import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axe } from '@/test/axe';
import { renderWithProviders } from '@/test/renderWithProviders';
import { Label } from './label';
import { Select } from './select';

function Field({ disabled = false }: { disabled?: boolean }) {
  return (
    <>
      <Label htmlFor="choice">Choice</Label>
      <Select id="choice" defaultValue="a" disabled={disabled} aria-invalid={disabled}>
        <option value="a">A</option>
        <option value="b">B</option>
      </Select>
    </>
  );
}

describe('Select', () => {
  it('is labelled and changes value when an option is chosen', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Field />);

    const select = screen.getByLabelText('Choice');
    await user.selectOptions(select, 'B');

    expect(select).toHaveDisplayValue('B');
  });

  it('passes disabled and aria-invalid through', () => {
    renderWithProviders(<Field disabled />);

    const select = screen.getByRole('combobox');
    expect(select).toBeDisabled();
    expect(select).toHaveAttribute('aria-invalid', 'true');
  });

  it('has no axe violations in Arabic', async () => {
    const { container } = renderWithProviders(<Field />, { lng: 'ar' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
