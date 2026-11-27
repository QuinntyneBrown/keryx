import { Component, computed, input, output } from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
import { Icon } from '../icon/icon';
import { Skeleton } from '../skeleton/skeleton';

/** One row of a list: a title, a meta line, an aside ("2h ago") and an optional badge. */
export interface ListEntry {
  id: string;
  title: string;
  meta: string;
  aside: string;
  badge?: { text: string; tone: BadgeTone };
}

/**
 * A run as a row of the run history (`.run-item`): its outcome as an icon of a tone and as a word
 * a screen reader hears, its start as the title, what it did as the meta line, and a flag when
 * sources failed. The whole row is a link; the current one is marked `aria-current`.
 */
export interface RunListItem {
  id: string;
  href: string;
  current: boolean;
  tone: 'success' | 'warning' | 'danger';
  word: string;
  title: string;
  meta: string;
  flag: string | null;
}

/**
 * A bordered list of rows (docs/design-system/components/list.html). Every text is bound as text:
 * a title from a source is never read as HTML. Long titles wrap inside their row; the meta line
 * is cut with an ellipsis.
 */
@Component({
  selector: 'kx-list',
  imports: [Badge, Icon, Skeleton],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class List {
  readonly entries = input<readonly ListEntry[]>([]);
  /** Runs as interactive rows, in place of entries. */
  readonly runs = input<readonly RunListItem[]>([]);
  /** A plain click on a run emits its `href` for the app's router. */
  readonly navigate = output<string>();
  /** Skeleton rows standing in while the entries load, in place of them. */
  readonly placeholders = input(0);

  protected readonly slots = computed(() =>
    Array.from({ length: this.placeholders() }, (_, index) => index),
  );

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
