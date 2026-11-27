import { DOCUMENT } from '@angular/common';
import { Component, inject, input } from '@angular/core';

/**
 * "Skip to content": the first focusable element of a page, parked above the viewport until it
 * has focus (docs/design-system/components/skip-link.html; WCAG 2.4.1 Bypass Blocks). Following
 * it moves focus to the element whose id is `target` without leaving the page, so the next Tab
 * goes to the first control in the content.
 */
@Component({
  selector: 'kx-skip-link',
  templateUrl: './skip-link.html',
  styleUrl: './skip-link.scss',
})
export class SkipLink {
  private readonly document = inject(DOCUMENT);

  /** The id of the element the link skips to: the `main` landmark. */
  readonly target = input('main');

  protected skip(event: Event): void {
    const target = this.document.getElementById(this.target());
    if (!target) return;
    // The router owns the address: a fragment there would be read as a route.
    event.preventDefault();
    if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
    target.focus();
  }
}
