import { useId, type ReactNode } from 'react';

export interface PrivacySectionProps {
  title: string;
  children: ReactNode;
}

export function PrivacySection({ title, children }: PrivacySectionProps) {
  const headingId = useId();

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-2">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {title}
      </h2>
      {children}
    </section>
  );
}
