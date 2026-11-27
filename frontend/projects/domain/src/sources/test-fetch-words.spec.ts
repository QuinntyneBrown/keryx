import type { SourceKind, SourceTestResult } from '@kx/api';
import { nothingRelevantNotice, testFetchSummary } from '../public-api';

const reached = (found: number, relevant: number): SourceTestResult => ({
  reachable: true,
  reason: null,
  items_found: found,
  items_relevant: relevant,
});

const failed = (reason: SourceTestResult['reason']): SourceTestResult => ({
  reachable: false,
  reason,
  items_found: 0,
  items_relevant: 0,
});

describe('testFetchSummary', () => {
  // L2-064 AC2: "Test fetch found 6 event pages — 5 look relevant."
  it.each([
    ['website', reached(6, 5), 'success', 'Test fetch found 6 event pages — 5 look relevant.'],
    ['website', reached(1, 1), 'success', 'Test fetch found 1 event page — 1 looks relevant.'],
    ['rss', reached(24, 0), 'warning', 'Test fetch found 24 items — none look relevant.'],
    ['rss', reached(1, 0), 'warning', 'Test fetch found 1 item — none look relevant.'],
    ['rss', reached(1234, 2), 'success', 'Test fetch found 1,234 items — 2 look relevant.'],
    ['eventbrite', reached(3, 3), 'success', 'Test fetch found 3 events — 3 look relevant.'],
    ['instagram', reached(9, 4), 'success', 'Test fetch found 9 posts — 4 look relevant.'],
    ['rss', reached(0, 0), 'warning', 'Test fetch reached the address but found nothing to read.'],
  ] as const)('words a %s result %o as %s "%s"', (kind, result, tone, text) => {
    expect(testFetchSummary(kind as SourceKind, result)).toEqual({ tone, text });
  });

  it.each([
    ['rss', 'not_found', 'Test fetch found nothing at this address (not found).'],
    ['rss', 'timeout', 'Test fetch timed out. Check the address, or try again later.'],
    ['rss', 'blocked_by_robots', 'This site asks robots not to read this address, so Keryx won’t.'],
    ['website', 'refused', 'The site refused the test fetch.'],
    ['instagram', 'refused', 'Keryx can’t read Instagram yet: its platform access isn’t set up.'],
    ['eventbrite', 'refused', 'Keryx can’t read Eventbrite yet: its platform access isn’t set up.'],
    ['rss', 'invalid_content', 'The address answered, but not with anything Keryx can read.'],
    ['rss', 'unsafe_address', 'Keryx can’t fetch this address: it isn’t on the public internet.'],
    ['rss', null, 'The site refused the test fetch.'],
  ] as const)('words a %s failure %s as "%s"', (kind, reason, text) => {
    expect(testFetchSummary(kind as SourceKind, failed(reason))).toEqual({ tone: 'warning', text });
  });
});

describe('nothingRelevantNotice', () => {
  // L2-064 AC5 and notifications/source-toast/warning.html.
  it('warns when the test reached items and none looked relevant', () => {
    expect(nothingRelevantNotice('website', reached(6, 0), 'LifePoint Fitness Ministry')).toEqual({
      tone: 'warning',
      title: 'Source added, but nothing looked relevant',
      text: 'The test fetch for LifePoint Fitness Ministry found 6 pages and none matched your topics.',
    });
  });

  it.each([
    ['a relevant item', reached(6, 1)],
    ['nothing to read', reached(0, 0)],
    ['an unreachable address', failed('timeout')],
    ['no test', null],
  ])('says nothing more after %s', (_, result) => {
    expect(nothingRelevantNotice('rss', result, 'LifePoint')).toBeNull();
  });
});
