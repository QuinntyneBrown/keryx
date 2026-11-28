/** The chip lists of Settings (L2-072), by the field of the settings that holds them. */
export type KeywordList = 'topics' | 'exclusions' | 'focus_areas';

/** What the Add dialog says for one list (docs/mocks/dialogs/add-keyword/). */
export interface KeywordWords {
  title: string;
  label: string;
  hint: string;
  submit: string;
  /** The error for a value already in the list. */
  duplicate: (value: string) => string;
}

export const KEYWORD_WORDS: Readonly<Record<KeywordList, KeywordWords>> = {
  topics: {
    title: 'Add a topic',
    label: 'Topic',
    hint: 'A word or short phrase, like “pickleball” or “youth sports”. Items that mention it score higher.',
    submit: 'Add topic',
    duplicate: (value) => `“${value}” is already a topic. Enter a different word or phrase.`,
  },
  exclusions: {
    title: 'Add an exclusion',
    label: 'Term',
    hint: 'A word or short phrase, like “casino” or “sponsored”. Items that mention it score lower.',
    submit: 'Add exclusion',
    duplicate: (value) => `“${value}” is already excluded. Enter a different word or phrase.`,
  },
  focus_areas: {
    title: 'Add a focus area',
    label: 'Area',
    hint: 'A city or region, like “Hamilton” or “Durham Region”. Items about it score higher, and it becomes a region.',
    submit: 'Add focus area',
    duplicate: (value) => `“${value}” is already a focus area. Enter a different place.`,
  },
};

/** Whether a value is in the list already, ignoring case and surrounding space (L2-072 AC4). */
export function isDuplicateKeyword(values: readonly string[], entry: string): boolean {
  const key = entry.trim().toLowerCase();
  return values.some((value) => value.trim().toLowerCase() === key);
}

/**
 * Why an entry cannot join the list, or null when it can: 2 to 40 characters once trimmed, and
 * not already there (L2-070 AC5, L2-072 AC4).
 */
export function keywordError(
  list: KeywordList,
  values: readonly string[],
  entry: string,
): string | null {
  const value = entry.trim();
  if (value.length < 2 || value.length > 40) return 'Enter 2 to 40 characters.';
  return isDuplicateKeyword(values, value) ? KEYWORD_WORDS[list].duplicate(value) : null;
}
