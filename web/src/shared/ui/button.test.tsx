import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { Button } from './button';

describe('Button', () => {
  it('defaults to type button so it never submits a form by accident', () => {
    renderWithProviders(<Button>Go</Button>);

    expect(screen.getByRole('button', { name: 'Go' })).toHaveAttribute('type', 'button');
  });

  it('renders its child as the element when asChild is set', () => {
    renderWithProviders(
      <Button asChild>
        <a href="/x">Go</a>
      </Button>,
    );

    expect(screen.getByRole('link', { name: 'Go' })).toHaveAttribute('href', '/x');
    expect(screen.queryByRole('button')).toBeNull();
  });
});
