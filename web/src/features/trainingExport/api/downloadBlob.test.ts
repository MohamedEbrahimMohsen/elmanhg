import { afterEach, describe, expect, it, vi } from 'vitest';
import { downloadBlob } from './downloadBlob';

describe('downloadBlob', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('clicks an anchor to the blob with the file name and removes it afterwards', () => {
    const blob = new Blob(['{}\n']);
    const createObjectURL = vi.fn<(value: Blob) => string>(() => 'blob:export');
    const revokeObjectURL = vi.fn<(url: string) => void>();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true, writable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true, writable: true });
    const clicked: { anchor: HTMLAnchorElement; connected: boolean }[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push({ anchor: this, connected: this.isConnected });
    });

    downloadBlob(blob, 'elmanhg-attempts-2026-01-01-2026-02-01.jsonl');

    expect(createObjectURL).toHaveBeenCalledWith(blob);
    const [click] = clicked;
    expect(clicked).toHaveLength(1);
    expect(click?.connected).toBe(true);
    expect(click?.anchor.href).toBe('blob:export');
    expect(click?.anchor.download).toBe('elmanhg-attempts-2026-01-01-2026-02-01.jsonl');
    expect(click?.anchor.isConnected).toBe(false);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:export');
  });
});
