import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CURRENT_STAFF } from '@kx/api';
import { App, Avatar, Brand, PageBody, Sidebar, SkipLink } from '@kx/components';

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
  imports: [App, Avatar, Brand, PageBody, RouterOutlet, Sidebar, SkipLink],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export default class AppShell {
  private readonly router = inject(Router);
  /** The signed-in person, read by the staff guard before the shell matched. */
  protected readonly staff = inject(CURRENT_STAFF).profile;

  /** A library link was followed: the router goes there, and the document stays. */
  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }
}
