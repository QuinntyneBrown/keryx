import { Component, computed, input, output } from '@angular/core';
import { Badge, type BadgeTone } from '../badge/badge';
import { Btn } from '../btn/btn';
import { Endpoint } from '../endpoint/endpoint';
import { Icon } from '../icon/icon';
import { Skeleton } from '../skeleton/skeleton';
import { Spinner } from '../spinner/spinner';

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
  /** `info` is a run in progress, shown turning. */
  tone: 'success' | 'warning' | 'danger' | 'info';
  word: string;
  title: string;
  meta: string;
  flag: string | null;
}

/**
 * An endpoint of the consumer API as a row (`.endpoint`). The whole row is a button that chooses
 * it; the chosen one is marked `aria-current`.
 */
export interface EndpointListItem {
  id: string;
  method: string;
  path: string;
  description: string;
  current: boolean;
}

/**
 * A problem a run raised as a row (`.issue`): its severity as an icon of a tone and as a word, its
 * title and subject, its detail in mono and its streak. Every text is bound as text: a subject
 * and a detail came from a source.
 */
export interface IssueListItem {
  id: string;
  tone: 'danger' | 'warning';
  word: string;
  title: string;
  subject: string | null;
  detail: string;
  streak: string | null;
  /** It concerns a source, so it offers "Edit source" (L2-057 AC4); a stage's issue does not. */
  editable?: boolean;
  /**
   * An open issue offers "Mute"; a muted one shows a "Muted" badge and offers "Unmute" (L2-020).
   * One that is over offers neither.
   */
  muting?: 'open' | 'muted';
  /** What the issue is called in its buttons' names: "“Source timed out” for Covenant Cycling". */
  name?: string;
  /** Its mute or unmute is waiting for the API's answer. */
  busy?: boolean;
}

/**
 * A bordered list of rows (docs/design-system/components/list.html). Every text is bound as text:
 * a title from a source is never read as HTML. Long titles wrap inside their row; the meta line
 * is cut with an ellipsis.
 */
@Component({
  selector: 'kx-list',
  imports: [Badge, Btn, Endpoint, Icon, Skeleton, Spinner],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class List {
  readonly entries = input<readonly ListEntry[]>([]);
  /** Runs as interactive rows, in place of entries. */
  readonly runs = input<readonly RunListItem[]>([]);
  /** Endpoints as rows to choose, in place of entries. */
  readonly endpoints = input<readonly EndpointListItem[]>([]);
  /** An endpoint was chosen: its id. */
  readonly choose = output<string>();
  /** A run's issues, in place of entries. */
  readonly issues = input<readonly IssueListItem[]>([]);
  /** A plain click on a run emits its `href` for the app's router. */
  readonly navigate = output<string>();
  /** An issue's "Edit source" was pressed: its id. */
  readonly editSource = output<string>();
  /** An open issue's "Mute" was pressed: its id. */
  readonly mute = output<string>();
  /** A muted issue's "Unmute" was pressed: its id. */
  readonly unmute = output<string>();
  /** Skeleton rows standing in while the entries load, in place of them. */
  readonly placeholders = input(0);
  /** Skeleton run rows standing in while the history loads. */
  readonly runPlaceholders = input(0);

  protected readonly slots = computed(() =>
    Array.from({ length: this.placeholders() }, (_, index) => index),
  );
  protected readonly runSlots = computed(() =>
    Array.from({ length: this.runPlaceholders() }, (_, index) => index),
  );

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
