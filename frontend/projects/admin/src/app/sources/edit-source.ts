import { Injector } from '@angular/core';
import { ApiProblem, NOTIFIER, SOURCES_API, type SourceSaved } from '@kx/api';
import { lastFetchFailure, sourceSavedNotice } from '@kx/domain';
import type { EditSourceData } from '../pages/sources/edit-source-dialog/edit-source-dialog';
import { ScheduleStore } from '../schedule/schedule-store';
import { HttpSourcesApi } from './http-sources-api';

/** Toronto's zone, which Keryx's times read in until the schedule names it. */
const TORONTO = 'America/Toronto';

/** The source "Edit source" opens on: its stored values and, for the alert, its health. */
export type EditableSource = EditSourceData['source'] & {
  health: string;
  failure_reason: string | null;
  last_fetched_at: string | null;
};

/**
 * Opens "Edit source" on a source, from Sources, Runs or a notification (L2-065). The dialog and
 * the CDK dialog load when first opened, the dialog reaches the admin API through its own
 * binding, and focus returns to what had it. A save says how the test of the saved address went
 * (`sourceSavedNotice`) and resolves with what was saved.
 */
export async function openEditSource(
  injector: Injector,
  source: EditableSource,
): Promise<SourceSaved | undefined> {
  const opener = document.activeElement;
  const schedule = injector.get(ScheduleStore);
  const data: EditSourceData = {
    source,
    lastFetch: lastFetchFailure(
      source as Parameters<typeof lastFetchFailure>[0],
      schedule.now(),
      schedule.schedule()?.time_zone ?? TORONTO,
    ),
  };
  const [{ Dialog }, dialog] = await Promise.all([
    import('@angular/cdk/dialog'),
    import('../pages/sources/edit-source-dialog/edit-source-dialog'),
  ]);
  const ref = injector.get(Dialog).open<SourceSaved>(dialog.EditSourceDialog, {
    injector: Injector.create({
      providers: [{ provide: SOURCES_API, useClass: HttpSourcesApi, deps: [] }],
      parent: injector,
    }),
    data,
    ariaModal: true,
    ariaLabelledBy: dialog.EDIT_SOURCE_TITLE_ID,
    ariaDescribedBy: dialog.EDIT_SOURCE_DESCRIPTION_ID,
    autoFocus: '#source-url',
    restoreFocus: opener instanceof HTMLElement ? opener : true,
    backdropClass: 'kx-backdrop',
  });
  return new Promise((resolve) =>
    ref.closed.subscribe((saved) => {
      if (saved) injector.get(NOTIFIER).notify(sourceSavedNotice(source.kind, saved.test));
      resolve(saved);
    }),
  );
}

/**
 * Opens "Edit source" on a source known by its id, as an issue on Runs or a notification knows
 * it: the source is read first. One removed meanwhile is said; a read that fails is said with
 * "Try again". `name` is what the caller calls it, for those notifications.
 */
export async function editSourceById(
  injector: Injector,
  id: string,
  name: string,
): Promise<SourceSaved | undefined> {
  const notifier = injector.get(NOTIFIER);
  let source: EditableSource;
  try {
    source = await new HttpSourcesApi().source(id, new AbortController().signal);
  } catch (error) {
    if (error instanceof ApiProblem && error.code === 'source_not_found') {
      notifier.notify({
        tone: 'danger',
        title: 'Source not found',
        text: `${name} was removed meanwhile.`,
      });
      return undefined;
    }
    const reference = error instanceof ApiProblem ? error.reference : undefined;
    notifier.notify({
      tone: 'danger',
      title: 'Keryx couldn’t open the source',
      text: `Try again in a moment.${reference ? ` Reference ${reference}` : ''}`,
      action: { label: 'Try again', run: () => void editSourceById(injector, id, name) },
    });
    return undefined;
  }
  return openEditSource(injector, source);
}
