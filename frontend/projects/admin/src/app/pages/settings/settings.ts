import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CURRENT_STAFF, SESSION_API, SETTINGS_API, type IntervalHours } from '@kx/api';
import {
  Alert,
  Btn,
  Chip,
  Empty,
  Field,
  Input,
  InputAffix,
  PageBody,
  PageHeader,
  Range,
  SaveBar,
  Segmented,
  SettingsSection,
  Switch,
  type SegmentedChoice,
} from '@kx/components';
import { cronLine, thresholdHelp } from '@kx/domain';
import { HttpSettingsApi } from '../../settings/http-settings-api';
import { SettingsStore } from '../../settings/settings-store';

/**
 * Settings (docs/mocks/pages/settings/): the form of what Keryx collects, scores, places, keeps
 * and alerts on (L2-070), then the Account section of who is signed in, with "Sign out" (L2-041
 * AC3), which is not part of the settings.
 */
@Component({
  selector: 'kx-settings',
  imports: [
    Alert,
    Btn,
    Chip,
    Empty,
    Field,
    Input,
    InputAffix,
    PageBody,
    PageHeader,
    Range,
    SaveBar,
    Segmented,
    SettingsSection,
    Switch,
  ],
  providers: [{ provide: SETTINGS_API, useClass: HttpSettingsApi }, SettingsStore],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export default class Settings {
  private readonly session = inject(SESSION_API);
  private readonly staff = inject(CURRENT_STAFF);
  private readonly router = inject(Router);
  private readonly abort = new AbortController();
  protected readonly store = inject(SettingsStore);

  protected readonly intervals: readonly SegmentedChoice[] = [
    { label: '1 hour', value: '1' },
    { label: '3 hours', value: '3' },
    { label: '6 hours', value: '6' },
    { label: '12 hours', value: '12' },
    { label: 'Day', value: '24' },
  ];
  /** The five sections the skeleton shows while the form loads. */
  protected readonly placeholders = [1, 2, 3, 4, 5];
  protected readonly schedule = computed(() => {
    const draft = this.store.draft();
    return draft ? cronLine(draft.interval_hours, new Date(), 'America/Toronto') : '';
  });
  protected readonly thresholdHelp = computed(() =>
    thresholdHelp(this.store.draft()?.publish_threshold ?? 0),
  );

  protected readonly person = this.staff.profile;
  protected readonly busy = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
    void this.store.load();
  }

  protected chooseInterval(value: string): void {
    this.store.set('interval_hours', Number(value) as IntervalHours);
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  protected async signOut(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.failed.set(false);
    try {
      await this.session.signOut(this.abort.signal);
    } catch {
      if (this.abort.signal.aborted) return;
      this.failed.set(true);
      this.busy.set(false);
      return;
    }
    if (this.abort.signal.aborted) return;
    this.staff.clear();
    await this.router.navigate(['/sign-in'], { queryParams: { state: 'signed-out' } });
  }
}
