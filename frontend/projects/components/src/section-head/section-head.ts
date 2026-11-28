import { booleanAttribute, Component, input, output } from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * A section's title with a link to the whole of it or a short note
 * (docs/design-system/components/section-head.html): "Latest articles" with "View all 18,432",
 * or "Needs attention" with "3 open". The title's id labels the section around it.
 *
 * A plain click on the link emits `navigate` with its `href` for the app's router; any other
 * click is left to the browser.
 */
@Component({
  selector: 'kx-section-head',
  imports: [Icon],
  templateUrl: './section-head.html',
  styleUrl: './section-head.scss',
})
export class SectionHead {
  readonly heading = input.required<string>();
  readonly headingId = input<string | null>(null);
  /**
   * A caption over a chart rather than a section's heading: the words are a caption and the link
   * is the small one ("All runs").
   */
  readonly caption = input(false, { transform: booleanAttribute });
  readonly href = input<string | null>(null);
  readonly linkText = input<string | null>(null);
  /** A short note in place of a link ("3 open"). */
  readonly note = input<string | null>(null);

  readonly navigate = output<string>();

  protected follow(event: MouseEvent, href: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
