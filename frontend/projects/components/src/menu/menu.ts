import {
  createFlexibleConnectedPositionStrategy,
  createOverlayRef,
  createRepositionScrollStrategy,
  type OverlayRef,
} from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import { Component, DestroyRef, inject, Injector, input, output, signal } from '@angular/core';
import { Icon, type IconName } from '../icon/icon';
import { IconBtn } from '../icon-btn/icon-btn';
import { MENU_PANEL, MenuPanel, type MenuPanelData } from './menu-panel';

/** One item of a menu: an action it emits by `id`, or a link that opens `href` in a new tab. */
export interface MenuItem {
  id: string;
  label: string;
  icon: IconName;
  /** A link out of Keryx, opened in a new tab; the consumer passes only http(s). */
  href?: string;
  /** Destructive, in the danger colour, after a divider. */
  danger?: boolean;
}

/**
 * A row's actions (docs/design-system/components/menu.html): an icon button "Actions for …" that
 * opens a menu in a CDK overlay, kept inside the viewport, following the WAI-ARIA menu button
 * pattern. The arrow keys, Home and End move focus and wrap (`menuKeyTarget`). Escape closes it
 * and returns focus to the button; focus leaving it, by Tab or a press elsewhere, closes it, so
 * one menu is open at a time. It uses the CDK overlay but not the CDK Menu, whose key manager
 * grows the initial bundle (ADR 0013). The button is the library's icon button, composed here.
 */
@Component({
  selector: 'kx-menu',
  imports: [Icon, IconBtn],
  templateUrl: './menu.html',
  styleUrl: './menu.scss',
})
export class Menu {
  /** The button's and the menu's accessible name ("Actions for Redeemed Run Club Toronto"). */
  readonly label = input.required<string>();
  readonly items = input.required<readonly MenuItem[]>();
  /** An action item was chosen; the menu has closed and focus is on its button. */
  readonly chosen = output<string>();

  protected readonly open = signal(false);
  private readonly injector = inject(Injector);
  /** The button that opened the menu, which focus returns to. */
  private trigger: IconBtn | null = null;
  private ref: OverlayRef | null = null;
  private focusable: HTMLElement[] = [];

  constructor() {
    inject(DestroyRef).onDestroy(() => this.close(false));
  }

  protected toggle(trigger: IconBtn): void {
    if (this.open()) this.close(true);
    else this.show(trigger, 'first');
  }

  /** Arrow Down or Up on the closed button opens the menu on its first or last item. */
  protected triggerKeydown(event: KeyboardEvent, trigger: IconBtn): void {
    if (this.open() || (event.key !== 'ArrowDown' && event.key !== 'ArrowUp')) return;
    event.preventDefault();
    this.show(trigger, event.key === 'ArrowDown' ? 'first' : 'last');
  }

  /** The arrow keys, Home and End move among the items, wrapping at the ends. */
  private menuKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close(true);
      return;
    }
    const count = this.focusable.length;
    const at = this.focusable.indexOf(document.activeElement as HTMLElement);
    const to = menuKeyTarget(event.key, at, count);
    if (to === null) return;
    event.preventDefault();
    this.focusable[to].focus();
  }

  private choose(id: string): void {
    this.close(true);
    this.chosen.emit(id);
  }

  private followed(): void {
    this.close(true);
  }

  /** Focus left the menu for somewhere other than its button: Tab moves on, and it closes. */
  private focusout(event: FocusEvent, menu: HTMLElement): void {
    const next = event.relatedTarget as Node | null;
    if (next && (menu.contains(next) || this.trigger?.element.contains(next))) return;
    this.close(false);
  }

  private show(trigger: IconBtn, focus: 'first' | 'last'): void {
    this.trigger = trigger;
    const origin = trigger.element;
    // The functional overlay API, as the CDK dialog uses: the Overlay service would pull every
    // strategy into the initial bundle.
    const ref = createOverlayRef(this.injector, {
      positionStrategy: createFlexibleConnectedPositionStrategy(this.injector, origin)
        .withPositions([
          { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 4 },
          { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -4 },
        ])
        .withPush(true)
        .withViewportMargin(8),
      scrollStrategy: createRepositionScrollStrategy(this.injector),
    });
    this.ref = ref;
    const data: MenuPanelData = {
      label: this.label(),
      items: this.items(),
      choose: (id) => this.choose(id),
      followed: () => this.followed(),
      keydown: (event) => this.menuKeydown(event),
      focusout: (event, menu) => this.focusout(event, menu),
    };
    const injector = Injector.create({
      providers: [{ provide: MENU_PANEL, useValue: data }],
      parent: this.injector,
    });
    // Rendered now, so its items are there to focus.
    ref.attach(new ComponentPortal(MenuPanel, null, injector)).changeDetectorRef.detectChanges();
    this.open.set(true);
    this.focusable = [...ref.overlayElement.querySelectorAll<HTMLElement>('.menu__item')];
    this.focusable[focus === 'first' ? 0 : this.focusable.length - 1]?.focus();
  }

  private close(returnFocus: boolean): void {
    const ref = this.ref;
    if (!ref) return;
    // Closed before the panel goes, since removing it blurs its item and calls here again; and
    // focus moves to the button first, so it is never left on the page's body.
    this.ref = null;
    this.focusable = [];
    this.open.set(false);
    if (returnFocus) this.trigger?.focus();
    ref.dispose();
  }
}

/**
 * Where a key moves focus among `count` menu items from `at` (-1 when none has it): the arrow
 * keys step and wrap, Home and End go to the ends; null for any other key.
 */
export function menuKeyTarget(key: string, at: number, count: number): number | null {
  if (count === 0) return null;
  switch (key) {
    case 'ArrowDown':
      return (at + 1) % count;
    case 'ArrowUp':
      return at <= 0 ? count - 1 : at - 1;
    case 'Home':
      return 0;
    case 'End':
      return count - 1;
    default:
      return null;
  }
}
