import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** The admin app's root: the routed screens render in its outlet. */
@Component({
  selector: 'kx-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
