/** What staff entered in "New API key". */
export interface ApiKeyFormEntries {
  productName: string;
  scopes: readonly string[];
}

/** The message for each part that is wrong, in the order of the form; empty when all are right. */
export type ApiKeyFormErrors = Partial<Record<keyof ApiKeyFormEntries, string>>;

const MAX_PRODUCT_NAME = 80;

/**
 * Checks "New API key" before anything is sent (L2-068 AC2): a product name of 1 to 80
 * characters once trimmed, and at least one scope. The API checks the same rules again (L2-035 AC3).
 */
export function apiKeyFormValidation(entries: ApiKeyFormEntries): ApiKeyFormErrors {
  const errors: ApiKeyFormErrors = {};
  const name = entries.productName.trim();
  if (name.length === 0 || name.length > MAX_PRODUCT_NAME) {
    errors.productName = 'Enter the product’s name so you can recognise its key later.';
  }
  if (entries.scopes.length === 0) {
    errors.scopes = 'Choose at least one scope.';
  }
  return errors;
}
