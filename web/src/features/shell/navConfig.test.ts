import { Users } from 'lucide-react';
import { describe, expect, it } from 'vitest';
import { can, roles } from '@/features/session';
import { navByRole, overflowItems, topBarItems, topBarOverflowItems, visibleNavItems, type RoleNav } from './navConfig';

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

describe('topBarItems', () => {
  it('returns the admin top-bar destinations in nav order', () => {
    expect(topBarItems('admin', navByRole.admin).map((item) => item.key)).toEqual([
      'dashboard',
      'content',
      'questions',
      'users',
    ]);
  });
});

describe('topBarOverflowItems', () => {
  it('puts the remaining admin destinations in More and none for students or teachers', () => {
    expect(topBarOverflowItems('admin', navByRole.admin).map((item) => item.key)).toEqual([
      'blueprints',
      'payments',
      'audit',
      'avatarConversations',
      'export',
      'configuration',
    ]);
    expect(topBarOverflowItems('student', navByRole.student)).toEqual([]);
    expect(topBarOverflowItems('teacher', navByRole.teacher)).toEqual([]);
  });

  it('hides an overflow destination whose capability the role lacks', () => {
    const nav: RoleNav = {
      ...navByRole.teacher,
      items: [
        ...navByRole.teacher.items,
        { key: 'users', to: '/admin/users', labelKey: 'nav.admin.users', icon: Users, capability: 'usersManage' },
      ],
    };

    expect(topBarOverflowItems('teacher', nav)).toEqual([]);
  });
});
