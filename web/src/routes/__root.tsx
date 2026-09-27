import { createRootRouteWithContext, Outlet } from '@tanstack/react-router';
import type { RouterContext } from '@/app/routerContext';

export const Route = createRootRouteWithContext<RouterContext>()({ component: Outlet });
