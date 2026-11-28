import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { ApiProblem, SOURCES_API, type Region, type SourceKind, type SourceSaved } from '@kx/api';
import {
  Alert,
  Btn,
  ChoiceCard,
  ChoiceGrid,
  Dialog,
  Field,
  FieldGrid,
  Icon,
  Input,
  KIND_ICONS,
  Select,
} from '@kx/components';
import {
  SOURCE_URL_REFUSALS,
  sourceFormValidation,
  sourceKindLabel,
  type SourceFormErrors,
} from '@kx/domain';

/** The source "Edit source" opens on, and the alert about its last failed fetch, if any. */
export interface EditSourceData {
  source: {
    id: string;
    kind: SourceKind;
    address: string;
    organization_name: string;
    region: string;
  };
  lastFetch: { title: string; text: string } | null;
}

/** A control the dialog can move focus to. */
interface Focusable {
  focus(): void;
}

/** The ids the CDK container is labelled and described by. */
export const EDIT_SOURCE_TITLE_ID = 'edit-source-title';
export const EDIT_SOURCE_DESCRIPTION_ID = 'edit-source-description';

/**
 * "Edit source" (docs/mocks/dialogs/edit-source/, L2-065), opened in a CDK dialog by Sources. It
 * holds the source's values in signals, and saving follows "Add a source": one request while
 * busy, each wrong field said and focused, a refused URL marked, and a failure kept in the dialog
 * with every entry (L2-064). The kind is shown but not editable (L2-003).
 */
@Component({
  selector: 'kx-edit-source-dialog',
  imports: [Alert, Btn, ChoiceCard, ChoiceGrid, Dialog, Field, FieldGrid, Icon, Input, Select],
  templateUrl: './edit-source-dialog.html',
  styleUrl: './edit-source-dialog.scss',
})
export class EditSourceDialog {
  private readonly api = inject(SOURCES_API);
  private readonly ref = inject<DialogRef<SourceSaved>>(DialogRef);
  private readonly changes = inject(ChangeDetectorRef);
  private readonly abort = new AbortController();
  protected readonly data = inject<EditSourceData>(DIALOG_DATA);

  protected readonly titleId = EDIT_SOURCE_TITLE_ID;
  protected readonly descriptionId = EDIT_SOURCE_DESCRIPTION_ID;
  protected readonly kind = this.data.source.kind;
  protected readonly kindLabel = sourceKindLabel(this.kind);
  protected readonly kindIcon = KIND_ICONS[this.kind];
  protected readonly newsQuery = this.kind === 'news_query';
  protected readonly url = signal(this.data.source.address);
  protected readonly organisation = signal(this.data.source.organization_name);
  protected readonly region = signal(this.data.source.region);
  private readonly regions = signal<Region[]>([]);
  /** Whether staff have tried to save: from then on each field's error follows its entry. */
  private readonly attempted = signal(false);
  /** The API's refusal of the URL, for as long as the URL is the one it refused. */
  private readonly refusedUrl = signal<{ url: string; message: string } | null>(null);
  protected readonly errors = computed<SourceFormErrors>(() => {
    const errors = this.attempted()
      ? sourceFormValidation({
          url: this.url(),
          organisation: this.organisation(),
          region: this.region(),
          newsQuery: this.newsQuery,
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

  /** "Save source", or Enter in a field: with an entry wrong, nothing is sent (L2-064 AC3). */
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

  /** Sends the changes once; the dialog stays open, busy, until the answer (L2-065 AC2). */
  private async save(url: Focusable): Promise<void> {
    this.busy.set(true);
    this.failure.set(null);
    this.ref.disableClose = true;
    try {
      const saved = await this.api.update(
        this.data.source.id,
        {
          address: this.url().trim(),
          organization_name: this.organisation().trim(),
          region: this.region(),
        },
        this.abort.signal,
      );
      this.ref.disableClose = false;
      this.ref.close(saved);
    } catch (error) {
      if (this.abort.signal.aborted) return;
      this.ref.disableClose = false;
      this.busy.set(false);
      const message =
        error instanceof ApiProblem && error.code ? SOURCE_URL_REFUSALS[error.code] : undefined;
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
