import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { Pagination } from './Pagination';

describe('Pagination', () => {
  it('disables previous on the first page and reports page changes', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    renderWithProviders(<Pagination page={1} totalPages={3} onPageChange={onPageChange} />);

    expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Next page' }));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});
