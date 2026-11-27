import { decidedNotice, decisionFailedNotice } from '../public-api';

const TITLE = 'Toronto youth pastors challenge congregations to 100 km in March';

describe('decidedNotice', () => {
  // L2-052 AC1 and AC3: what a decision says, with the title as text.
  it.each([
    ['publish', 'success', 'Article published', `“${TITLE}” is now served by the API.`],
    ['hide', 'info', 'Hidden from the API', `“${TITLE}” is no longer served. It stays in Keryx.`],
  ] as const)('reports %s', (decision, tone, title, text) => {
    expect(decidedNotice(decision, TITLE)).toEqual({ tone, title, text });
  });
});

describe('decisionFailedNotice', () => {
  // L2-052 AC4: a failed request changes nothing, and says what the article still is.
  it.each([
    [
      'publish',
      'needs_review',
      'Keryx couldn’t publish the article',
      'Nothing changed — it is still waiting in Needs review.',
    ],
    [
      'publish',
      'hidden',
      'Keryx couldn’t publish the article',
      'Nothing changed — it is still hidden from the API.',
    ],
    [
      'hide',
      'needs_review',
      'Keryx couldn’t hide the article',
      'Nothing changed — it is still waiting in Needs review.',
    ],
    [
      'hide',
      'published',
      'Keryx couldn’t hide the article',
      'Nothing changed — it is still served by the API.',
    ],
    [
      'undo',
      'published',
      'Keryx couldn’t undo the decision',
      'Nothing changed — it is still served by the API.',
    ],
    [
      'undo',
      'hidden',
      'Keryx couldn’t undo the decision',
      'Nothing changed — it is still hidden from the API.',
    ],
  ] as const)('reports a failed %s of a %s article', (decision, status, title, text) => {
    expect(decisionFailedNotice(decision, status)).toEqual({ tone: 'danger', title, text });
  });
});
