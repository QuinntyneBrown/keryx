import { isDuplicateKeyword, keywordError } from './keyword-words';

describe('isDuplicateKeyword', () => {
  const values = ['fitness', 'running', 'sports ministry'];
  // L2-072 AC4: case and surrounding space are ignored; inner space is not.
  const cases: [string, boolean][] = [
    ['running', true],
    ['RUNNING', true],
    ['  Running ', true],
    ['sports ministry', true],
    ['sportsministry', false],
    ['run', false],
    ['runnings', false],
  ];
  for (const [entry, duplicate] of cases) {
    it(`${duplicate ? 'finds' : 'does not find'} "${entry}"`, () => {
      expect(isDuplicateKeyword(values, entry)).toBe(duplicate);
    });
  }
});

describe('keywordError', () => {
  const cases: [Parameters<typeof keywordError>, string | null][] = [
    [
      ['topics', ['running'], 'running'],
      '“running” is already a topic. Enter a different word or phrase.',
    ],
    [
      ['topics', ['running'], ' Running '],
      '“Running” is already a topic. Enter a different word or phrase.',
    ],
    [
      ['exclusions', ['casino'], 'casino'],
      '“casino” is already excluded. Enter a different word or phrase.',
    ],
    [
      ['focus_areas', ['Toronto'], 'toronto'],
      '“toronto” is already a focus area. Enter a different place.',
    ],
    [['topics', ['running'], 'pickleball'], null],
    [['topics', [], 'ab'], null],
    [['topics', [], ' a '], 'Enter 2 to 40 characters.'],
    [['topics', [], ''], 'Enter 2 to 40 characters.'],
    [['topics', [], 'x'.repeat(40)], null],
    [['topics', [], 'x'.repeat(41)], 'Enter 2 to 40 characters.'],
  ];
  for (const [args, error] of cases) {
    it(`answers ${error ? `"${error}"` : 'null'} for "${args[2].slice(0, 12)}"`, () => {
      expect(keywordError(...args)).toBe(error);
    });
  }
});
