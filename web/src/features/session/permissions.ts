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
  'avatarChat',
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
  'configurationManage',
] as const;

export type Capability = (typeof capabilities)[number];

export const roleCapabilities: Record<Role, readonly Capability[]> = {
  student: [
    'contentBrowse',
    'assessmentsTake',
    'askTeacherSubmit',
    'avatarChat',
    'progressViewOwn',
    'subscriptionManage',
  ],
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
    'configurationManage',
  ],
};

export function can(role: Role, capability: Capability): boolean {
  return roleCapabilities[role].includes(capability);
}
