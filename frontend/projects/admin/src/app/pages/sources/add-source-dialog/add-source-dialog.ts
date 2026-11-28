import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import {
  ApiProblem,
  SOURCES_API,
  type Region,
  type SourceKind,
  type SourceTestResult,
} from '@kx/api';
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
import {
  isAcceptableAddress,
  SOURCE_URL_REFUSALS,
  sourceFormValidation,
  testFetchSummary,
  type SourceFormErrors,
} from '@kx/domain';

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
  kind: SourceKind;
  /** The test fetch of the address that was added, when one finished. */
  test: SourceTestResult | null;
}

/** The hint under the fields before and between tests (docs/mocks/dialogs/add-source/). */
type Hint =
  | { state: 'idle' }
  | { state: 'invalid' }
  | { state: 'tested'; kind: SourceKind; address: string; result: SourceTestResult };

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
  private readonly hintState = signal<Hint>({ state: 'idle' });
  /** The address and kind of the test in flight or done, so each pair is tested once. */
  private tested: string | null = null;
  protected readonly hint = computed(() => {
    const hint = this.hintState();
    if (hint.state === 'invalid') {
      return { tone: 'neutral' as const, text: 'The test fetch runs once the URL is valid.' };
    }
    if (
      hint.state === 'tested' &&
      hint.kind === this.kind() &&
      hint.address === this.url().trim()
    ) {
      return testFetchSummary(hint.kind, hint.result);
    }
    return {
      tone: 'neutral' as const,
      text: 'Paste a URL and Keryx runs a test fetch before anything is added.',
    };
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
   * The URL lost focus: a valid address is test-fetched once for its kind and the result is
   * announced politely; an invalid one runs no test (L2-064 AC2). A failed call keeps the hint.
   */
  protected async testUrl(): Promise<void> {
    const address = this.url().trim();
    const kind = this.kind();
    if (!isAcceptableAddress(address)) {
      this.tested = null;
      this.hintState.set({ state: 'invalid' });
      return;
    }
    const key = `${kind} ${address}`;
    if (this.tested === key) return;
    this.tested = key;
    if (this.hintState().state === 'invalid') this.hintState.set({ state: 'idle' });
    try {
      const result = await this.api.testAddress(kind, address, this.abort.signal);
      if (this.tested === key) this.hintState.set({ state: 'tested', kind, address, result });
    } catch {
      if (this.tested === key) this.tested = null;
    }
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
      const hint = this.hintState();
      this.ref.close({
        organisation: created.organization_name,
        kind: this.kind(),
        test:
          hint.state === 'tested' && hint.kind === this.kind() && hint.address === address
            ? hint.result
            : null,
      });
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
