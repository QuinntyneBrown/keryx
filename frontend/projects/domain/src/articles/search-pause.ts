/** How long typing must pause before a search is sent (L2-050 AC1). */
export const SEARCH_PAUSE_MS = 300;

/** Runs the last search given once typing has paused; `cancel` drops the pending one. */
export interface SearchPause {
  run(search: () => void): void;
  cancel(): void;
}

/** A pause for one search field: each `run` cancels the search pending before it. */
export function searchPause(): SearchPause {
  let timer: ReturnType<typeof setTimeout> | undefined;
  return {
    run(search) {
      clearTimeout(timer);
      timer = setTimeout(search, SEARCH_PAUSE_MS);
    },
    cancel() {
      clearTimeout(timer);
    },
  };
}
