import type { Role } from './sessionStore';

export const capabilities = [
  'contentBrowse',
  'assessmentsTake',
  'contentManage',
  'lessonsPublish',
  'questionsValidate',
  'questionsChangeDifficulty',
  'blueprintsManage',
  'askTeacherSubmit',
  'askTeacherReply',
  'aiGradesOverride',
  'progressViewOwn',
  'progressViewAny',
  'subscriptionManage',
  'paymentsManage',
  'dashboardsView',
  'teacherStatsViewOwn',
  'usersManage',
  'auditLogView',
  'trainingDataExport',
  'avatarConversationsView',
] as const;

export type Capability = (typeof capabilities)[number];

export const roleCapabilities: Record<Role, readonly Capability[]> = {
  student: ['contentBrowse', 'assessmentsTake', 'askTeacherSubmit', 'progressViewOwn', 'subscriptionManage'],
  teacher: [
    'contentBrowse',
    'questionsValidate',
    'questionsChangeDifficulty',
    'askTeacherReply',
    'aiGradesOverride',
    'teacherStatsViewOwn',
  ],
  admin: [
    'contentBrowse',
    'assessmentsTake',
    'contentManage',
    'lessonsPublish',
    'questionsChangeDifficulty',
    'blueprintsManage',
    'askTeacherReply',
    'aiGradesOverride',
    'progressViewAny',
    'dashboardsView',
    'usersManage',
    'paymentsManage',
    'auditLogView',
    'trainingDataExport',
    'avatarConversationsView',
  ],
};

export function can(role: Role, capability: Capability): boolean {
  return roleCapabilities[role].includes(capability);
}
