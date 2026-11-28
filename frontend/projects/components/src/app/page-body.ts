import { NgTemplateOutlet } from '@angular/common';
import { booleanAttribute, Component, computed, input, output } from '@angular/core';

/**
 * The body of a page inside the frame (docs/design-system/components/app.html, "Page body"):
 * the layout margin at the sides and room below.
 *
 * - `form`: no wider than a form reads well, with room for the save bar below. It is the
 *   `form` element itself (`novalidate`: the screen validates), and submitting it, as Enter in a
 *   field does, emits `submitted` and reloads nothing.
 * - `centered`: one centred card, the arrangement of a system page.
 */
@Component({
  selector: 'kx-page-body',
  imports: [NgTemplateOutlet],
  templateUrl: './page-body.html',
  styleUrl: './page-body.scss',
})
export class PageBody {
  readonly form = input(false, { transform: booleanAttribute });
  readonly centered = input(false, { transform: booleanAttribute });
  /** Its content is loading: assistive technology waits for it (`aria-busy`). */
  readonly busy = input(false, { transform: booleanAttribute });
  /** The form's id, which a submit button outside it names. */
  readonly formId = input<string>();
  readonly submitted = output<void>();

  protected readonly classes = computed(() =>
    [
      'page-body',
      this.form() ? 'page-body--form' : '',
      this.centered() ? 'page-body--centered' : '',
    ]
      .filter(Boolean)
      .join(' '),
  );
}
