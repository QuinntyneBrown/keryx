import { Component, computed, input } from '@angular/core';
import type { SourceKind } from '@kx/api';
import { Icon, type IconName } from '../icon/icon';

const KIND_ICONS: Record<SourceKind, IconName> = {
  rss: 'rss',
  website: 'globe',
  instagram: 'instagram',
  eventbrite: 'calendar',
  news_query: 'search',
};

/**
 * The source cell (docs/design-system/components/source.html): a tile with the kind's icon, the
 * organisation's name, and where Keryx reads it in mono after a hidden kind ("RSS: …"). Both
 * lines truncate. What is projected follows them: the health on narrow screens. Text only.
 *
 * The lines keep the mock's `table__title` and `table__sub` classes, which e2e locators use, and
 * are styled by this block's own `source__name` and `source__address`.
 */
@Component({
  selector: 'kx-source',
  imports: [Icon],
  templateUrl: './source.html',
  styleUrl: './source.scss',
})
export class Source {
  readonly kind = input.required<SourceKind>();
  /** The kind in words, read before the address: "RSS". */
  readonly kindLabel = input.required<string>();
  readonly name = input.required<string>();
  /** Where Keryx reads it, without scheme or "www.": "redeemedrun.ca/feed". */
  readonly address = input.required<string>();

  protected readonly icon = computed(() => KIND_ICONS[this.kind()]);
}
