import type { PublicationStatus } from './publication-status';

/** A decision's notification: its tone and words. The title is the article's, shown as text. */
export interface DecisionNotice {
  tone: 'success' | 'info' | 'danger';
  title: string;
  text: string;
}

/** What a decision staff made says (L2-052 AC1, AC3). */
export function decidedNotice(decision: 'publish' | 'hide', title: string): DecisionNotice {
  return decision === 'publish'
    ? { tone: 'success', title: 'Article published', text: `“${title}” is now served by the API.` }
    : {
        tone: 'info',
        title: 'Hidden from the API',
        text: `“${title}” is no longer served. It stays in Keryx.`,
      };
}

const FAILED: Record<'publish' | 'hide' | 'undo', string> = {
  publish: 'Keryx couldn’t publish the article',
  hide: 'Keryx couldn’t hide the article',
  undo: 'Keryx couldn’t undo the decision',
};

const STILL: Record<PublicationStatus, string> = {
  needs_review: 'it is still waiting in Needs review.',
  published: 'it is still served by the API.',
  hidden: 'it is still hidden from the API.',
};

/** What a failed decision says: nothing changed, and the status the article still has (L2-052 AC4). */
export function decisionFailedNotice(
  decision: 'publish' | 'hide' | 'undo',
  status: PublicationStatus,
): DecisionNotice {
  return { tone: 'danger', title: FAILED[decision], text: `Nothing changed — ${STILL[status]}` };
}
