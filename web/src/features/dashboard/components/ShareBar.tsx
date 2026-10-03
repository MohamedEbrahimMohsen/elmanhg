export interface ShareBarProps {
  percent: number;
  'aria-label'?: string;
  'aria-labelledby'?: string;
}

export function ShareBar({ percent, 'aria-label': ariaLabel, 'aria-labelledby': ariaLabelledBy }: ShareBarProps) {
  return (
    <progress
      value={Math.min(100, Math.max(0, percent))}
      max={100}
      aria-label={ariaLabel}
      aria-labelledby={ariaLabelledBy}
      className="h-1.5 w-full appearance-none overflow-hidden rounded-full bg-soft [&::-moz-progress-bar]:bg-accent [&::-webkit-progress-bar]:bg-soft [&::-webkit-progress-value]:bg-accent"
    />
  );
}
