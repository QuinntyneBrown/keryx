import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ARTICLES_API, OVERVIEW_API, RUNS_API } from '@kx/api';
import {
  Btn,
  Empty,
  Icon,
  IssueCard,
  List,
  PageBody,
  PageHeader,
  Rail,
  RunChart,
  SectionHead,
  Stack,
  StatusCard,
  type ListEntry,
  type StatItem,
} from '@kx/components';
import {
  categoryName,
  formatClockTime,
  formatCollectedAt,
  formatCount,
  formatCountdown,
  formatDuration,
  formatRelativeAge,
  issueSeverityWord,
  issueTitle,
  issueWhen,
  organizationLabel,
  runBars,
  runHeadline,
  runTone,
  titleLabel,
} from '@kx/domain';
import { HttpArticlesApi } from '../../articles/http-articles-api';
import { HttpOverviewApi } from '../../overview/http-overview-api';
import { ScheduleStore } from '../../schedule/schedule-store';
import { OverviewStore, type Section, type SectionName } from '../../overview/overview-store';
import { HttpRunsApi } from '../../runs/http-runs-api';

/** A section's data once it is ready, otherwise null. */
function ready<T>(section: Section<T>): T | null {
  return section.state === 'ready' ? section.data : null;
}

/**
 * Overview (docs/mocks/pages/overview/): how the last run went and when the next one is
 * (L2-043). Times read in the zone the status names, Toronto's.
 */
@Component({
  selector: 'kx-overview',
  imports: [
    NgTemplateOutlet,
    Btn,
    Empty,
    Icon,
    IssueCard,
    List,
    PageBody,
    PageHeader,
    Rail,
    RunChart,
    SectionHead,
    Stack,
    StatusCard,
  ],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  providers: [
    { provide: OVERVIEW_API, useClass: HttpOverviewApi },
    { provide: RUNS_API, useClass: HttpRunsApi },
    { provide: ARTICLES_API, useClass: HttpArticlesApi },
    OverviewStore,
  ],
})
export default class Overview {
  protected readonly store = inject(OverviewStore);
  protected readonly schedule = inject(ScheduleStore);
  private readonly router = inject(Router);

  /**
   * The zone times read in: the one the status names, or Toronto's, Keryx's first region, while
   * the status has not loaded.
   */
  private readonly zone = computed(
    () => ready(this.store.status())?.time_zone ?? 'America/Toronto',
  );

  /** The shapes of the status card's stats, which its skeletons follow while it loads. */
  protected readonly loadingStats: readonly StatItem[] = [
    { label: 'New articles', value: '' },
    { label: 'Sources OK', value: '' },
    { label: 'Next run', value: '', size: 'sm', sub: '' },
  ];

  /**
   * The status card's words and stats: the run in progress when there is one (L2-043 AC3),
   * otherwise the last run.
   */
  protected readonly card = computed(() => {
    const status = ready(this.store.status());
    const current = status?.current_run;
    const run = current ?? status?.last_run;
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
    if (current) {
      return {
        eyebrow: 'Current run',
        headline: runHeadline(current),
        meta: `Started ${formatClockTime(current.started_at, status.time_zone)} · ${current.sources_checked} of ${current.sources_total} sources checked`,
        reference: null,
        tone: runTone(current.outcome),
        stats,
        progress: {
          label: 'Sources checked',
          value: current.sources_checked,
          max: current.sources_total,
        },
      };
    }
    return {
      eyebrow: 'Last run',
      headline: runHeadline(run),
      meta: `${formatCollectedAt(run.started_at, now, status.time_zone)} · took ${formatDuration(run.duration_ms ?? 0)}`,
      reference: run.id,
      tone: runTone(run.outcome),
      stats,
      progress: null,
    };
  });

  /** The run chart's bars, oldest first, in the zone the status names. */
  protected readonly bars = computed(() => {
    const runs = ready(this.store.chart());
    const zone = ready(this.store.status())?.time_zone;
    // Each bar opens its run on Runs (L2-044 AC5).
    return runs && zone
      ? runBars(runs, this.store.now(), zone).map((bar) => ({
          ...bar,
          href: `/runs?run=${encodeURIComponent(bar.id)}`,
        }))
      : null;
  });

  /** Latest articles: "View all 18,432" and a row per article, newest first (L2-045 AC1). */
  protected readonly latest = computed(() => {
    const latest = ready(this.store.latest());
    if (!latest) return null;
    const now = this.store.now();
    const regionName = (slug: string) =>
      latest.regions.find((region) => region.slug === slug)?.name ?? slug;
    const entries: ListEntry[] = latest.page.items.map((row) => ({
      id: row.id,
      title: titleLabel(row.title),
      meta: `${organizationLabel(row.organization)} · ${regionName(row.region)} · ${categoryName(row.category)}`,
      aside: formatRelativeAge(row.collected_at, now),
      ...(row.status === 'needs_review'
        ? { badge: { text: 'Review', tone: 'warning' as const } }
        : {}),
    }));
    const total = formatCount(latest.page.total);
    // After a failed run the list gives way to a notice that the API still serves the rest (L2-043 AC4).
    const failed = ready(this.store.status())?.last_run?.outcome === 'failed';
    return {
      viewAll: `View all ${total}`,
      entries,
      failedNotice: failed
        ? `The run stopped before anything was stored. The API is still serving the ${total} articles from earlier runs.`
        : null,
    };
  });

  /** Needs attention: the open count and a card per issue, most severe first (L2-045 AC2). */
  protected readonly attention = computed(() => {
    const issues = ready(this.store.issues());
    if (!issues) return null;
    const zone = this.zone();
    const now = this.store.now();
    return {
      note: issues.total > 0 ? `${formatCount(issues.total)} open` : null,
      // More open than the cards shown: the count leads to the runs with problems (L2-045 AC6).
      more: issues.total > issues.items.length,
      cards: issues.items.map((issue) => ({
        id: issue.id,
        severity: issue.severity,
        severityWord: issueSeverityWord(issue.severity),
        title: issueTitle(issue.kind),
        source: issue.subject_name,
        meta: issueWhen(issue.since, issue.consecutive_runs, now, zone),
        // The card opens the run the issue last occurred in (L2-045 AC3).
        href: `/runs?run=${encodeURIComponent(issue.run_id)}`,
      })),
    };
  });

  protected retry(): void {
    void this.store.retry();
  }

  protected retrySection(name: SectionName): void {
    void this.store.retrySection(name);
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  /** Starts a run; the status, read again, shows it in progress (L2-047 AC1). */
  protected runNow(): void {
    void this.schedule.runNow(() => this.store.load(['status', 'chart']));
  }

  constructor() {
    void this.store.load();
  }
}
