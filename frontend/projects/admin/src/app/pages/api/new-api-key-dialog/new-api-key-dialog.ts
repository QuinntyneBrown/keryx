import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { API_CLIENTS_API, ApiProblem, type ApiScope, type CreatedApiClientDto } from '@kx/api';
import { Alert, Btn, Check, CheckList, Dialog, Field, Input } from '@kx/components';
import { apiKeyFormValidation, type ApiKeyFormErrors } from '@kx/domain';

/** The scopes in the order the dialog lists them, each with what it reads (L2-068 AC1). */
const SCOPES: readonly { value: ApiScope; description: string }[] = [
  { value: 'articles:read', description: 'Published articles and their summaries' },
  { value: 'events:read', description: 'Events with dates and places' },
  { value: 'sources:read', description: 'The list of organisations and sources' },
];

/** The name field's message when an active client already has the name (L2-068 AC6). */
const NAME_TAKEN =
  'A key with that name already exists. Revoke it first, or name this product differently.';

/** A control the dialog can move focus to. */
interface Focusable {
  focus(): void;
}

export const NEW_API_KEY_TITLE_ID = 'new-api-key-title';
export const NEW_API_KEY_DESCRIPTION_ID = 'new-api-key-description';

/**
 * "New API key" (docs/mocks/dialogs/new-api-key/, L2-068), opened by the API screen in a CDK
 * dialog. It closes with the created client and its key, which only the screen's store keeps.
 */
@Component({
  selector: 'kx-new-api-key-dialog',
  imports: [Alert, Btn, Check, CheckList, Dialog, Field, Input],
  templateUrl: './new-api-key-dialog.html',
  styleUrl: './new-api-key-dialog.scss',
})
export class NewApiKeyDialog {
  private readonly api = inject(API_CLIENTS_API);
  private readonly ref = inject<DialogRef<CreatedApiClientDto>>(DialogRef);
  private readonly changes = inject(ChangeDetectorRef);
  private readonly abort = new AbortController();

  protected readonly titleId = NEW_API_KEY_TITLE_ID;
  protected readonly descriptionId = NEW_API_KEY_DESCRIPTION_ID;
  protected readonly scopeOptions = SCOPES;
  protected readonly productName = signal('');
  protected readonly scopes = signal<readonly ApiScope[]>([]);
  /** Whether staff have tried to create: from then on each message follows its entry. */
  private readonly attempted = signal(false);
  /** The name the API refused as taken, for as long as the name is that one. */
  private readonly takenName = signal<string | null>(null);
  protected readonly errors = computed<ApiKeyFormErrors>(() => {
    const errors = this.attempted()
      ? apiKeyFormValidation({ productName: this.productName(), scopes: this.scopes() })
      : {};
    return !errors.productName && this.takenName() === this.productName()
      ? { productName: NAME_TAKEN, ...errors }
      : errors;
  });
  protected readonly nameDescribedBy = computed(() =>
    this.errors().productName ? 'key-name-help key-name-error' : 'key-name-help',
  );
  /** A request is in flight: every control is disabled and nothing closes the dialog. */
  protected readonly busy = signal(false);
  /** The last request failed for a reason staff cannot fix in the form. */
  protected readonly failure = signal<{ reference: string | null } | null>(null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
  }

  protected toggle(scope: ApiScope, checked: boolean): void {
    this.scopes.update((scopes) =>
      checked
        ? SCOPES.map((option) => option.value).filter(
            (value) => value === scope || scopes.includes(value),
          )
        : scopes.filter((value) => value !== scope),
    );
  }

  /** "Create key", or Enter in the name. With an entry wrong, nothing is sent and focus moves to the name (L2-068 AC2). */
  protected submit(name: Focusable): void {
    this.attempted.set(true);
    const errors = this.errors();
    if (errors.productName || errors.scopes) {
      name.focus();
      return;
    }
    void this.save(name);
  }

  /** Sends the request once; the dialog stays open, busy, until the answer (L2-068 AC3, AC6). */
  private async save(name: Focusable): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.failure.set(null);
    this.ref.disableClose = true;
    try {
      const created = await this.api.create(
        { product_name: this.productName().trim(), scopes: [...this.scopes()] },
        this.abort.signal,
      );
      this.ref.disableClose = false;
      this.ref.close(created);
    } catch (error) {
      if (this.abort.signal.aborted) return;
      this.ref.disableClose = false;
      this.busy.set(false);
      if (error instanceof ApiProblem && error.code === 'client_name_taken') {
        this.takenName.set(this.productName());
        // The name is enabled again before focus moves to it.
        this.changes.detectChanges();
        name.focus();
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
