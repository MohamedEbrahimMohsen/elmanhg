const skeletonBlocks = ['first', 'second', 'third'];

export interface ConfigurationSkeletonProps {
  label: string;
}

export function ConfigurationSkeleton({ label }: ConfigurationSkeletonProps) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className="flex flex-col gap-3">
      {skeletonBlocks.map((block) => (
        <div key={block} className="h-16 rounded-md bg-soft" />
      ))}
    </div>
  );
}
