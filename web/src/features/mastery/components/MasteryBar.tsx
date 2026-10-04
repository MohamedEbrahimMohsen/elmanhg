import { cn } from '@/shared/lib/utils';

export interface MasteryBarProps {
  percent: number;
  label: string;
  tone?: 'default' | 'onHero';
}

const toneClassName = {
  default:
    'bg-soft [&::-moz-progress-bar]:bg-accent [&::-webkit-progress-bar]:bg-soft [&::-webkit-progress-value]:bg-accent',
  onHero:
    'bg-surface/30 [&::-moz-progress-bar]:bg-surface [&::-webkit-progress-bar]:bg-surface/30 [&::-webkit-progress-value]:bg-surface',
} as const;

export function MasteryBar({ percent, label, tone = 'default' }: MasteryBarProps) {
  return (
    <progress
      value={Math.min(100, Math.max(0, percent))}
      max={100}
      aria-label={label}
      className={cn('h-1.5 w-full appearance-none overflow-hidden rounded-full', toneClassName[tone])}
    />
  );
}
