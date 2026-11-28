/** What staff entered in "Add a source" or "Edit source". */
export interface SourceFormEntries {
  url: string;
  organisation: string;
  region: string;
  /** A news query's address is its query text, not a URL (L2-003); every other kind's is a URL. */
  newsQuery?: boolean;
}

/** The message for each field that is wrong, in the order of the form; empty when all are right. */
export type SourceFormErrors = Partial<Record<'url' | 'organisation' | 'region', string>>;

const MAX_ADDRESS = 2048;
const MAX_ORGANISATION = 120;

/**
 * Checks the entries of "Add a source" before anything is sent (L2-064 AC3). The address rules
 * are the API's (L2-001, L2-024 AC5): `http` or `https`, a host, no user name or password, port
 * 80 or 443, at most 2,048 characters. The API checks them again, and also that the host is public.
 */
export function sourceFormValidation(entries: SourceFormEntries): SourceFormErrors {
  const errors: SourceFormErrors = {};
  const address = entries.url.trim();
  if (entries.newsQuery) {
    if (address.length === 0 || address.length > MAX_ADDRESS) errors.url = 'Enter the news query.';
  } else if (!isAcceptableAddress(address)) {
    errors.url = 'Enter a full URL starting with https://, like https://lifepointfit.ca/events.';
  }
  const organisation = entries.organisation.trim();
  if (organisation.length === 0 || organisation.length > MAX_ORGANISATION) {
    errors.organisation = 'Enter the organisation’s name as it publishes it.';
  }
  if (entries.region === '') {
    errors.region = 'Choose the region the organisation is in.';
  }
  return errors;
}

/** Whether an address is one the API accepts (L2-001, L2-024 AC5). */
export function isAcceptableAddress(address: string): boolean {
  if (address.length === 0 || address.length > MAX_ADDRESS) return false;
  let url: URL;
  try {
    url = new URL(address);
  } catch {
    return false;
  }
  return (
    (url.protocol === 'http:' || url.protocol === 'https:') &&
    url.hostname.length > 0 &&
    url.username === '' &&
    url.password === '' &&
    (url.port === '' || url.port === '80' || url.port === '443')
  );
}

/** The URL field's message for a refusal the API words by its code (L2-064 AC7, L2-024). */
export const SOURCE_URL_REFUSALS: Readonly<Record<string, string>> = {
  source_exists: 'This source is already in the list.',
  unsafe_address: 'Keryx can’t fetch this address: it isn’t on the public internet.',
};
