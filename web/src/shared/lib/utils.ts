import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';

const twMerge = extendTailwindMerge({
  extend: {
    theme: {
      text: [
        'display',
        'display-desktop',
        'h1',
        'h1-desktop',
        'h2',
        'h2-desktop',
        'h3',
        'body',
        'ui',
        'label',
        'caption',
        'micro',
        'stat',
        'mono',
      ],
      shadow: ['1'],
    },
  },
});

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
