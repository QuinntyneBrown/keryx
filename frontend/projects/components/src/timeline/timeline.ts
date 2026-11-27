import { Component, input } from '@angular/core';
import { Icon, type IconName } from '../icon/icon';

/** A step's state: its icon and colour, and for a skipped step a quieter name. */
export type TimelineState = 'ok' | 'warn' | 'failed' | 'running' | 'skipped';

/** One step of a timeline: its state as an icon and as words, its name, time and detail. */
export interface TimelineStep {
  id: string;
  state: TimelineState;
  /** The state as words, heard before the name ("Done with warnings"). */
  word: string;
  name: string;
  time: string;
  detail: string;
}

const ICONS: Record<TimelineState, IconName> = {
  ok: 'success',
  warn: 'warning',
  failed: 'danger',
  running: 'loader',
  skipped: 'circle',
};

/**
 * Steps in order down a rule (docs/design-system/components/timeline.html), as a run's pipeline.
 * Every text is bound as text.
 */
@Component({
  selector: 'kx-timeline',
  imports: [Icon],
  templateUrl: './timeline.html',
  styleUrl: './timeline.scss',
})
export class Timeline {
  readonly steps = input.required<readonly TimelineStep[]>();

  protected icon(state: TimelineState): IconName {
    return ICONS[state];
  }
}
