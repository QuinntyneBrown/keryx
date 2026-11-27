import { NgTemplateOutlet } from '@angular/common';
import { booleanAttribute, Component, computed, input } from '@angular/core';

/**
 * The frame every screen sits in (docs/design-system/components/app.html): a slot for the
 * sidebar beside the one `main` landmark, which can take focus so the skip link and the router
 * can move there.
 *
 * - Default: `[slot=sidebar]` and the content.
 * - `split`: the content beside a `[slot=panel]` (a table and its detail panel).
 * - `bare`: no sidebar, for the screens a person sees before they are let in.
 */
@Component({
  selector: 'kx-app',
  imports: [NgTemplateOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  /** No sidebar, so no sidebar column. */
  readonly bare = input(false, { transform: booleanAttribute });
  /** The content and a panel side by side. */
  readonly split = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() => (this.bare() ? 'app app--bare' : 'app'));
  protected readonly mainClasses = computed(() =>
    this.split() ? 'app__main app__main--split' : 'app__main',
  );
}
