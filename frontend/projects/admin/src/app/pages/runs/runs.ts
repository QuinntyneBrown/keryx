import { Component, computed, DestroyRef, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RUNS_API } from '@kx/api';
import {
  Btn,
  Card,
  Empty,
  Icon,
  List,
  PageBody,
  PageHeader,
  Rail,
  Segmented,
  Stack,
  Stats,
  Timeline,
  type IssueListItem,
  type RunListItem,
  type SegmentedOption,
  type TimelineStep,
  type StatItem,
} from '@kx/components';
import {
  formatCollectedAt,
  formatDuration,
  issueSeverityWord,
  issueStreakLabel,
  issueTitle,
  PIPELINE_STAGES,
  runFailedFlag,
  runLineMeta,
  runOutcomeLabel,
  runTone,
  scheduleLine,
  stageDetail,
  stageName,
  stageState,
  stageStatusLabel,
  stageTime,
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
  imports: [
    Btn,
    Card,
    Empty,
    Icon,
    List,
    PageBody,
    PageHeader,
    Rail,
    Segmented,
    Stack,
    Stats,
    Timeline,
  ],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
  providers: [{ provide: RUNS_API, useClass: HttpRunsApi }, RunsStore],
})
export default class Runs {
  protected readonly store = inject(RunsStore);
  private readonly schedule = inject(ScheduleStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** The zone times read in: the schedule's, or Toronto's until it is read. */
  private readonly zone = computed(() => this.schedule.schedule()?.time_zone ?? TORONTO);

  /** "Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)" once the schedule is read. */
  protected readonly description = computed(() => {
    const schedule = this.schedule.schedule();
    return schedule ? scheduleLine(schedule, this.schedule.now()) : undefined;
  });

  /** A row per run, newest first: its outcome, its start, what it did and its failed sources. */
  protected readonly runs = computed((): RunListItem[] => {
    const page = this.store.page();
    if (!page) return [];
    const zone = this.zone();
    const now = this.schedule.now();
    const selected = this.store.selected();
    return page.items.map((run) => ({
      id: run.id,
      href: this.store.problems() ? `/runs?problems=true&run=${run.id}` : `/runs?run=${run.id}`,
      current: run.id === selected,
      tone: runTone(run.outcome) as RunListItem['tone'],
      word: runOutcomeLabel(run.outcome),
      title: formatCollectedAt(run.started_at, now, zone),
      meta: runLineMeta(run),
      flag: runFailedFlag(run.sources_failed),
    }));
  });

  /** "All runs" and "With problems", the one shown current (L2-056 AC2). */
  protected readonly filters = computed((): SegmentedOption[] => {
    const problems = this.store.problems() === true;
    return [
      { label: 'All runs', href: '/runs', current: !problems },
      { label: 'With problems', href: '/runs?problems=true', current: problems },
    ];
  });

  /** The chosen run's card: its start, its outcome, its reference and how it ran (L2-057 AC1). */
  protected readonly detail = computed(() => {
    const run = this.store.detail();
    if (!run) return null;
    const tiles: StatItem[] = [
      { label: 'New articles', value: String(run.added), size: 'md' },
      { label: 'Duplicates skipped', value: String(run.duplicates), size: 'md' },
      { label: 'Sources OK', value: String(run.sources_ok), size: 'md' },
      {
        label: 'Sources failed',
        value: String(run.sources_failed),
        size: 'md',
        ...(run.sources_failed > 0 ? { tone: 'danger' as const } : {}),
      },
    ];
    return {
      heading: formatCollectedAt(run.started_at, this.schedule.now(), this.zone()),
      badge: { text: runOutcomeLabel(run.outcome), tone: runTone(run.outcome) },
      reference: run.id,
      meta: `${run.trigger === 'manual' ? 'Manual' : 'Schedule'} · took ${formatDuration(run.duration_ms ?? 0)}`,
      tiles,
      // The six stages in the pipeline's order, whatever order they were served in (L2-057 AC2).
      steps: PIPELINE_STAGES.flatMap((name): TimelineStep[] => {
        const result = run.stages.find((stage) => stage.stage === name);
        if (!result) return [];
        return [
          {
            id: name,
            state: stageState(result.status),
            word: stageStatusLabel(result.status),
            name: stageName(name),
            time: stageTime(result),
            detail: stageDetail(result),
          },
        ];
      }),
      issues: run.issues.map((issue): IssueListItem => ({
        id: issue.id,
        tone: issue.severity === 'error' ? 'danger' : 'warning',
        word: issueSeverityWord(issue.severity),
        title: issueTitle(issue.kind),
        subject: issue.subject_name,
        detail: issue.detail,
        streak: issueStreakLabel(issue.severity, issue.consecutive_runs),
      })),
    };
  });

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  constructor() {
    this.schedule.load();
    const subscription = this.route.queryParamMap.subscribe((params) =>
      this.store.request(params.get('run'), params.get('problems') === 'true'),
    );
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }
}
