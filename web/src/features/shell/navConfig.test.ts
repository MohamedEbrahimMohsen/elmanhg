import { Users } from 'lucide-react';
import { describe, expect, it } from 'vitest';
import { can, roles } from '@/features/session';
import { navByRole, overflowItems, visibleNavItems, type RoleNav } from './navConfig';

describe('navByRole', () => {
  it('grants every configured destination to its role', () => {
    for (const role of roles) {
      for (const item of navByRole[role].items) {
        expect(can(role, item.capability), `${role} → ${item.key}`).toBe(true);
      }
    }
  });
});

describe('visibleNavItems', () => {
  it('hides a destination whose capability the role lacks', () => {
    const nav: RoleNav = {
      ...navByRole.teacher,
      items: [
        ...navByRole.teacher.items,
        { key: 'users', to: '/admin/users', labelKey: 'nav.admin.users', icon: Users, capability: 'usersManage' },
      ],
    };

    expect(visibleNavItems('teacher', nav).map((item) => item.key)).toEqual([
      'queue',
      'gradeReviews',
      'inbox',
      'stats',
    ]);
    expect(overflowItems('teacher', nav).map((item) => item.key)).toEqual(['stats']);
  });
});
