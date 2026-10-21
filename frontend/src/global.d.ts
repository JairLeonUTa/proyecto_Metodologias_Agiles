/// <reference types="@testing-library/jest-dom" />

import { Vi } from 'vitest';

declare module 'vitest' {
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