/// <reference types="@testing-library/jest-dom" />

import 'vitest';

declare module 'vitest' {
  // El parámetro debe coincidir con la declaración original de vitest para poder ampliarla.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  interface Assertion<T = any> {
    toBeDisabled(): T;
    toHaveFocus(): T;
    toHaveValue(value: string): T;
    toHaveAttribute(name: string, value?: string): T;
  }
}

declare global {
  interface Window {
    requestAnimationFrame: (callback: FrameRequestCallback) => number;
    cancelAnimationFrame: (handle: number) => void;
  }
}