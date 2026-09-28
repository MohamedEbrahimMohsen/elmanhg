import { FolderTree } from 'lucide-react';

export interface ContentEmptyStateProps {
  message: string;
}

export function ContentEmptyState({ message }: ContentEmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <FolderTree aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{message}</p>
    </div>
  );
}
