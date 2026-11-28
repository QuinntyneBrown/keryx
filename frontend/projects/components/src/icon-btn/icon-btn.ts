import { booleanAttribute, Component, ElementRef, inject, input, output } from '@angular/core';

/**
 * A button that is only an icon (docs/design-system/components/icon-btn.html). Its `label` is its
 * accessible name, since the icon says nothing to assistive technology.
 */
@Component({ selector: 'kx-icon-btn', templateUrl: './icon-btn.html', styleUrl: './icon-btn.scss' })
export class IconBtn {
  readonly label = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  /** It opens a menu (`aria-haspopup`), as a menu's button does. */
  readonly haspopup = input<'menu' | null>(null);
  /** Whether what it opens is open (`aria-expanded`); null when it opens nothing. */
  readonly expanded = input<boolean | null>(null);
  readonly activated = output<void>();

  // The inner button is found through the host: no view query is needed for one element.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The button itself, which a menu is placed against. */
  get element(): HTMLButtonElement {
    return this.host.nativeElement.querySelector('button') as HTMLButtonElement;
  }

  focus(): void {
    this.element.focus();
  }
}
