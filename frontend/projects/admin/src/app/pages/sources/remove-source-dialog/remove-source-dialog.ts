import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import type { SourceListItem } from '@kx/api';
import { Alert, Btn, Dialog } from '@kx/components';
import { formatSourceAddress, removeSourceText } from '@kx/domain';
import { SourcesStore } from '../../../sources/sources-store';

/** The source "Remove source" asks about, and how many articles stay, when that was read. */
export interface RemoveSourceData {
  source: SourceListItem;
  articles: number | null;
}

export const REMOVE_SOURCE_TITLE_ID = 'remove-source-title';
export const REMOVE_SOURCE_DESCRIPTION_ID = 'remove-source-description';
/** Where focus starts: "Cancel", never the destructive button (L2-065 AC3). */
export const REMOVE_SOURCE_AUTO_FOCUS = '.dialog__foot .btn';

/**
 * "Remove source" (docs/mocks/dialogs/remove-source/, L2-065), opened by Sources in a CDK dialog.
 * The screen's store removes the source and says so; the dialog closes with `true` when it went,
 * and otherwise shows the failure with its reference.
 */
@Component({
  selector: 'kx-remove-source-dialog',
  imports: [Alert, Btn, Dialog],
  templateUrl: './remove-source-dialog.html',
  styleUrl: './remove-source-dialog.scss',
})
export class RemoveSourceDialog {
  private readonly store = inject(SourcesStore);
  private readonly ref = inject<DialogRef<boolean>>(DialogRef);
  private readonly data = inject<RemoveSourceData>(DIALOG_DATA);

  protected readonly titleId = REMOVE_SOURCE_TITLE_ID;
  protected readonly descriptionId = REMOVE_SOURCE_DESCRIPTION_ID;
  protected readonly title = `Remove ${this.data.source.organization_name}?`;
  protected readonly description = `Keryx stops checking ${formatSourceAddress(
    this.data.source.kind,
    this.data.source.address,
  )}.`;
  protected readonly text = removeSourceText(this.data.articles);
  /** A request is in flight: nothing closes the dialog and one request is sent. */
  protected readonly busy = signal(false);
  /** The last removal failed; the source is still listed (L2-065 AC5). */
  protected readonly failure = signal<{ reference: string | null } | null>(null);

  protected async confirm(): Promise<void> {
    this.busy.set(true);
    this.failure.set(null);
    this.ref.disableClose = true;
    const failure = await this.store.remove(this.data.source);
    this.ref.disableClose = false;
    if (!failure) {
      this.ref.close(true);
      return;
    }
    this.busy.set(false);
    this.failure.set(failure);
  }

  protected close(): void {
    if (this.busy()) return;
    this.ref.close();
  }
}
