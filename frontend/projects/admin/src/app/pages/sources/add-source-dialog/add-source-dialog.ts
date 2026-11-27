import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { ApiProblem, SOURCES_API, type Region, type SourceKind } from '@kx/api';
import {
  Alert,
  Btn,
  ChoiceCard,
  ChoiceGrid,
  Dialog,
  Field,
  FieldGrid,
  Icon,
  type IconName,
  Input,
  Select,
} from '@kx/components';
import { sourceFormValidation, type SourceFormErrors } from '@kx/domain';

/** The kinds staff can add, in the order of the cards; `news_query` is seeded only. */
const KINDS: readonly { value: SourceKind; label: string; icon: IconName }[] = [
  { value: 'rss', label: 'RSS', icon: 'rss' },
  { value: 'website', label: 'Website', icon: 'globe' },
  { value: 'instagram', label: 'Instagram', icon: 'instagram' },
  { value: 'eventbrite', label: 'Eventbrite', icon: 'calendar' },
];

/** A control the dialog can move focus to. */
interface Focusable {
  focus(): void;
}

/** What the dialog closes with when the source was added. */
export interface SourceAdded {
  organisation: string;
}

/** The URL field's message for a refusal the API words by its code (L2-064 AC7, L2-024). */
const URL_REFUSALS: Readonly<Record<string, string>> = {
  source_exists: 'This source is already in the list.',
  unsafe_address: 'Keryx can’t fetch this address: it isn’t on the public internet.',
};

/** The ids the CDK container is labelled and described by. */
export const ADD_SOURCE_TITLE_ID = 'add-source-title';
export const ADD_SOURCE_DESCRIPTION_ID = 'add-source-description';

/**
 * "Add a source" (docs/mocks/dialogs/add-source/, L2-064), opened by Sources in a CDK dialog.
 * It reads the regions when it opens and holds its entries in signals.
 */
@Component({
  selector: 'kx-add-source-dialog',
  imports: [Alert, Btn, ChoiceCard, ChoiceGrid, Dialog, Field, FieldGrid, Icon, Input, Select],
  templateUrl: './add-source-dialog.html',
  styleUrl: './add-source-dialog.scss',
})
export class AddSourceDialog {
  private readonly api = inject(SOURCES_API);
  private readonly ref = inject<DialogRef<SourceAdded>>(DialogRef);
  private readonly changes = inject(ChangeDetectorRef);
  private readonly abort = new AbortController();

  protected readonly titleId = ADD_SOURCE_TITLE_ID;
  protected readonly descriptionId = ADD_SOURCE_DESCRIPTION_ID;
  protected readonly kinds = KINDS;
  protected readonly kind = signal<SourceKind>('rss');
  protected readonly url = signal('');
  protected readonly organisation = signal('');
  protected readonly region = signal('');
  private readonly regions = signal<Region[]>([]);
  /** Whether staff have tried to add: from then on each field's error follows its entry. */
  private readonly attempted = signal(false);
  /** The API's refusal of the URL, for as long as the URL is the one it refused. */
  private readonly refusedUrl = signal<{ url: string; message: string } | null>(null);
  protected readonly errors = computed<SourceFormErrors>(() => {
    const errors = this.attempted()
      ? sourceFormValidation({
          url: this.url(),
          organisation: this.organisation(),
          region: this.region(),
        })
      : {};
    const refused = this.refusedUrl();
    return !errors.url && refused?.url === this.url()
      ? { url: refused.message, ...errors }
      : errors;
  });
  /** A request is in flight: every control is disabled and nothing closes the dialog. */
  protected readonly busy = signal(false);
  /** The last request failed for a reason staff cannot fix in the form. */
  protected readonly failure = signal<{ reference: string | null } | null>(null);
  protected readonly regionOptions = computed(() =>
    this.regions().map((region) => ({ value: region.slug, label: region.name })),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
    this.api.regions(this.abort.signal).then(
      (regions) => this.regions.set(regions),
      () => undefined,
    );
  }

  /**
   * "Add source", or Enter in a field. With an entry wrong, nothing is sent, each wrong field
   * says why and focus moves to the first of them; no entry is cleared (L2-064 AC3).
   */
  protected submit(url: Focusable, organisation: Focusable, region: Focusable): void {
    this.attempted.set(true);
    const errors = this.errors();
    const first = errors.url
      ? url
      : errors.organisation
        ? organisation
        : errors.region
          ? region
          : null;
    if (first) {
      first.focus();
      return;
    }
    void this.save(url);
  }

  /** Sends the source once; the dialog stays open, busy, until the answer (L2-064 AC4 to AC7). */
  private async save(url: Focusable): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.failure.set(null);
    this.ref.disableClose = true;
    const address = this.url().trim();
    try {
      const created = await this.api.create(
        {
          kind: this.kind(),
          address,
          organization_name: this.organisation().trim(),
          region: this.region(),
        },
        this.abort.signal,
      );
      this.ref.disableClose = false;
      this.ref.close({ organisation: created.organization_name });
    } catch (error) {
      if (this.abort.signal.aborted) return;
      this.ref.disableClose = false;
      this.busy.set(false);
      const message =
        error instanceof ApiProblem && error.code ? URL_REFUSALS[error.code] : undefined;
      if (message) {
        this.refusedUrl.set({ url: this.url(), message });
        // The URL is enabled again before focus moves to it.
        this.changes.detectChanges();
        url.focus();
        return;
      }
      this.failure.set({
        reference: error instanceof ApiProblem ? (error.reference ?? null) : null,
      });
    }
  }

  protected close(): void {
    if (this.busy()) return;
    this.ref.close();
  }
}
