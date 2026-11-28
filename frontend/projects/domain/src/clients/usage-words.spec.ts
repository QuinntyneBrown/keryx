import { formatErrorRate, formatLastCall, formatLatency, requestsLabel } from '../public-api';

const ZONE = 'America/Toronto';
const NOW = new Date('2026-11-28T14:46:00Z'); // Saturday 9:46 AM in Toronto

describe('formatErrorRate', () => {
  it.each([
    [0, 0, '—'],
    [0.5, 0, '—'],
    [0, 96, '0%'],
    [0.0002, 48_210, '0.02%'],
    [0.0005, 52_826, '0.05%'],
    [0.004, 3_402, '0.4%'],
    [0.00123, 1_000, '0.12%'],
    [0.00996, 1_000, '1%'],
    [0.03, 100, '3%'],
    [0.125, 8, '12.5%'],
    [0.12345, 100_000, '12.3%'],
    [1, 4, '100%'],
    [0.00004, 100_000, '<0.01%'],
  ])('%d over %i requests reads %s', (rate, requests, words) => {
    expect(formatErrorRate(rate, requests)).toBe(words);
  });
});

describe('formatLastCall', () => {
  it.each([
    [null, 'never'],
    ['2026-11-28T14:45:40Z', 'just now'],
    ['2026-11-28T14:46:30Z', 'just now'],
    ['2026-11-28T14:45:00Z', '1 min ago'],
    ['2026-11-28T14:44:00Z', '2 min ago'],
    ['2026-11-28T13:46:01Z', '59 min ago'],
    ['2026-11-28T13:46:00Z', '8:46 AM'],
    ['2026-11-28T11:15:00Z', '6:15 AM'],
    ['2026-11-28T05:00:00Z', '12:00 AM'],
    ['2026-11-28T04:59:00Z', 'Nov 27'],
    ['2026-02-02T15:00:00Z', 'Feb 2'],
    ['2025-12-31T15:00:00Z', 'Dec 31, 2025'],
  ])('%s reads %s', (at, words) => {
    expect(formatLastCall(at, NOW, ZONE)).toBe(words);
  });
});

describe('formatLatency', () => {
  it.each([
    [null, { value: '—' }],
    [84, { value: '84', unit: 'ms' }],
    [10_000, { value: '10,000', unit: 'ms' }],
  ] as const)('%s reads %j', (p95, words) => {
    expect(formatLatency(p95)).toEqual(words);
  });
});

describe('requestsLabel', () => {
  it.each([
    [0, '0 requests'],
    [1, '1 request'],
    [48_210, '48,210 requests'],
  ])('%i reads %s', (requests, words) => {
    expect(requestsLabel(requests)).toBe(words);
  });
});
