import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { RUNS_API } from '@kx/api';
import { List, PageBody, PageHeader, Rail, type RunListItem } from '@kx/components';
import {
  formatCollectedAt,
  runFailedFlag,
  runLineMeta,
  runOutcomeLabel,
  runTone,
  scheduleLine,
} from '@kx/domain';
import { RunsStore } from '../../runs/runs-store';
import { HttpRunsApi } from '../../runs/http-runs-api';
import { ScheduleStore } from '../../schedule/schedule-store';

/** Toronto's zone, which Keryx's times read in until the schedule names it. */
const TORONTO = 'America/Toronto';

/**
 * Runs (docs/mocks/pages/runs/): the most recent collection runs beside the chosen run's detail
 * (L2-056), under the schedule.
 */
@Component({
  selector: 'kx-runs',
  imports: [List, PageBody, PageHeader, Rail],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
  providers: [{ provide: RUNS_API, useClass: HttpRunsApi }, RunsStore],
})
export default class Runs {
  protected readonly store = inject(RunsStore);
  private readonly schedule = inject(ScheduleStore);
  private readonly router = inject(Router);

  /** "Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)" once the schedule is read. */
  protected readonly description = computed(() => {
    const schedule = this.schedule.schedule();
    return schedule ? scheduleLine(schedule, this.schedule.now()) : undefined;
  });

  /** A row per run, newest first: its outcome, its start, what it did and its failed sources. */
  protected readonly runs = computed((): RunListItem[] => {
    const page = this.store.page();
    if (!page) return [];
    const zone = this.schedule.schedule()?.time_zone ?? TORONTO;
    const now = this.schedule.now();
    return page.items.map((run) => ({
      id: run.id,
      href: `/runs?run=${run.id}`,
      current: false,
      tone: runTone(run.outcome) as RunListItem['tone'],
      word: runOutcomeLabel(run.outcome),
      title: formatCollectedAt(run.started_at, now, zone),
      meta: runLineMeta(run),
      flag: runFailedFlag(run.sources_failed),
    }));
  });

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  constructor() {
    this.schedule.load();
    void this.store.load();
  }
}
