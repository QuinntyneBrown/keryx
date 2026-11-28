import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';
import { Btn, Dialog } from '@kx/components';

export const LEAVE_SETTINGS_TITLE_ID = 'leave-settings-title';
export const LEAVE_SETTINGS_DESCRIPTION_ID = 'leave-settings-description';
/** Where focus starts: "Stay", never the button that loses the changes. */
export const LEAVE_SETTINGS_AUTO_FOCUS = '.dialog__foot .btn';

/**
 * "Leave without saving?" (docs/mocks/dialogs/leave-settings/, L2-071 AC6), opened when staff
 * leave Settings for another screen with unsaved changes. It closes with `true` on Leave; Stay,
 * Close and Escape close it with nothing, and the navigation is cancelled.
 */
@Component({
  selector: 'kx-leave-settings-dialog',
  imports: [Btn, Dialog],
  templateUrl: './leave-settings-dialog.html',
  styleUrl: './leave-settings-dialog.scss',
})
export class LeaveSettingsDialog {
  private readonly ref = inject<DialogRef<boolean>>(DialogRef);

  protected readonly titleId = LEAVE_SETTINGS_TITLE_ID;
  protected readonly descriptionId = LEAVE_SETTINGS_DESCRIPTION_ID;

  protected leave(): void {
    this.ref.close(true);
  }

  protected stay(): void {
    this.ref.close();
  }
}
