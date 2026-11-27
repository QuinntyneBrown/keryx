import { Component } from '@angular/core';
import { Empty, PageBody } from '@kx/components';

/** The stand-in at Overview's address until the Overview screen exists (ADR 0006). */
@Component({
  selector: 'kx-home-placeholder',
  imports: [Empty, PageBody],
  templateUrl: './home-placeholder.html',
  styleUrl: './home-placeholder.scss',
})
export default class HomePlaceholder {}
