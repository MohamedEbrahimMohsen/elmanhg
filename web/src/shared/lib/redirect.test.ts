import { describe, expect, it } from 'vitest';
import { isInAppUrl } from './redirect';

describe('isInAppUrl', () => {
  it('treats a same-origin path as in-app', () => {
    expect(isInAppUrl('/student/fake-checkout/x')).toBe(true);
  });

  it('treats an absolute URL as external', () => {
    expect(isInAppUrl('https://accept.paymob.com/unifiedcheckout/?publicKey=pk&clientSecret=cs')).toBe(false);
  });

  it('treats a protocol-relative URL as external', () => {
    expect(isInAppUrl('//evil.test/x')).toBe(false);
  });
});
