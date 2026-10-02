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
  MessagesSquare,
  ScrollText,
  Settings,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { can, type Capability, type Role } from '@/features/session';

export type NavPath = NonNullable<LinkProps['to']>;

export interface NavItem {
  key: string;
  to: NavPath;
  labelKey: string;
  icon: LucideIcon;
  capability: Capability;
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
      { key: 'home', to: '/student', labelKey: 'nav.student.home', icon: House, capability: 'contentBrowse' },
      {
        key: 'progress',
        to: '/student/progress',
        labelKey: 'nav.student.progress',
        icon: ChartLine,
        capability: 'progressViewOwn',
      },
      {
        key: 'multiExam',
        to: '/student/multi-exam',
        labelKey: 'nav.student.multiExam',
        icon: Layers,
        capability: 'assessmentsTake',
      },
      {
        key: 'ask',
        to: '/student/ask',
        labelKey: 'nav.student.ask',
        icon: MessageCircleQuestion,
        capability: 'askTeacherSubmit',
      },
      {
        key: 'subscription',
        to: '/student/subscription',
        labelKey: 'nav.student.subscription',
        icon: CreditCard,
        capability: 'subscriptionManage',
      },
    ],
    tabBarKeys: ['home', 'progress', 'ask'],
    morePath: '/student/more',
  },
  teacher: {
    items: [
      {
        key: 'queue',
        to: '/teacher',
        labelKey: 'nav.teacher.queue',
        icon: ListChecks,
        capability: 'questionsValidate',
      },
      {
        key: 'gradeReviews',
        to: '/teacher/grades',
        labelKey: 'nav.teacher.gradeReviews',
        icon: ClipboardList,
        capability: 'aiGradesOverride',
      },
      { key: 'inbox', to: '/teacher/inbox', labelKey: 'nav.teacher.inbox', icon: Inbox, capability: 'askTeacherReply' },
      {
        key: 'stats',
        to: '/teacher/stats',
        labelKey: 'nav.teacher.stats',
        icon: ChartColumn,
        capability: 'teacherStatsViewOwn',
      },
    ],
    tabBarKeys: ['queue', 'gradeReviews', 'inbox'],
    morePath: '/teacher/more',
  },
  admin: {
    items: [
      {
        key: 'dashboard',
        to: '/admin',
        labelKey: 'nav.admin.dashboard',
        icon: LayoutDashboard,
        capability: 'dashboardsView',
      },
      {
        key: 'content',
        to: '/admin/content',
        labelKey: 'nav.admin.content',
        icon: BookOpen,
        capability: 'contentManage',
      },
      {
        key: 'questions',
        to: '/admin/questions',
        labelKey: 'nav.admin.questions',
        icon: FileQuestion,
        capability: 'contentManage',
      },
      {
        key: 'blueprints',
        to: '/admin/blueprints',
        labelKey: 'nav.admin.blueprints',
        icon: ClipboardList,
        capability: 'blueprintsManage',
      },
      { key: 'users', to: '/admin/users', labelKey: 'nav.admin.users', icon: Users, capability: 'usersManage' },
      {
        key: 'payments',
        to: '/admin/payments',
        labelKey: 'nav.admin.payments',
        icon: CreditCard,
        capability: 'paymentsManage',
      },
      { key: 'audit', to: '/admin/audit', labelKey: 'nav.admin.audit', icon: ScrollText, capability: 'auditLogView' },
      {
        key: 'avatarConversations',
        to: '/admin/avatar-conversations',
        labelKey: 'nav.admin.avatarConversations',
        icon: MessagesSquare,
        capability: 'avatarConversationsView',
      },
      {
        key: 'export',
        to: '/admin/export',
        labelKey: 'nav.admin.export',
        icon: Download,
        capability: 'trainingDataExport',
      },
      {
        key: 'configuration',
        to: '/admin/configuration',
        labelKey: 'nav.admin.configuration',
        icon: Settings,
        capability: 'configurationManage',
      },
    ],
    tabBarKeys: ['dashboard', 'content', 'questions'],
    morePath: '/admin/more',
  },
};

export function visibleNavItems(role: Role, nav: RoleNav): readonly NavItem[] {
  return nav.items.filter((item) => can(role, item.capability));
}

export function tabBarItems(role: Role, nav: RoleNav): readonly NavItem[] {
  return nav.tabBarKeys.flatMap((key) => visibleNavItems(role, nav).filter((item) => item.key === key));
}

export function overflowItems(role: Role, nav: RoleNav): readonly NavItem[] {
  return visibleNavItems(role, nav).filter((item) => !nav.tabBarKeys.includes(item.key));
}
