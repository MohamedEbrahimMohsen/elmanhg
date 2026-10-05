import { type ReactNode, useId } from 'react';
import { useTranslation } from 'react-i18next';

export interface PlanCardProps {
  title: string;
  priceLines: string[];
  features: string[];
  isActive: boolean;
  actions?: ReactNode;
}

export function PlanCard({ title, priceLines, features, isActive, actions }: PlanCardProps) {
  const { t } = useTranslation('subscription');
  const headingId = useId();

  return (
    <article
      aria-labelledby={headingId}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <div className="flex items-center justify-between gap-2">
        <h3 id={headingId} className="font-display text-h3 font-bold">
          {title}
        </h3>
        {isActive ? (
          <span className="rounded-pill bg-success-soft px-2.5 py-0.5 text-micro font-bold text-success-text">
            {t('badge.active')}
          </span>
        ) : null}
      </div>
      {priceLines.map((line) => (
        <p key={line} className="font-display text-h3 font-bold">
          {line}
        </p>
      ))}
      <ul className="list-disc ps-5 text-ui">
        {features.map((feature) => (
          <li key={feature}>{feature}</li>
        ))}
      </ul>
      {actions ? <div className="mt-auto flex flex-col gap-2">{actions}</div> : null}
    </article>
  );
}
