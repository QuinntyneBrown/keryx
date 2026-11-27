/** What staff entered in "Add a source". */
export interface SourceFormEntries {
  url: string;
  organisation: string;
  region: string;
}

/** The message for each field that is wrong, in the order of the form; empty when all are right. */
export type SourceFormErrors = Partial<Record<keyof SourceFormEntries, string>>;

const MAX_ADDRESS = 2048;
const MAX_ORGANISATION = 120;

/**
 * Checks the entries of "Add a source" before anything is sent (L2-064 AC3). The address rules
 * are the API's (L2-001, L2-024 AC5): `http` or `https`, a host, no user name or password, port
 * 80 or 443, at most 2,048 characters. The API checks them again, and also that the host is public.
 */
export function sourceFormValidation(entries: SourceFormEntries): SourceFormErrors {
  const errors: SourceFormErrors = {};
  if (!isAcceptableAddress(entries.url.trim())) {
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
