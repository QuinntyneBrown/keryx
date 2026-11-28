import {
  issueChangeFailedNotice,
  issueMutedNotice,
  issueName,
  issueSeverityWord,
  issueTitle,
  issueWhen,
} from '../public-api';

const ZONE = 'America/Toronto';
const NOW = new Date('2026-11-28T14:46:00Z'); // Saturday 9:46 AM in Toronto

describe('issueTitle', () => {
  it.each([
    ['source_not_found', 'Feed returned 404'],
    ['source_timeout', 'Source timed out'],
    ['source_blocked', 'Source blocked Keryx'],
    ['source_invalid', 'Feed couldn’t be read'],
    ['items_held', 'Items held for review'],
    ['stage_failed', 'A stage failed'],
    ['run_skipped', 'Scheduled run skipped'],
    ['alert_not_sent', 'Alert e-mail not sent'],
    ['something_new', 'Something needs attention'],
  ])('%s reads %s', (kind, title) => {
    expect(issueTitle(kind)).toBe(title);
  });
});

describe('issueName', () => {
  it.each([
    [
      'source_timeout',
      'Covenant Cycling Collective',
      '“Source timed out” for Covenant Cycling Collective',
    ],
    ['stage_failed', null, '“A stage failed”'],
  ])('%s about %s is %s', (kind, subject, name) => {
    expect(issueName(kind, subject)).toBe(name);
  });
});

describe('issueMutedNotice', () => {
  it.each([
    [
      'source_timeout',
      'Covenant Cycling Collective',
      '“Source timed out” no longer raises alerts for Covenant Cycling Collective.',
    ],
    ['stage_failed', null, '“A stage failed” no longer raises alerts.'],
  ])('%s about %s says %s', (kind, subject, text) => {
    expect(issueMutedNotice(kind, subject)).toEqual({ title: 'Issue muted', text });
  });
});

describe('issueChangeFailedNotice', () => {
  it.each([
    [
      'mute',
      'Issue not muted',
      'Keryx couldn’t reach its API just now, so the issue still raises alerts. Try again.',
    ],
    [
      'unmute',
      'Issue not unmuted',
      'Keryx couldn’t reach its API just now, so the issue is still muted. Try again.',
    ],
  ] as const)('%s reads %s', (action, title, text) => {
    expect(issueChangeFailedNotice(action)).toEqual({ title, text });
  });
});

describe('issueSeverityWord', () => {
  it.each([
    ['error', 'Error:'],
    ['warning', 'Warning:'],
  ] as const)('%s reads %s', (severity, word) => {
    expect(issueSeverityWord(severity)).toBe(word);
  });
});

describe('issueWhen', () => {
  // L2-045 AC2: one run gives its time; a streak gives its length and when it began, by weekday
  // within the past week.
  it.each([
    ['2026-11-28T11:01:00Z', 1, 'Today, 6:01 AM'],
    ['2026-11-27T17:02:00Z', 1, 'Yesterday, 12:02 PM'],
    ['2026-11-23T17:02:00Z', 4, '4 runs in a row · since Mon, 12:02 PM'],
    ['2026-11-27T17:02:00Z', 2, '2 runs in a row · since Yesterday, 12:02 PM'],
    ['2026-11-22T17:02:00Z', 9, '9 runs in a row · since Sun, 12:02 PM'],
    ['2026-11-22T05:00:00Z', 9, '9 runs in a row · since Sun, 12:00 AM'],
    ['2026-11-22T04:59:00Z', 9, '9 runs in a row · since Sat, Nov 21, 11:59 PM'],
    ['2026-11-21T17:02:00Z', 12, '12 runs in a row · since Sat, Nov 21, 12:02 PM'],
    ['2025-11-28T17:02:00Z', 900, '900 runs in a row · since Fri, Nov 28, 2025, 12:02 PM'],
  ])('%s over %i runs reads %s', (since, runs, words) => {
    expect(issueWhen(since, runs, NOW, ZONE)).toBe(words);
  });
});
