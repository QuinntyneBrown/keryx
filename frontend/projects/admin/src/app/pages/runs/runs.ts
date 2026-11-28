import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RUNS_API } from '@kx/api';
import {
  Alert,
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
  collectionRunningLine,
  formatClockTime,
  formatCollectedAt,
  issueSeverityWord,
  issueStreakLabel,
  issueTitle,
  PIPELINE_STAGES,
  runFailedFlag,
  runElapsedMs,
  runHowLine,
  runLineMeta,
  runningStageMs,
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
import { CollectionWatcher } from '../../schedule/collection-watcher';
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
    Alert,
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
  protected readonly schedule = inject(ScheduleStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** The run the URL names: below 1024 px it takes the list's place. */
  protected readonly viewing = signal<string | null>(null);

  /** Back to the list, keeping its filter (L2-056 AC7). */
  protected readonly backHref = computed(() =>
    this.store.problems() ? '/runs?problems=true' : '/runs',
  );

  /** The zone times read in: the schedule's, or Toronto's until it is read. */
  private readonly zone = computed(() => this.schedule.schedule()?.time_zone ?? TORONTO);

  /** "Scheduled every 6 hours · next at 12:00 PM (in 2h 14m)" once the schedule is read. */
  protected readonly description = computed(() => {
    const active = this.schedule.active();
    if (active) return collectionRunningLine(active.startedAt, this.zone());
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
      meta: runLineMeta(run, now),
      flag: runFailedFlag(run.sources_failed),
    }));
  });

  /** The shapes of the detail's four tiles, which their skeletons follow while it loads. */
  protected readonly loadingTiles: readonly StatItem[] = [
    { label: 'New articles', value: '', size: 'md' },
    { label: 'Duplicates skipped', value: '', size: 'md' },
    { label: 'Sources OK', value: '', size: 'md' },
    { label: 'Sources failed', value: '', size: 'md' },
  ];

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
    const now = this.schedule.now();
    const running = run.outcome === 'running';
    const elapsed = runElapsedMs(run.started_at, now);
    const progress = running
      ? { checked: run.sources_checked, total: run.sources_total, serving: this.serving() }
      : null;
    const stageMs = running ? runningStageMs(run.stages, elapsed) : 0;
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
      heading: formatCollectedAt(run.started_at, now, this.zone()),
      badge: { text: runOutcomeLabel(run.outcome), tone: runTone(run.outcome) },
      reference: run.id,
      meta: runHowLine(run, now),
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
            time: stageTime(result, stageMs),
            detail: stageDetail(result, progress),
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

  /**
   * When the run the API serves while one is in progress started, as "6:00 AM": the newest
   * listed run that finished and did not fail.
   */
  private readonly serving = computed(() => {
    const run = this.store
      .page()
      ?.items.find(
        (item) => item.outcome === 'succeeded' || item.outcome === 'completed_with_issues',
      );
    return run ? formatClockTime(run.started_at, this.zone()) : null;
  });

  /** The empty state's words, naming the first scheduled time once the schedule is read. */
  protected readonly emptyText = computed(() => {
    const schedule = this.schedule.schedule();
    const first = schedule
      ? `at ${formatClockTime(schedule.next_run_at, schedule.time_zone)}`
      : 'at the next scheduled time';
    return `A run is one pass over every source. The first one starts ${first}. Each run is recorded here with what it added and what went wrong.`;
  });

  /** The chosen run's start, which names its detail even when the detail failed. */
  protected readonly selectedTitle = computed(() => {
    const id = this.store.selected();
    return this.runs().find((run) => run.id === id)?.title ?? '';
  });

  protected retry(): void {
    void this.store.retry();
  }

  protected retryDetail(): void {
    void this.store.retryDetail();
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  /** Fetches the chosen run's failed sources again and shows the run it runs as (L2-059 AC1). */
  protected retryFailedSources(): void {
    void this.store.retryFailedSources((id) => {
      if (id === this.store.selected()) {
        this.store.reload();
        return;
      }
      this.store.refresh();
      void this.router.navigateByUrl(`/runs?run=${encodeURIComponent(id)}`);
    });
  }

  /** Starts a run and shows it, first in the list and chosen (L2-047 AC1). */
  protected runNow(): void {
    void this.schedule.runNow(async (id) => {
      this.store.refresh();
      await this.router.navigateByUrl(`/runs?run=${encodeURIComponent(id)}`);
    });
  }

  /** Opens a run; below 1024 px focus moves to the way back, the first thing in its place. */
  protected async openRun(href: string): Promise<void> {
    await this.router.navigateByUrl(href);
    this.afterRender(() =>
      this.host.nativeElement.querySelector<HTMLElement>('.runs__back a')?.focus(),
    );
  }

  /** Returns to the list, with focus on the run it came from. */
  protected async goBack(href: string): Promise<void> {
    const from = this.viewing();
    await this.router.navigateByUrl(href);
    this.afterRender(() =>
      this.host.nativeElement
        .querySelector<HTMLElement>(`a.run-item[href$="run=${CSS.escape(from ?? '')}"]`)
        ?.focus(),
    );
  }

  private afterRender(action: () => void): void {
    afterNextRender(action, { injector: this.injector });
  }

  constructor() {
    const subscription = this.route.queryParamMap.subscribe((params) => {
      this.viewing.set(params.get('run'));
      this.store.request(params.get('run'), params.get('problems') === 'true');
    });
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => subscription.unsubscribe());
    // The run in progress ended: its row and the header read the final outcome (L2-058 AC3).
    inject(CollectionWatcher).onEnd(() => this.store.refresh(), destroyRef);
  }
}
