import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SESSION_API } from '@kx/api';
import { Alert, App, Brand, Btn, Empty, Icon, PageBody, SkipLink } from '@kx/components';

@Component({
  selector: 'kx-sign-in',
  imports: [Alert, App, Brand, Btn, Empty, Icon, PageBody, SkipLink],
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.scss',
})
export default class SignIn {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(SESSION_API);
  protected readonly busy = signal(false);
  protected get state() {
    return this.route.snapshot.queryParamMap.get('state');
  }
  protected get reference() {
    return this.route.snapshot.queryParamMap.get('ref');
  }
  protected start() {
    if (this.busy()) return;
    this.busy.set(true);
    window.location.assign(
      this.api.signInUrl(this.route.snapshot.queryParamMap.get('return') ?? '/'),
    );
  }
}
