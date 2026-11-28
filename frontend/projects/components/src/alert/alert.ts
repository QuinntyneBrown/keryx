import { Component, booleanAttribute, input, output } from '@angular/core';
import { Icon } from '../icon/icon';

/** A line of an error summary: what to fix, linked to the control it names by id. */
export interface AlertItem {
  id: string;
  text: string;
}

@Component({
  selector: 'kx-alert',
  imports: [Icon],
  templateUrl: './alert.html',
  styleUrl: './alert.scss',
})
export class Alert {
  readonly tone = input<'info' | 'success' | 'warning' | 'danger' | 'neutral'>('info');
  /** Its first line, in bold; a short alert is its text alone. */
  readonly title = input<string>();
  readonly announcement = input<'alert' | 'status'>();
  /**
   * An error summary's lines, each a link to its control. Following one emits `follow` with the
   * control's id, for the screen to move focus there.
   */
  readonly items = input<readonly AlertItem[]>([]);
  readonly follow = output<string>();
  /** It can take focus from script (`tabindex="-1"`), as a summary does when a save is refused. */
  readonly focusable = input(false, { transform: booleanAttribute });

  protected go(event: Event, id: string): void {
    event.preventDefault();
    this.follow.emit(id);
  }
}
