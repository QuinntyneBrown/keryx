import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Btn, Empty, PageBody } from '@kx/components';

/**
 * Not found (docs/mocks/pages/not-found/default.html): an address inside the admin that does not
 * exist, shown in the shell with no navigation item current (L2-040 AC4).
 */
@Component({
  selector: 'kx-not-found',
  imports: [Btn, Empty, PageBody],
  templateUrl: './not-found.html',
  styleUrl: './not-found.scss',
})
export default class NotFound {
  private readonly router = inject(Router);

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
