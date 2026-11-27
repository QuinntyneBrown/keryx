import { booleanAttribute, Component, computed, input } from '@angular/core';

/**
 * The navigation frame (docs/design-system/components/sidebar.html): a column beside the
 * content from 1024 px and a top bar below, where its footer is hidden.
 *
 * It projects the lockup (`[slot=brand]`), the links (the default slot, into the one navigation
 * landmark, "Primary") and the person's avatar (`[slot=avatar]`). The footer appears once it is
 * told the next collection or the person, and every value arrives ready to read: this component
 * formats nothing.
 */
@Component({
  selector: 'kx-sidebar',
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
})
export class Sidebar {
  /** The tag beside the lockup: the surface this is. */
  readonly tag = input('Admin');
  /** False while there is nothing to navigate to: no landmark is announced with no links in it. */
  readonly navigation = input(true, { transform: booleanAttribute });
  /** The time of the next collection, as text ("12:00 PM"). */
  readonly nextCollection = input<string>();
  /** How long until then, as text ("in 2h 14m"). */
  readonly countdown = input<string>();
  /** The schedule in words ("Every 6 hours, Toronto time"). */
  readonly schedule = input<string>();
  /** A run is in progress: the footer says so in place of the next time. */
  readonly collecting = input(false, { transform: booleanAttribute });
  /** The signed-in person's name. */
  readonly userName = input<string>();
  /** Their role, in words ("Owner"). */
  readonly userRole = input<string>();

  protected readonly hasCollection = computed(() => this.collecting() || !!this.nextCollection());
  protected readonly hasFooter = computed(() => this.hasCollection() || !!this.userName());
}
