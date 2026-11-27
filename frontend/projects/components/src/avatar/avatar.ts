import { Component, input } from '@angular/core';

/**
 * The signed-in person's initials on a disc (docs/design-system/components/avatar.html). It is
 * decorative: the name always stands beside it and is its label, so it is hidden from assistive
 * technology. Keryx holds no photos of people, so it shows initials only.
 */
@Component({
  selector: 'kx-avatar',
  templateUrl: './avatar.html',
  styleUrl: './avatar.scss',
})
export class Avatar {
  /** Two initials, upper case ("DA"). */
  readonly initials = input.required<string>();
}
