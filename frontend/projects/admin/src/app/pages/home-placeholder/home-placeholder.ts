import { Component } from '@angular/core';
import { Empty } from '@kx/components';

/** The stand-in at Overview's address until the Overview screen exists (ADR 0006). */
@Component({
  selector: 'kx-home-placeholder',
  imports: [Empty],
  templateUrl: './home-placeholder.html',
  styleUrl: './home-placeholder.scss',
})
export default class HomePlaceholder {}
