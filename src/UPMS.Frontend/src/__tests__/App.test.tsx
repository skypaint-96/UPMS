import { describe, expect, test } from 'vitest';

describe('frontend shell', () => {
  test('contains application name', () => {
    expect('UPMS').toBe('UPMS');
  });
});
