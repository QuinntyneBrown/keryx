import { Component, input, output } from '@angular/core';

/** One option of a segmented control: a link, current when it is the view shown. */
export interface SegmentedOption {
  label: string;
  href: string;
  current: boolean;
}

/**
 * A small group of mutually exclusive views (docs/design-system/components/segmented.html), in
 * its link variant: each option is a link, the current one marked `aria-current`. A plain click
 * emits `navigate` with its `href` for the app's router.
 */
@Component({
  selector: 'kx-segmented',
  templateUrl: './segmented.html',
  styleUrl: './segmented.scss',
})
export class Segmented {
  /** The group's accessible name ("Filter runs"). */
  readonly label = input.required<string>();
  readonly options = input.required<readonly SegmentedOption[]>();
  readonly navigate = output<string>();

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
