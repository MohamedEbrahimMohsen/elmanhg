export interface DiagramDragGhostProps {
  text: string;
  x: number;
  y: number;
}

export function DiagramDragGhost({ text, x, y }: DiagramDragGhostProps) {
  return (
    <div aria-hidden dir="ltr" className="pointer-events-none fixed inset-0 z-50">
      <span
        className="absolute -translate-x-1/2 -translate-y-1/2 rounded-full border border-border-strong bg-surface px-3.5 py-2 text-ui shadow-2"
        style={{ insetInlineStart: x, top: y }}
      >
        {text}
      </span>
    </div>
  );
}
