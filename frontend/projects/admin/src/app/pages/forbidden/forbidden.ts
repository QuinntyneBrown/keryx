import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SESSION_API } from '@kx/api';
import { Alert, App, Brand, Btn, Empty, Icon, PageBody, SkipLink } from '@kx/components';

@Component({
  selector: 'kx-forbidden',
  imports: [Alert, App, Brand, Btn, Empty, Icon, PageBody, SkipLink],
  templateUrl: './forbidden.html',
  styleUrl: './forbidden.scss',
})
export default class Forbidden {
  private readonly session = inject(SESSION_API);
  private readonly route = inject(ActivatedRoute);
  private readonly abort = new AbortController();
  protected readonly reference = this.route.snapshot.queryParamMap.get('ref') ?? undefined;
  protected readonly busy = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
  }

  protected async switchAccount() {
    if (this.busy()) return;
    this.busy.set(true);
    this.failed.set(false);
    try {
      await this.session.signOut(this.abort.signal);
      if (!this.abort.signal.aborted) {
        window.location.assign(
          this.session.signInUrl(this.route.snapshot.queryParamMap.get('return') ?? '/', true),
        );
      }
    } catch {
      if (!this.abort.signal.aborted) {
        this.failed.set(true);
        this.busy.set(false);
      }
    }
  }
}
