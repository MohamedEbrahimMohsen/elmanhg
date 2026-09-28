import { describe, expect, it } from 'vitest';
import { imageInsertSchema } from './imageInsertSchema';

const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'diagram.png', { type: 'image/png' });

describe('imageInsertSchema', () => {
  it('accepts a file with a description', () => {
    expect(imageInsertSchema.safeParse({ file, description: 'Force diagram' }).success).toBe(true);
  });

  it('rejects a missing file with validation.fileRequired', () => {
    expect(imageInsertSchema.safeParse({ description: 'Force diagram' }).error?.issues[0]?.message).toBe(
      'validation.fileRequired',
    );
  });

  it('rejects an empty description with validation.required', () => {
    expect(imageInsertSchema.safeParse({ file, description: ' ' }).error?.issues[0]?.message).toBe(
      'validation.required',
    );
  });
});
