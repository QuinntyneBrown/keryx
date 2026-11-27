import { Component, computed, input } from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
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
 * A bordered list of rows (docs/design-system/components/list.html). Every text is bound as text:
 * a title from a source is never read as HTML. Long titles wrap inside their row; the meta line
 * is cut with an ellipsis.
 */
@Component({
  selector: 'kx-list',
  imports: [Badge, Skeleton],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class List {
  readonly entries = input.required<readonly ListEntry[]>();
  /** Skeleton rows standing in while the entries load, in place of them. */
  readonly placeholders = input(0);

  protected readonly slots = computed(() =>
    Array.from({ length: this.placeholders() }, (_, index) => index),
  );
}
