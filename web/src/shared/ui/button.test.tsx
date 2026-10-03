import { screen } from '@testing-library/react';
import { beforeAll, describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test/renderWithProviders';
import { compileBackgroundRules, specificity, winningBackground, type PointerState } from '@/test/tailwindCascade';
import { Button, buttonVariants, type ButtonProps } from './button';

type Variant = NonNullable<ButtonProps['variant']>;

const variants: Variant[] = ['primary', 'accent', 'secondary', 'danger', 'ghost'];

let rules: Awaited<ReturnType<typeof compileBackgroundRules>>;

beforeAll(async () => {
  rules = await compileBackgroundRules(variants.map((variant) => buttonVariants({ variant })));
});

const fill = (token: string) => `var(--ds-color-${token})`;

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

  it.each<[string, ButtonProps, PointerState, string]>([
    ['primary at rest', { variant: 'primary' }, {}, fill('accent')],
    ['primary hovered', { variant: 'primary' }, { hover: true }, fill('accent-hover')],
    ['primary pressed', { variant: 'primary' }, { hover: true, active: true }, fill('accent-pressed')],
    ['accent pressed', { variant: 'accent' }, { hover: true, active: true }, fill('accent-pressed')],
    ['disabled primary hovered', { variant: 'primary', disabled: true }, { hover: true }, fill('accent')],
    ['secondary hovered', { variant: 'secondary' }, { hover: true }, fill('soft')],
    ['toggled secondary hovered', { variant: 'secondary', 'aria-pressed': true }, { hover: true }, fill('accent-soft')],
    [
      'toggled secondary pressed',
      { variant: 'secondary', 'aria-pressed': true },
      { hover: true, active: true },
      fill('accent-soft'),
    ],
    ['disabled secondary hovered', { variant: 'secondary', disabled: true }, { hover: true }, fill('surface')],
    [
      'disabled toggled secondary',
      { variant: 'secondary', disabled: true, 'aria-pressed': true },
      { hover: true },
      fill('accent-soft'),
    ],
    ['danger hovered', { variant: 'danger' }, { hover: true }, fill('danger-soft')],
    ['disabled danger hovered', { variant: 'danger', disabled: true }, { hover: true }, fill('surface')],
    ['disabled ghost hovered', { variant: 'ghost', disabled: true }, { hover: true }, 'transparent'],
  ])('resolves the %s fill from the compiled CSS', (_, props, state, expected) => {
    renderWithProviders(<Button {...props}>Go</Button>);

    expect(winningBackground(screen.getByRole('button', { name: 'Go' }), rules, state)).toBe(expected);
  });

  it('keeps the hover fill on an asChild link', () => {
    renderWithProviders(
      <Button asChild>
        <a href="/x">Go</a>
      </Button>,
    );

    expect(winningBackground(screen.getByRole('link', { name: 'Go' }), rules, { hover: true })).toBe(
      fill('accent-hover'),
    );
  });

  it('counts pseudo-classes inside :not() towards specificity', () => {
    expect(specificity('.a\\:b:hover:not(:disabled)')).toEqual([0, 3, 0]);
    expect(specificity('.a[aria-pressed=true]')).toEqual([0, 2, 0]);
  });
});
