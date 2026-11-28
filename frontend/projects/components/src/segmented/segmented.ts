import { booleanAttribute, Component, input, output } from '@angular/core';

/** One option of a segmented control: a link, current when it is the view shown. */
export interface SegmentedOption {
  label: string;
  href: string;
  current: boolean;
}

/** One option of the button variant: a value chosen in a form, pressed when it is the one held. */
export interface SegmentedChoice {
  label: string;
  value: string;
}

/**
 * A small group of mutually exclusive options (docs/design-system/components/segmented.html).
 * In its link variant (`options`) each option is a view's link, the current one marked
 * `aria-current`, and a plain click emits `navigate` with its `href` for the app's router. In its
 * button variant (`choices`) each option is a button, the one equal to `value` marked
 * `aria-pressed`, and pressing one emits `choose` with its value.
 */
@Component({
  selector: 'kx-segmented',
  templateUrl: './segmented.html',
  styleUrl: './segmented.scss',
})
export class Segmented {
  /**
   * The group's accessible name ("Filter runs"). Without one it is no group of its own, as when
   * a fieldset's legend already names the choice.
   */
  readonly label = input<string>();
  readonly options = input<readonly SegmentedOption[]>([]);
  readonly navigate = output<string>();
  readonly choices = input<readonly SegmentedChoice[]>([]);
  /** The choice held: its button is pressed. */
  readonly value = input<string | null>(null);
  readonly choose = output<string>();
  /** The views cannot be switched yet, as while the first answer loads. */
  readonly disabled = input(false, { transform: booleanAttribute });

  protected follow(event: MouseEvent, href: string): void {
    // A disabled group's links do nothing, by pointer or by key.
    if (this.disabled()) {
      event.preventDefault();
      return;
    }
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    this.navigate.emit(href);
  }
}
