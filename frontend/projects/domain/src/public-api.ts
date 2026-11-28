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
  ARTICLE_CATEGORIES,
  articleReasonAlert,
  articleStatusLabel,
  formatCollectedAt,
  articleStatusTone,
  categoryName,
  formatCount,
  noResultsWords,
  pageRange,
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
export {
  SEARCH_PAUSE_MS,
  SKELETON_DELAY_MS,
  searchPause,
  skeletonDelay,
  type SearchPause,
} from './articles/search-pause';
export {
  decidedNotice,
  decisionFailedNotice,
  type DecisionNotice,
} from './articles/decision-words';
export {
  formatClockTime,
  formatCountdown,
  formatDuration,
  runHeadline,
  runTone,
  type RunOutcome,
  type RunSummaryView,
} from './overview/run-words';
export { runBars, type RunBar, type RunPoint } from './overview/run-bars';
export { issueSeverityWord, issueTitle, issueWhen } from './overview/issue-words';
export {
  issueStreakLabel,
  runFailedFlag,
  runElapsedMs,
  runHowLine,
  runLineMeta,
  runOutcomeLabel,
  scheduleLine,
  type RunLineView,
  type ScheduleView,
} from './runs/run-history-words';
export {
  formatStageDuration,
  PIPELINE_STAGES,
  runningStageMs,
  stageDetail,
  stageName,
  stageState,
  stageStatusLabel,
  stageSummary,
  stageTime,
  type RunProgress,
  type StageView,
} from './runs/stage-words';
export { collectionStartedText } from './runs/run-now-words';
