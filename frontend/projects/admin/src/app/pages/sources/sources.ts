import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SOURCES_API, type SourceListItem } from '@kx/api';
import {
  Btn,
  Empty,
  Health,
  Icon,
  PageBody,
  PageHeader,
  Source,
  Table,
  type TableColumn,
} from '@kx/components';
import {
  formatSourceAddress,
  sourceHealthLabel,
  sourceKindLabel,
  sourcesSummary,
} from '@kx/domain';
import { HttpSourcesApi } from '../../sources/http-sources-api';
import { SourcesStore } from '../../sources/sources-store';

/** What the header says when it cannot count the catalogue: while loading, or after a failure. */
const PURPOSE = 'Feeds, pages and queries Keryx checks on every run.';

/**
 * Sources (docs/mocks/pages/sources/). Journey 3 builds the header with "Add source", the empty
 * and error states, and a table of each source's name, address, health and region, failing
 * first. The kind chips, yield, last fetch, row menu and paging arrive with journey 17 (ADR 0008).
 */
@Component({
  selector: 'kx-sources',
  imports: [Btn, Empty, Health, Icon, PageBody, PageHeader, Source, Table],
  providers: [{ provide: SOURCES_API, useClass: HttpSourcesApi }, SourcesStore],
  templateUrl: './sources.html',
  styleUrl: './sources.scss',
})
export default class Sources {
  private readonly router = inject(Router);
  protected readonly store = inject(SourcesStore);

  protected readonly columns: TableColumn[] = [
    { key: 'source', label: 'Source', primary: true },
    { key: 'health', label: 'Health', wide: true },
    { key: 'region', label: 'Region', wide: true, muted: true },
  ];

  protected readonly description = computed(() => {
    const state = this.store.state();
    if (state.status !== 'ready') return PURPOSE;
    const { all_count: all, failing_count: failing, needs_look_count: needsLook } = state.page;
    return sourcesSummary({ all, failing, needsLook });
  });

  protected readonly rows = computed(() => {
    const state = this.store.state();
    if (state.status !== 'ready') return [];
    return state.page.items.map((item) => ({
      item,
      kindLabel: sourceKindLabel(item.kind),
      address: formatSourceAddress(item.kind, item.address),
      health: sourceHealthLabel(item.health, item.held_count),
    }));
  });

  protected readonly rowKey = (row: unknown) => (row as { item: SourceListItem }).item.id;

  constructor() {
    void this.store.load();
  }

  protected retry(): void {
    void this.store.load();
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
