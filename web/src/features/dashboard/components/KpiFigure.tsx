import type { ReactNode } from 'react';

export interface KpiFigureProps {
  value: string;
  caption: string;
  note?: string | undefined;
  children?: ReactNode;
}

export function KpiFigure({ value, caption, note, children }: KpiFigureProps) {
  return (
    <>
      <p className="font-display text-stat font-bold text-text">{value}</p>
      <p className="text-caption text-text-muted">{caption}</p>
      {note ? <p className="text-caption text-text-muted">{note}</p> : null}
      {children ? <ul className="flex flex-col gap-1 text-caption text-text">{children}</ul> : null}
    </>
  );
}
