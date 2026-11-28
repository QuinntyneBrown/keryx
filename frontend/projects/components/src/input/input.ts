import {
  booleanAttribute,
  Component,
  ElementRef,
  inject,
  input,
  model,
  output,
} from '@angular/core';

/**
 * A text input (docs/design-system/components/input.html). Its state for assistive technology is
 * an attribute: `aria-invalid` when `invalid`, described by the field's error. `value` is two-way.
 */
@Component({ selector: 'kx-input', templateUrl: './input.html', styleUrl: './input.scss' })
export class Input {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The control's id, which a field's label names. */
  readonly controlId = input.required<string>();
  readonly value = model('');
  readonly type = input<'text' | 'url' | 'email' | 'search'>('text');
  readonly inputmode = input<string>();
  readonly autocomplete = input('off');
  /** Code and addresses: the mono face. */
  readonly mono = input(false, { transform: booleanAttribute });
  readonly invalid = input(false, { transform: booleanAttribute });
  /** The id of the text that describes it: its error. */
  readonly describedBy = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  /** Shown while it is empty; it says what may be entered and is never its label. */
  readonly placeholder = input<string>();
  /** Room at the start for an icon drawn over it, as in a search field. */
  readonly iconStart = input(false, { transform: booleanAttribute });
  /** A width for a short entry: `short` for a number with its unit, `medium` for an address. */
  readonly width = input<'short' | 'medium' | null>(null);
  /** Focus left the input. */
  readonly left = output<void>();

  focus(): void {
    this.host.nativeElement.querySelector('input')?.focus();
  }

  protected update(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
