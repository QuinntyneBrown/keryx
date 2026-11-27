import { booleanAttribute, Component, input, output } from '@angular/core';

/**
 * One destination in the sidebar (docs/design-system/components/nav-link.html): an icon, a word
 * and, on Runs, a count of what needs attention. It is a real link. The current page is marked
 * with `aria-current="page"` and shown by fill, weight, a volt icon and, from 1024 px, a bar.
 *
 * A plain click emits `navigate` with the `href` so the app follows it with its router; any
 * other click is left to the browser. The icon is projected into `[slot=icon]`.
 */
@Component({
  selector: 'kx-nav-link',
  templateUrl: './nav-link.html',
  styleUrl: './nav-link.scss',
})
export class NavLink {
  readonly href = input.required<string>();
  /** The page this link leads to is the one shown. */
  readonly current = input(false, { transform: booleanAttribute });
  /** A count of what needs attention there; no badge when it is 0 or absent. */
  readonly count = input<number>();
  /** The badge's accessible name ("2 failing sources"). */
  readonly countLabel = input<string>();

  readonly navigate = output<string>();

  protected follow(event: MouseEvent): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(this.href());
  }
}
