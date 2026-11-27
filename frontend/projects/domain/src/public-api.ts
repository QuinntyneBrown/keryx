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
