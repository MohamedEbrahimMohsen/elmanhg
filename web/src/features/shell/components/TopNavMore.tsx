import { useEffect, useId, useRef, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { navIconStrokeWidth, type NavItem } from '../navConfig';
import { topNavItemClassName } from '../navStyles';

export interface TopNavMoreProps {
  items: readonly NavItem[];
}

export function TopNavMore({ items }: TopNavMoreProps) {
  const { t } = useTranslation('shell');
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const panelId = useId();

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!(event.target instanceof Node) || !rootRef.current?.contains(event.target)) {
        setOpen(false);
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    const onFocusOut = (event: FocusEvent) => {
      if (event.relatedTarget instanceof Node && !rootRef.current?.contains(event.relatedTarget)) {
        setOpen(false);
      }
    };
    const root = rootRef.current;
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    root?.addEventListener('focusout', onFocusOut);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
      root?.removeEventListener('focusout', onFocusOut);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="group relative shrink-0">
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        className={`${topNavItemClassName} group-has-[[data-status=active]]:bg-accent-soft group-has-[[data-status=active]]:font-bold group-has-[[data-status=active]]:text-accent-text`}
        onClick={() => {
          setOpen(!open);
        }}
      >
        {t('nav.more')}
        <ChevronDown aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} />
      </button>
      <ul
        id={panelId}
        hidden={!open}
        className="absolute end-0 top-full z-30 mt-2 flex min-w-56 flex-col gap-0.5 rounded-md border border-border bg-surface p-1.5 shadow-1"
      >
        {items.map(({ key, to, labelKey, icon: Icon }) => (
          <li key={key}>
            <Link
              to={to}
              onClick={() => {
                setOpen(false);
              }}
              className="flex min-h-11 items-center gap-3 rounded-sm px-3 text-ui text-text hover:bg-bg focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden data-[status=active]:bg-accent-soft data-[status=active]:font-bold data-[status=active]:text-accent-text"
            >
              <Icon aria-hidden className="size-5 shrink-0" strokeWidth={navIconStrokeWidth} />
              <span>{t(labelKey)}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
