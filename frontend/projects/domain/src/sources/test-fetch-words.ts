import type { FetchFailure, Notice, SourceKind, SourceTestResult } from '@kx/api';
import { sourceKindLabel } from './source-words';

/** How a test fetch's result reads in "Add a source": its tone and its sentence. */
export interface TestFetchSummary {
  tone: 'success' | 'warning';
  text: string;
}

const NUMBER = new Intl.NumberFormat('en-CA');

/** What a kind's items are called: [one, many]. */
const ITEMS: Record<SourceKind, readonly [string, string]> = {
  rss: ['item', 'items'],
  website: ['event page', 'event pages'],
  instagram: ['post', 'posts'],
  eventbrite: ['event', 'events'],
  news_query: ['article', 'articles'],
};

/** The same, in the toast after adding, as notifications/source-toast/warning.html words it. */
const SHORT_ITEMS: Record<SourceKind, readonly [string, string]> = {
  ...ITEMS,
  website: ['page', 'pages'],
};

const FAILURES: Record<Exclude<FetchFailure, 'refused'>, string> = {
  not_found: 'Test fetch found nothing at this address (not found).',
  timeout: 'Test fetch timed out. Check the address, or try again later.',
  blocked_by_robots: 'This site asks robots not to read this address, so Keryx won’t.',
  invalid_content: 'The address answered, but not with anything Keryx can read.',
  unsafe_address: 'Keryx can’t fetch this address: it isn’t on the public internet.',
};

function count(kind: SourceKind, n: number, words = ITEMS): string {
  const [one, many] = words[kind];
  return `${NUMBER.format(n)} ${n === 1 ? one : many}`;
}

/**
 * The sentence under the URL after a test fetch (L2-064 AC2): "Test fetch found 6 event pages —
 * 5 look relevant.", or why it could not read the address. A platform kind is refused only when
 * its access is not set up, so its refusal says the kind is unavailable (L2-021 AC4).
 */
export function testFetchSummary(kind: SourceKind, result: SourceTestResult): TestFetchSummary {
  if (!result.reachable) {
    if (result.reason && result.reason !== 'refused') {
      return { tone: 'warning', text: FAILURES[result.reason] };
    }
    if (kind === 'instagram' || kind === 'eventbrite') {
      return {
        tone: 'warning',
        text: `Keryx can’t read ${sourceKindLabel(kind)} yet: its platform access isn’t set up.`,
      };
    }
    return { tone: 'warning', text: 'The site refused the test fetch.' };
  }
  const found = result.items_found;
  if (found === 0) {
    return { tone: 'warning', text: 'Test fetch reached the address but found nothing to read.' };
  }
  const relevant = result.items_relevant;
  const verdict =
    relevant === 0
      ? 'none look relevant'
      : `${NUMBER.format(relevant)} ${relevant === 1 ? 'looks' : 'look'} relevant`;
  return {
    tone: relevant === 0 ? 'warning' : 'success',
    text: `Test fetch found ${count(kind, found)} — ${verdict}.`,
  };
}

/**
 * The warning that replaces "Source added" when the last test reached items and none looked
 * relevant (L2-064 AC5); null otherwise. Its "Edit source" action arrives with the edit dialog
 * (journey 17).
 */
export function nothingRelevantNotice(
  kind: SourceKind,
  result: SourceTestResult | null,
  organisation: string,
): Notice | null {
  if (!result?.reachable || result.items_found === 0 || result.items_relevant > 0) return null;
  return {
    tone: 'warning',
    title: 'Source added, but nothing looked relevant',
    text: `The test fetch for ${organisation} found ${count(kind, result.items_found, SHORT_ITEMS)} and none matched your topics.`,
  };
}

/**
 * How "Test fetch now" went (L2-063 AC3): "Tested Redeemed Run Club Toronto" and the sentence of
 * the add dialog. An unreachable source's health is not changed by a test, so the notice says
 * when it is.
 */
export function sourceTestNotice(kind: SourceKind, name: string, result: SourceTestResult): Notice {
  const summary = testFetchSummary(kind, result);
  return {
    tone: summary.tone,
    title: `Tested ${name}`,
    text: result.reachable
      ? summary.text
      : `${summary.text} Its health changes when a run fetches it.`,
  };
}

/**
 * The notification after "Save source" (L2-065 AC2): "Source saved", with how the test of the
 * saved address went and when the change applies, in the test's tone.
 */
export function sourceSavedNotice(kind: SourceKind, test: SourceTestResult): Notice {
  const summary = testFetchSummary(kind, test);
  return {
    tone: summary.tone,
    title: 'Source saved',
    text: `${summary.text} Changes apply from the next scheduled run.`,
  };
}
