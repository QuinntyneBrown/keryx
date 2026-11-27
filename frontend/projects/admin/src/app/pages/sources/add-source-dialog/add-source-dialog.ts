import { DialogRef } from '@angular/cdk/dialog';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { SOURCES_API, type Region, type SourceKind } from '@kx/api';
import {
  Alert,
  Btn,
  ChoiceCard,
  ChoiceGrid,
  Dialog,
  Field,
  FieldGrid,
  Input,
  Select,
} from '@kx/components';

/** The kinds staff can add, in the order of the cards; `news_query` is seeded only. */
const KINDS: readonly { value: SourceKind; label: string }[] = [
  { value: 'rss', label: 'RSS' },
  { value: 'website', label: 'Website' },
  { value: 'instagram', label: 'Instagram' },
  { value: 'eventbrite', label: 'Eventbrite' },
];

/** The ids the CDK container is labelled and described by. */
export const ADD_SOURCE_TITLE_ID = 'add-source-title';
export const ADD_SOURCE_DESCRIPTION_ID = 'add-source-description';

/**
 * "Add a source" (docs/mocks/dialogs/add-source/, L2-064), opened by Sources in a CDK dialog.
 * It reads the regions when it opens and holds its entries in signals.
 */
@Component({
  selector: 'kx-add-source-dialog',
  imports: [Alert, Btn, ChoiceCard, ChoiceGrid, Dialog, Field, FieldGrid, Input, Select],
  templateUrl: './add-source-dialog.html',
  styleUrl: './add-source-dialog.scss',
})
export class AddSourceDialog {
  private readonly api = inject(SOURCES_API);
  private readonly ref = inject(DialogRef);
  private readonly abort = new AbortController();

  protected readonly titleId = ADD_SOURCE_TITLE_ID;
  protected readonly descriptionId = ADD_SOURCE_DESCRIPTION_ID;
  protected readonly kinds = KINDS;
  protected readonly kind = signal<SourceKind>('rss');
  protected readonly url = signal('');
  protected readonly organisation = signal('');
  protected readonly region = signal('');
  private readonly regions = signal<Region[]>([]);
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

  protected close(): void {
    this.ref.close();
  }
}
