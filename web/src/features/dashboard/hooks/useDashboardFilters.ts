import { useEffect, useMemo, useState } from 'react';
import { getRouteApi } from '@tanstack/react-router';
import {
  cairoToday,
  dashboardRange,
  defaultDashboardPeriod,
  type DashboardPeriod,
  type DashboardRange,
} from '../api/dashboardRange';

const routeApi = getRouteApi('/admin/');

const dayCheckIntervalMs = 60_000;

function useCairoToday(): string {
  const [today, setToday] = useState(() => cairoToday(new Date()));
  useEffect(() => {
    const id = setInterval(() => {
      setToday(cairoToday(new Date()));
    }, dayCheckIntervalMs);
    return () => {
      clearInterval(id);
    };
  }, []);
  return today;
}

export interface DashboardFilters {
  days: DashboardPeriod;
  subjectId: string | undefined;
  range: DashboardRange;
  subjectRangeParams: { from: string; to: string; subjectId?: string };
  contentParams: { subjectId?: string };
  setDays: (days: DashboardPeriod) => void;
  setSubject: (subjectId: string) => void;
}

export function useDashboardFilters(): DashboardFilters {
  const search = routeApi.useSearch();
  const navigate = routeApi.useNavigate();
  const today = useCairoToday();
  const days = search.days ?? defaultDashboardPeriod;
  const range = useMemo(() => dashboardRange(days, new Date(`${today}T12:00:00Z`)), [days, today]);
  const subject = search.subjectId === undefined ? {} : { subjectId: search.subjectId };

  return {
    days,
    subjectId: search.subjectId,
    range,
    subjectRangeParams: { ...range, ...subject },
    contentParams: subject,
    setDays: (next) => {
      void navigate({ search: (previous) => ({ ...previous, days: next }) });
    },
    setSubject: (id) => {
      void navigate({
        search: (previous) => {
          const next = { ...previous };
          delete next.subjectId;
          return id === '' ? next : { ...next, subjectId: id };
        },
      });
    },
  };
}
