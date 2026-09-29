const skeletonRows = ['first', 'second', 'third', 'fourth', 'fifth'];

export interface AvatarConversationSkeletonProps {
  label: string;
}

export function AvatarConversationSkeleton({ label }: AvatarConversationSkeletonProps) {
  return (
    <div
      role="status"
      aria-busy="true"
      aria-label={label}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"
    >
      {skeletonRows.map((row) => (
        <div key={row} className="h-11 rounded-md bg-soft" />
      ))}
    </div>
  );
}
