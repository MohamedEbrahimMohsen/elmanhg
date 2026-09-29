import { describe, expect, it } from 'vitest';
import { can } from './permissions';

describe('can', () => {
  it('grants a teacher the validation and reply capabilities', () => {
    expect(can('teacher', 'questionsValidate')).toBe(true);
    expect(can('teacher', 'askTeacherReply')).toBe(true);
  });

  it('denies a teacher content management and user management', () => {
    expect(can('teacher', 'contentManage')).toBe(false);
    expect(can('teacher', 'usersManage')).toBe(false);
  });

  it('grants an admin payment management and denies other roles', () => {
    expect(can('admin', 'paymentsManage')).toBe(true);
    expect(can('teacher', 'paymentsManage')).toBe(false);
    expect(can('student', 'paymentsManage')).toBe(false);
  });

  it('denies an admin question validation', () => {
    expect(can('admin', 'questionsValidate')).toBe(false);
  });
});
