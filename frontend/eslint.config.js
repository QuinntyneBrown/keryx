// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const boundaries = require('eslint-plugin-boundaries');
const prettier = require('eslint-config-prettier');

/**
 * Lint for the whole workspace (AGENTS.md, "Frontend"). Prettier owns format, so its config
 * switches off every stylistic rule last. The dependency direction is enforced by
 * eslint-plugin-boundaries (docs/adr/0003-eslint-boundaries-for-the-dependency-direction.md):
 *
 *   admin       ->  components, domain, api
 *   components  ->  api (types only: no service, token or router)
 *   domain      ->  api
 *   api         ->  nothing in the workspace
 *
 * No library imports the app. A new library joins this list here.
 */
module.exports = defineConfig([
  { ignores: ['dist/', 'out-tsc/', '.angular/', 'test-results/', 'playwright-report/'] },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'kx', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'kx', style: 'kebab-case' },
      ],
      // File per type (AGENTS.md): templateUrl and styleUrl, never inline.
      '@angular-eslint/component-max-inline-declarations': [
        'error',
        { template: 0, styles: 0, animations: 0 },
      ],
      '@angular-eslint/use-component-view-encapsulation': 'error',
    },
  },
  {
    files: ['projects/**/*.ts'],
    plugins: { boundaries },
    settings: {
      'import/resolver': { typescript: { project: 'tsconfig.json' } },
      'boundaries/elements': [
        { type: 'app', pattern: 'projects/admin' },
        { type: 'components', pattern: 'projects/components' },
        { type: 'domain', pattern: 'projects/domain' },
        { type: 'api', pattern: 'projects/api' },
      ],
    },
    rules: {
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          policies: [
            {
              from: { element: { type: 'app' } },
              allow: { to: { element: { types: ['components', 'domain', 'api'] } } },
            },
            {
              from: { element: { type: 'components' } },
              allow: { to: { element: { type: 'api' } }, dependency: { kind: 'type' } },
            },
            {
              from: { element: { type: 'domain' } },
              allow: { to: { element: { type: 'api' } } },
            },
          ],
        },
      ],
    },
  },
  {
    // Presentation components take inputs and emit outputs only (AGENTS.md): no router and no
    // HTTP client. A component that sends someone somewhere renders an <a href> and emits
    // `navigate`; the app decides what navigating means.
    files: ['projects/components/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@angular/router',
              message: 'A presentation component takes no router: emit an output instead.',
            },
            {
              name: '@angular/common/http',
              message: 'A presentation component calls no API: take the data as an input.',
            },
          ],
        },
      ],
    },
  },
  {
    // A unit test's host component exists only to project content into the component under
    // test; it is not product code, so its template stays inline beside the test.
    files: ['projects/**/*.spec.ts'],
    rules: { '@angular-eslint/component-max-inline-declarations': 'off' },
  },
  {
    // Playwright reads a fixture's dependencies from its destructuring; `{}` means none.
    files: ['e2e/fixtures/**/*.ts'],
    rules: { 'no-empty-pattern': 'off' },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {
      // No style attribute in a template (AGENTS.md): one encapsulated stylesheet per component.
      '@angular-eslint/template/no-inline-styles': 'error',
    },
  },
  prettier,
]);
