import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import {
  getGetAuditLogResourceTypesMockHandler,
  getGetAuditLogsMockHandler,
} from '@/shared/api/generated/audit-logs/audit-logs.msw';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

describe('AuditLogPage in Arabic', () => {
  it('shows its own Arabic strings on first render with no English bundle loaded', async () => {
    for (const ns of Object.keys(i18n.store.data.en ?? {})) {
      i18n.removeResourceBundle('en', ns);
    }
    server.use(
      getGetAuditLogResourceTypesMockHandler(['Teacher']),
      getGetAuditLogsMockHandler({ items: [], pageNumber: 1, pageSize: 20, totalItems: 0, totalPages: 1 }),
    );

    renderApp('/admin/audit', { session: testSessions.admin, lng: 'ar' });

    expect(await screen.findByRole('heading', { name: 'سجل التدقيق' })).toBeInTheDocument();
    expect(screen.getByLabelText('المنفذ')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تطبيق' })).toBeInTheDocument();
    expect(i18n.hasResourceBundle('en', 'common')).toBe(false);
    expect(document.body).not.toHaveTextContent(/\b(page|filters)\.\w+/);
  });
});
