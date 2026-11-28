import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { CURRENT_STAFF, NOTIFIER } from '@kx/api';
import {
  App,
  Avatar,
  Brand,
  Icon,
  NavLink,
  Sidebar,
  SkipLink,
  Toast,
  ToastRegion,
} from '@kx/components';
import {
  collectionRecurrence,
  failingSourcesLabel,
  formatClockTime,
  formatCountdown,
} from '@kx/domain';
import { NotifierStore } from '../notifier/notifier-store';
import { CollectionWatcher } from '../schedule/collection-watcher';
import { ScheduleStore } from '../schedule/schedule-store';

/**
 * The admin shell: the frame every screen sits in. It orchestrates and composes; it draws
 * nothing itself. Every element on the screen is a design-system component from the components
 * library, built once there and reused (AGENTS.md).
 *
 * The routed screens render in its page body. Each screen's navigation link arrives with the
 * screen (ADR 0006).
 */
@Component({
  selector: 'kx-app-shell',
  imports: [App, Avatar, Brand, Icon, NavLink, RouterOutlet, Sidebar, SkipLink, Toast, ToastRegion],
  providers: [
    NotifierStore,
    { provide: NOTIFIER, useExisting: NotifierStore },
    CollectionWatcher,
    ScheduleStore,
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export default class AppShell {
  private readonly router = inject(Router);
  /** The toasts every screen inside the shell raises through `NOTIFIER`. */
  protected readonly notifier = inject(NotifierStore);
  /** The signed-in person, read by the staff guard before the shell matched. */
  protected readonly staff = inject(CURRENT_STAFF).profile;
  /** The path shown, without its query or fragment; it marks the current navigation link. */
  private readonly path = signal(this.pathOf(this.router.url));

  protected readonly schedule = inject(ScheduleStore);
  /** The next collection in the sidebar footer, in the schedule's zone (L2-042 AC1). */
  protected readonly nextCollection = computed(() => {
    const next = this.schedule.schedule();
    return next ? formatClockTime(next.next_run_at, next.time_zone) : undefined;
  });
  /** How long until then; it moves on with the store's clock, with no request (AC6). */
  protected readonly countdown = computed(() => {
    const next = this.schedule.schedule();
    return next ? formatCountdown(this.schedule.now(), next.next_run_at) : undefined;
  });
  protected readonly recurrence = computed(() => {
    const next = this.schedule.schedule();
    return next ? collectionRecurrence(next.interval_hours, next.time_zone) : undefined;
  });
  /** The sources whose last fetch failed, counted on the Runs link (L2-042 AC3). */
  protected readonly failingSources = computed(
    () => this.schedule.schedule()?.failing_sources ?? 0,
  );
  protected readonly failingSourcesLabel = computed(
    () => failingSourcesLabel(this.failingSources()) ?? undefined,
  );

  constructor() {
    // The schedule is read on any screen, so the shell learns when a scheduled run begins.
    this.schedule.load();
    const navigations = this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) this.path.set(this.pathOf(event.urlAfterRedirects));
    });
    inject(DestroyRef).onDestroy(() => navigations.unsubscribe());
  }

  /** Whether `href` is the screen shown, or a screen inside it. */
  protected isCurrent(href: string): boolean {
    const path = this.path();
    return path === href || path.startsWith(href + '/');
  }

  private pathOf(url: string): string {
    return (
      this.router
        .parseUrl(url)
        .root.children['primary']?.segments.map((s) => '/' + s.path)
        .join('') || '/'
    );
  }

  /** A library link was followed: the router goes there, and the document stays. */
  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
