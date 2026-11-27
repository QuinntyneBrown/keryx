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
import { OverviewStore, type Section } from '../../overview/overview-store';
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
  private readonly router = inject(Router);

  /** The status card's words and stats for the last run. */
  protected readonly card = computed(() => {
    const status = ready(this.store.status());
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

  /** The run chart's bars, oldest first, in the zone the status names. */
  protected readonly bars = computed(() => {
    const runs = ready(this.store.chart());
    const zone = ready(this.store.status())?.time_zone;
    return runs && zone ? runBars(runs, this.store.now(), zone) : null;
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
    const zone = ready(this.store.status())?.time_zone;
    if (!issues || !zone) return null;
    const now = this.store.now();
    return {
      note: issues.total > 0 ? `${formatCount(issues.total)} open` : null,
      cards: issues.items.map((issue) => ({
        id: issue.id,
        severity: issue.severity,
        severityWord: issueSeverityWord(issue.severity),
        title: issueTitle(issue.kind),
        source: issue.subject_name,
        meta: issueWhen(issue.since, issue.consecutive_runs, now, zone),
      })),
    };
  });

  protected retry(): void {
    void this.store.retry();
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  constructor() {
    void this.store.load();
  }
}
