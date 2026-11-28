import { Component, inject, InjectionToken } from '@angular/core';
import { Icon } from '../icon/icon';
import type { MenuItem } from './menu';

/** What an open menu's panel shows and whom it tells. */
export interface MenuPanelData {
  label: string;
  items: readonly MenuItem[];
  /** An action item was chosen. */
  choose(id: string): void;
  /** A link item was followed. */
  followed(): void;
  keydown(event: KeyboardEvent): void;
  focusout(event: FocusEvent, menu: HTMLElement): void;
}

export const MENU_PANEL = new InjectionToken<MenuPanelData>('MENU_PANEL');

/**
 * The open menu's list, in its CDK overlay (docs/design-system/components/menu.html). Only the
 * menu creates it, with its data bound where it is created.
 */
@Component({
  selector: 'kx-menu-panel',
  imports: [Icon],
  templateUrl: './menu-panel.html',
  styleUrl: './menu-panel.scss',
})
export class MenuPanel {
  protected readonly data = inject(MENU_PANEL);
}
