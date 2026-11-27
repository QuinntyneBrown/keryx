import { NgTemplateOutlet } from '@angular/common';
import { booleanAttribute, Component, computed, input, output } from '@angular/core';

/**
 * The button (docs/design-system/components/btn.html). With an `href` it is a link drawn as a
 * button, because it navigates: a plain click emits `navigate` with the `href` so the app follows
 * it with its router, and any other click is left to the browser. Without one it is a `button`
 * that emits `activated`.
 */
@Component({
  selector: 'kx-btn',
  imports: [NgTemplateOutlet],
  templateUrl: './btn.html',
  styleUrl: './btn.scss',
})
export class Btn {
  /** Where it leads. With one it is a link; `disabled`, `busy` and `label` then do not apply. */
  readonly href = input<string>();
  readonly variant = input<'default' | 'primary' | 'ghost' | 'danger' | 'danger-solid'>('default');
  readonly small = input(false, { transform: booleanAttribute });
  readonly block = input(false, { transform: booleanAttribute });
  readonly grow = input(false, { transform: booleanAttribute });
  readonly label = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly busy = input(false, { transform: booleanAttribute });
  /** Submits the form it is in: Enter in a field presses it too. */
  readonly submit = input(false, { transform: booleanAttribute });
  readonly activated = output<void>();
  /** A plain click on the link: the `href` to follow without loading the document again. */
  readonly navigate = output<string>();
  protected readonly classes = computed(() =>
    [
      'btn',
      this.variant() === 'default' ? '' : 'btn--' + this.variant(),
      this.small() && 'btn--sm',
      this.block() && 'btn--block',
      this.grow() && 'btn--grow',
      this.label() && 'btn--icon',
    ]
      .filter(Boolean)
      .join(' '),
  );

  protected follow(event: MouseEvent, href: string): void {
    // A new tab, a new window or a download is the browser's to do.
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
