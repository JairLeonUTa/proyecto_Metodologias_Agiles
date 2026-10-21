import '@testing-library/jest-dom';
import { vi } from 'vitest';

// Mock requestAnimationFrame for tests
global.requestAnimationFrame = (callback) => setTimeout(callback, 0);
global.cancelAnimationFrame = (id) => clearTimeout(id);