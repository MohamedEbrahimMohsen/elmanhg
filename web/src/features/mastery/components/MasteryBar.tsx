export interface MasteryBarProps {
  percent: number;
  label: string;
}

export function MasteryBar({ percent, label }: MasteryBarProps) {
  return (
    <progress
      value={Math.min(100, Math.max(0, percent))}
      max={100}
      aria-label={label}
      className="h-1.5 w-full appearance-none overflow-hidden rounded-full bg-soft [&::-moz-progress-bar]:bg-accent [&::-webkit-progress-bar]:bg-soft [&::-webkit-progress-value]:bg-accent"
    />
  );
}
