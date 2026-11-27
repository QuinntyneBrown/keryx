import { booleanAttribute, Component, input, output } from '@angular/core';

/**
 * One choice of a set, drawn as a card (docs/design-system/components/choice-card.html): a
 * native radio covering a label, so the arrow keys move between the cards of a group, as for
 * any radio group. Its text is projected.
 */
@Component({
  selector: 'kx-choice-card',
  templateUrl: './choice-card.html',
  styleUrl: './choice-card.scss',
})
export class ChoiceCard {
  readonly name = input.required<string>();
  readonly value = input.required<string>();
  readonly checked = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  /** This card was chosen: its value. */
  readonly chosen = output<string>();
}
