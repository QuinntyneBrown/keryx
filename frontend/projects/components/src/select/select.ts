import { booleanAttribute, Component, ElementRef, inject, input, model } from '@angular/core';

/** An option of a `kx-select`. */
export interface SelectOption {
  value: string;
  label: string;
}

/**
 * A native select (docs/design-system/components/select.html), with a placeholder option of no
 * value first. `value` is two-way; `aria-invalid` marks an invalid choice.
 */
@Component({ selector: 'kx-select', templateUrl: './select.html', styleUrl: './select.scss' })
export class Select {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The control's id, which a field's label names. */
  readonly controlId = input.required<string>();
  readonly value = model('');
  readonly options = input.required<readonly SelectOption[]>();
  /** The first option, of no value: "Choose a region". */
  readonly placeholder = input<string>();
  readonly invalid = input(false, { transform: booleanAttribute });
  readonly describedBy = input<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  /** As wide as its longest option, as a filter in a toolbar; otherwise it fills its field. */
  readonly auto = input(false, { transform: booleanAttribute });

  focus(): void {
    this.host.nativeElement.querySelector('select')?.focus();
  }

  protected update(event: Event): void {
    this.value.set((event.target as HTMLSelectElement).value);
  }
}
