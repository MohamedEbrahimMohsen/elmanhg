import type { LinkProps } from '@tanstack/react-router';
import {
  BookOpen,
  ChartColumn,
  ChartLine,
  ClipboardList,
  CreditCard,
  Download,
  FileQuestion,
  House,
  Inbox,
  Layers,
  LayoutDashboard,
  ListChecks,
  MessageCircleQuestion,
  ScrollText,
  Users,
  type LucideIcon,
} from 'lucide-react';
import type { Role } from '@/features/session';

export type NavPath = NonNullable<LinkProps['to']>;

export interface NavItem {
  key: string;
  to: NavPath;
  labelKey: string;
  icon: LucideIcon;
}

export interface RoleNav {
  items: readonly NavItem[];
  tabBarKeys: readonly string[];
  morePath: NavPath | null;
}

// design-system: Lucide stroke 1.8
export const navIconStrokeWidth = 1.8;

export const navByRole: Record<Role, RoleNav> = {
  student: {
    items: [
      { key: 'home', to: '/student', labelKey: 'nav.student.home', icon: House },
      { key: 'progress', to: '/student/progress', labelKey: 'nav.student.progress', icon: ChartLine },
      { key: 'multiExam', to: '/student/multi-exam', labelKey: 'nav.student.multiExam', icon: Layers },
      { key: 'ask', to: '/student/ask', labelKey: 'nav.student.ask', icon: MessageCircleQuestion },
      { key: 'subscription', to: '/student/subscription', labelKey: 'nav.student.subscription', icon: CreditCard },
    ],
    tabBarKeys: ['home', 'progress', 'ask'],
    morePath: '/student/more',
  },
  teacher: {
    items: [
      { key: 'queue', to: '/teacher', labelKey: 'nav.teacher.queue', icon: ListChecks },
      { key: 'inbox', to: '/teacher/inbox', labelKey: 'nav.teacher.inbox', icon: Inbox },
      { key: 'stats', to: '/teacher/stats', labelKey: 'nav.teacher.stats', icon: ChartColumn },
    ],
    tabBarKeys: ['queue', 'inbox', 'stats'],
    morePath: null,
  },
  admin: {
    items: [
      { key: 'dashboard', to: '/admin', labelKey: 'nav.admin.dashboard', icon: LayoutDashboard },
      { key: 'content', to: '/admin/content', labelKey: 'nav.admin.content', icon: BookOpen },
      { key: 'questions', to: '/admin/questions', labelKey: 'nav.admin.questions', icon: FileQuestion },
      { key: 'blueprints', to: '/admin/blueprints', labelKey: 'nav.admin.blueprints', icon: ClipboardList },
      { key: 'users', to: '/admin/users', labelKey: 'nav.admin.users', icon: Users },
      { key: 'audit', to: '/admin/audit', labelKey: 'nav.admin.audit', icon: ScrollText },
      { key: 'export', to: '/admin/export', labelKey: 'nav.admin.export', icon: Download },
    ],
    tabBarKeys: ['dashboard', 'content', 'questions'],
    morePath: '/admin/more',
  },
};

export function tabBarItems(nav: RoleNav): readonly NavItem[] {
  return nav.tabBarKeys.flatMap((key) => nav.items.filter((item) => item.key === key));
}

export function overflowItems(nav: RoleNav): readonly NavItem[] {
  return nav.items.filter((item) => !nav.tabBarKeys.includes(item.key));
}
