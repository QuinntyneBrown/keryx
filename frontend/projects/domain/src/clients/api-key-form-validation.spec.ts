import { describe, expect, it } from 'vitest';
import { apiKeyFormValidation, type ApiKeyFormErrors } from './api-key-form-validation';

const NAME = 'Enter the product’s name so you can recognise its key later.';
const SCOPES = 'Choose at least one scope.';

describe('apiKeyFormValidation', () => {
  // The backend's CreateApiClientTests case table (product name, scopes, refused field); a scope
  // the API does not know cannot be chosen from the checkboxes, so those rows are the API's alone.
  it.each<[string, string[], ApiKeyFormErrors]>([
    ['', ['articles:read'], { productName: NAME }],
    ['   ', ['articles:read'], { productName: NAME }],
    ['a'.repeat(81), ['articles:read'], { productName: NAME }],
    ['Covenant Cycling site', [], { scopes: SCOPES }],
    ['', [], { productName: NAME, scopes: SCOPES }],
    [`  ${'a'.repeat(80)}  `, ['sources:read'], {}],
    ['a', ['articles:read', 'events:read', 'sources:read'], {}],
  ])('name %j with scopes %j gives %j', (productName, scopes, expected) => {
    expect(apiKeyFormValidation({ productName, scopes })).toEqual(expected);
  });
});
