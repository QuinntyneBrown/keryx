import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { Btn, Dialog, Field, Input } from '@kx/components';
import { KEYWORD_WORDS, keywordError, type KeywordList } from '@kx/domain';

/** The list the dialog adds to, and the values it holds now. */
export interface AddKeywordData {
  list: KeywordList;
  values: readonly string[];
}

export const ADD_KEYWORD_TITLE_ID = 'add-keyword-title';
export const ADD_KEYWORD_DESCRIPTION_ID = 'add-keyword-description';
/** Where focus starts: the one field. */
export const ADD_KEYWORD_AUTO_FOCUS = '#keyword';

/**
 * "Add a topic", "Add an exclusion" or "Add a focus area" (docs/mocks/dialogs/add-keyword/,
 * L2-072), opened by Settings in a CDK dialog. It closes with the trimmed value, which the screen
 * adds to its list; a value already in the list, or not 2 to 40 characters, is refused here.
 */
@Component({
  selector: 'kx-add-keyword-dialog',
  imports: [Btn, Dialog, Field, Input],
  templateUrl: './add-keyword-dialog.html',
  styleUrl: './add-keyword-dialog.scss',
})
export class AddKeywordDialog {
  private readonly ref = inject<DialogRef<string>>(DialogRef);
  private readonly data = inject<AddKeywordData>(DIALOG_DATA);

  protected readonly titleId = ADD_KEYWORD_TITLE_ID;
  protected readonly descriptionId = ADD_KEYWORD_DESCRIPTION_ID;
  protected readonly words = KEYWORD_WORDS[this.data.list];
  protected readonly value = signal('');
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    const error = keywordError(this.data.list, this.data.values, this.value());
    this.error.set(error);
    if (!error) this.ref.close(this.value().trim());
  }

  protected close(): void {
    this.ref.close();
  }
}
