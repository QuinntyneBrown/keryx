// The domain library: pure rules and helpers with unit tests (AGENTS.md). No Angular, no DOM and
// no request: what is here can be reasoned about, and tested, as a function of its arguments.
export { viewportClass, type ViewportClass } from './viewport/viewport-class';
export {
  formatSourceAddress,
  lastFetchFailure,
  removeSourceText,
  sourceHealthLabel,
  SOURCE_KINDS,
  sourceKindLabel,
  sourceKindOf,
  sourceLastFetched,
  sourceSiteLink,
  sourcesNoResults,
  sourcesRange,
  sourcesSummary,
  type SourceHealthLabel,
  type SourceCounts,
} from './sources/source-words';
export {
  isAcceptableAddress,
  SOURCE_URL_REFUSALS,
  sourceFormValidation,
  type SourceFormEntries,
  type SourceFormErrors,
} from './sources/source-form-validation';
export {
  nothingRelevantNotice,
  sourceSavedNotice,
  sourceTestNotice,
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
export {
  issueMutedNotice,
  issueName,
  issueSeverityWord,
  issueTitle,
  issueWhen,
} from './overview/issue-words';
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
export {
  collectionEndedNotice,
  collectionRunningLine,
  collectionStartedText,
  type EndedNotice,
  type EndedRun,
} from './runs/run-now-words';
export {
  canRetryFailedSources,
  failedSources,
  namedFew,
  retryStartedNotice,
  summarizeRetry,
  type FailedSource,
} from './runs/retry-words';
export { cronLine, savedLine, thresholdHelp } from './settings/settings-words';
export { draftOf, settingsEqual, settingsOf, type SettingsDraft } from './settings/settings-draft';
export {
  fixFieldsBar,
  fixFieldsTitle,
  settingsValidation,
  type SettingsError,
  type SettingsField,
} from './settings/settings-validation';
export {
  isDuplicateKeyword,
  KEYWORD_WORDS,
  keywordError,
  type KeywordList,
  type KeywordWords,
} from './settings/keyword-words';
