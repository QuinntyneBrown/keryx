import { Component, computed, inject } from '@angular/core';
import { OVERVIEW_API } from '@kx/api';
import { PageBody, PageHeader, StatusCard, type StatItem } from '@kx/components';
import {
  formatClockTime,
  formatCollectedAt,
  formatCountdown,
  formatDuration,
  runHeadline,
  runTone,
} from '@kx/domain';
import { HttpOverviewApi } from '../../overview/http-overview-api';
import { OverviewStore } from '../../overview/overview-store';

/**
 * Overview (docs/mocks/pages/overview/): how the last run went and when the next one is
 * (L2-043). Times read in the zone the status names, Toronto's.
 */
@Component({
  selector: 'kx-overview',
  imports: [PageBody, PageHeader, StatusCard],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  providers: [{ provide: OVERVIEW_API, useClass: HttpOverviewApi }, OverviewStore],
})
export default class Overview {
  protected readonly store = inject(OverviewStore);

  /** The status card's words and stats for the last run. */
  protected readonly card = computed(() => {
    const status = this.store.status();
    const run = status?.last_run;
    if (!status || !run) return null;
    const now = this.store.now();
    const stats: StatItem[] = [
      { label: 'New articles', value: String(run.added) },
      { label: 'Sources OK', value: String(run.sources_ok), unit: `/${run.sources_total}` },
      {
        label: 'Next run',
        value: formatClockTime(status.next_run_at, status.time_zone),
        sub: formatCountdown(now, status.next_run_at),
        size: 'sm',
      },
    ];
    return {
      eyebrow: 'Last run',
      headline: runHeadline(run),
      meta: `${formatCollectedAt(run.started_at, now, status.time_zone)} · took ${formatDuration(run.duration_ms ?? 0)}`,
      reference: run.id,
      tone: runTone(run.outcome),
      stats,
    };
  });

  constructor() {
    void this.store.load();
  }
}
