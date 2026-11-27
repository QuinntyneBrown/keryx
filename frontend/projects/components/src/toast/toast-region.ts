import { Component } from '@angular/core';

/**
 * Where toasts appear (docs/design-system/components/toast.html, "Region"): bottom right from
 * 640 px, across the bottom below. It is a polite live region, present before any toast, so a
 * toast added to it is announced.
 */
@Component({
  selector: 'kx-toast-region',
  templateUrl: './toast-region.html',
  styleUrl: './toast-region.scss',
})
export class ToastRegion {}
