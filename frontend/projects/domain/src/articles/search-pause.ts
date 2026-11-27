/** How long typing must pause before a search is sent (L2-050 AC1). */
export const SEARCH_PAUSE_MS = 300;

/** How long a first load may take before skeleton rows stand in for the list (L2-055 AC1). */
export const SKELETON_DELAY_MS = 300;

/** Runs the last action given once a pause has passed; `cancel` drops the pending one. */
export interface SearchPause {
  run(action: () => void): void;
  cancel(): void;
}

function afterPause(ms: number): SearchPause {
  let timer: ReturnType<typeof setTimeout> | undefined;
  return {
    run(action) {
      clearTimeout(timer);
      timer = setTimeout(action, ms);
    },
    cancel() {
      clearTimeout(timer);
    },
  };
}

/** A pause for one search field: each `run` cancels the search pending before it. */
export function searchPause(): SearchPause {
  return afterPause(SEARCH_PAUSE_MS);
}

/** The wait before skeleton rows show; cancelled when the answer comes first. */
export function skeletonDelay(): SearchPause {
  return afterPause(SKELETON_DELAY_MS);
}
