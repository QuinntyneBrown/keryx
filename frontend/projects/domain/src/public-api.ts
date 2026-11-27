// The domain library: pure rules and helpers with unit tests (AGENTS.md). No Angular, no DOM and
// no request: what is here can be reasoned about, and tested, as a function of its arguments.
export { viewportClass, type ViewportClass } from './viewport/viewport-class';
export {
  formatSourceAddress,
  sourceHealthLabel,
  sourceKindLabel,
  sourcesSummary,
  type SourceHealthLabel,
  type SourceCounts,
} from './sources/source-words';
export {
  isAcceptableAddress,
  sourceFormValidation,
  type SourceFormEntries,
  type SourceFormErrors,
} from './sources/source-form-validation';
export {
  nothingRelevantNotice,
  testFetchSummary,
  type TestFetchSummary,
} from './sources/test-fetch-words';
export {
  publicationStatusFor,
  REVIEW_FLOOR,
  type PublicationStatus,
} from './articles/publication-status';
export {
  articleStatusLabel,
  articleStatusTone,
  formatRelativeAge,
  formatRelevance,
  organizationLabel,
  safeHttpUrl,
  titleLabel,
} from './articles/article-words';
export {
  articleQueryFromParams,
  articleQueryToParams,
  changeArticleQuery,
  searchTerm,
  type ArticleParams,
  type ArticleQuery,
  type ArticleQueryParams,
} from './articles/article-query';
export {
  apiKeyFormValidation,
  type ApiKeyFormEntries,
  type ApiKeyFormErrors,
} from './clients/api-key-form-validation';
