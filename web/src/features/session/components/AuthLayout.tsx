import type { ReactNode } from 'react';
import { BrandBar } from '@/shared/components/BrandBar';

export interface AuthLayoutProps {
  title: string;
  children: ReactNode;
  footer: ReactNode;
}

export function AuthLayout({ title, children, footer }: AuthLayoutProps) {
  return (
    <div className="flex min-h-dvh flex-col">
      <BrandBar />
      <main id="main" className="mx-auto flex w-full max-w-auth flex-1 flex-col justify-center gap-6 px-4 py-8">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{title}</h1>
        <section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
          {children}
        </section>
        <p className="text-caption text-text-muted">{footer}</p>
      </main>
    </div>
  );
}
