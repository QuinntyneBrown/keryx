import { booleanAttribute, Component, input } from '@angular/core';
import { Skeleton } from '../skeleton/skeleton';

let nextId = 0;

/**
 * A titled group of a long form (docs/design-system/components/settings-section.html): its
 * heading and explanation on the left from 768 px, and its fields, projected, on the right. The
 * section is a region named by its heading.
 */
@Component({
  selector: 'kx-settings-section',
  imports: [Skeleton],
  templateUrl: './settings-section.html',
  styleUrl: './settings-section.scss',
})
export class SettingsSection {
  readonly title = input('');
  /** Shaped like a section while the form loads: skeletons in place of its heading and fields. */
  readonly placeholder = input(false, { transform: booleanAttribute });
  /** What the group is for, in a sentence. */
  readonly hint = input<string>();

  protected readonly headingId = `kx-settings-section-${nextId++}`;
}
