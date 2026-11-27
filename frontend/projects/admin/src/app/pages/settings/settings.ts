import { Component, DestroyRef, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CURRENT_STAFF, SESSION_API } from '@kx/api';
import { Alert, Btn, Field, PageBody, PageHeader, SettingsSection } from '@kx/components';

/**
 * Settings (docs/mocks/pages/settings/). Journey 2 builds its Account section: who is signed in,
 * and "Sign out" (L2-041 AC3). The settings form arrives with journey 18.
 */
@Component({
  selector: 'kx-settings',
  imports: [Alert, Btn, Field, PageBody, PageHeader, SettingsSection],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export default class Settings {
  private readonly session = inject(SESSION_API);
  private readonly staff = inject(CURRENT_STAFF);
  private readonly router = inject(Router);
  private readonly abort = new AbortController();

  protected readonly person = this.staff.profile;
  protected readonly busy = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
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
