import { booleanAttribute, Component, computed, input } from '@angular/core';

/** What the state reports: nothing yet, a failure, or a success. It tints the icon disc. */
export type EmptyTone = 'neutral' | 'error' | 'success';

/**
 * A state that stands where content would be (docs/design-system/components/empty.html): an
 * icon disc, a title, a sentence or two and at most two actions. Nothing collected yet, no
 * results, a screen that failed to load, and the one card of a system page.
 *
 * It projects the icon (`[slot=icon]`) and the buttons (`[slot=actions]`). The title, the text
 * and the reference are rendered as text, whatever they hold.
 */
@Component({
  selector: 'kx-empty',
  templateUrl: './empty.html',
  styleUrl: './empty.scss',
})
export class Empty {
  /** A plain statement of what is missing or what happened. */
  readonly title = input.required<string>();
  /** Why, and what to do about it, in a sentence or two. */
  readonly text = input<string>();
  /** A request reference a person can send on; shown as "Reference <id>". */
  readonly reference = input<string>();
  /** The heading level: 2 under a page header, 1 on a system page, whose only heading it is. */
  readonly level = input<1 | 2 | 3>(2);
  /** On a surface of its own, with the card's edge. */
  readonly card = input(false, { transform: booleanAttribute });
  /** Less padding, inside a table or a card. */
  readonly compact = input(false, { transform: booleanAttribute });
  readonly tone = input<EmptyTone>('neutral');
  /** Announce it at once: it replaced content that failed to load. */
  readonly announce = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() => {
    const tone = this.tone();
    return [
      'empty',
      this.card() ? 'empty--card' : '',
      this.compact() ? 'empty--compact' : '',
      tone === 'neutral' ? '' : `empty--${tone}`,
    ]
      .filter(Boolean)
      .join(' ');
  });
}
