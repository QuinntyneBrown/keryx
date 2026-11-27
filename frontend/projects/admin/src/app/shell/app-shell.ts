import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { App, Brand, Empty, PageBody, Sidebar, SkipLink } from '@kx/components';

/**
 * The admin shell: the frame every screen sits in. It orchestrates and composes; it draws
 * nothing itself. Every element on the screen is a design-system component from the components
 * library, built once there and reused (AGENTS.md).
 *
 * Until the screens exist it is the placeholder of ADR 0004: the frame, the lockup as the home
 * link and one empty-state card. Journey 2 projects the routed screens where the card is.
 */
@Component({
  selector: 'kx-app-shell',
  imports: [App, Brand, Empty, PageBody, Sidebar, SkipLink],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export default class AppShell {
  private readonly router = inject(Router);

  /** A library link was followed: the router goes there, and the document stays. */
  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
