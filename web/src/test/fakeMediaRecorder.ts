import { vi, type Mock } from 'vitest';

const webmHeader = [0x1a, 0x45, 0xdf, 0xa3];

export function installFakeMediaRecorder(options: { supported?: string[]; permission?: 'granted' | 'denied' } = {}): {
  stopTrack: Mock;
  uninstall(): void;
} {
  const supported = options.supported ?? ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'];
  let active: { state: string; stop(): void } | null = null;
  const stopTrack = vi.fn(() => {
    if (active?.state === 'recording') {
      active.stop();
    }
  });
  const originalRecorder = Object.getOwnPropertyDescriptor(globalThis, 'MediaRecorder');
  const originalDevices = Object.getOwnPropertyDescriptor(navigator, 'mediaDevices');
  const originalCreate = Object.getOwnPropertyDescriptor(URL, 'createObjectURL');
  const originalRevoke = Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL');

  class FakeMediaRecorder extends EventTarget {
    static isTypeSupported(type: string): boolean {
      return supported.includes(type);
    }

    state: 'inactive' | 'recording' = 'inactive';

    readonly mimeType: string;

    constructor(_stream: unknown, recorderOptions?: { mimeType?: string }) {
      super();
      this.mimeType = recorderOptions?.mimeType ?? '';
      // eslint-disable-next-line @typescript-eslint/no-this-alias -- a browser recorder stops when its tracks end.
      active = this;
    }

    start(): void {
      this.state = 'recording';
    }

    stop(): void {
      this.state = 'inactive';
      const data = new Blob([new Uint8Array(webmHeader)], { type: this.mimeType });
      this.dispatchEvent(Object.assign(new Event('dataavailable'), { data }));
      this.dispatchEvent(new Event('stop'));
    }
  }

  Object.defineProperty(globalThis, 'MediaRecorder', { configurable: true, writable: true, value: FakeMediaRecorder });
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: {
      getUserMedia: () =>
        options.permission === 'denied'
          ? Promise.reject(new DOMException('denied', 'NotAllowedError'))
          : Promise.resolve({ getTracks: () => [{ stop: stopTrack }] }),
    },
  });
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, writable: true, value: () => 'blob:voice' });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, writable: true, value: vi.fn() });

  function restore(target: object, key: string, descriptor: PropertyDescriptor | undefined) {
    if (descriptor === undefined) {
      Reflect.deleteProperty(target, key);
    } else {
      Object.defineProperty(target, key, descriptor);
    }
  }

  return {
    stopTrack,
    uninstall() {
      restore(globalThis, 'MediaRecorder', originalRecorder);
      restore(navigator, 'mediaDevices', originalDevices);
      restore(URL, 'createObjectURL', originalCreate);
      restore(URL, 'revokeObjectURL', originalRevoke);
    },
  };
}
