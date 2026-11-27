import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SourceKind } from '@kx/api';
import { Source } from '../public-api';

@Component({
  imports: [Source],
  template: `<kx-source [kind]="kind" kindLabel="RSS" [name]="name" address="redeemedrun.ca/feed"
    ><span class="extra">Failing</span></kx-source
  >`,
})
class Host {
  kind: SourceKind = 'rss';
  name = 'Redeemed Run Club Toronto';
}

function render(inputs: Partial<Host> = {}) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return fixture.nativeElement.querySelector('.source') as HTMLElement;
}

describe('Source', () => {
  it('shows the name, then the address after its hidden type, then what is projected', () => {
    const source = render();

    expect(source.querySelector('.table__title')?.textContent).toBe('Redeemed Run Club Toronto');
    expect(source.querySelector('.table__sub--mono')?.textContent).toBe('RSS: redeemedrun.ca/feed');
    expect(source.querySelector('.table__sub--mono .visually-hidden')?.textContent).toBe('RSS: ');
    expect(source.querySelector('.source__text .extra')?.textContent).toBe('Failing');
  });

  it.each([
    ['rss', 'M4 11a9 9 0 0 1 9 9'],
    ['website', 'M2 12h20'],
    ['news_query', 'm21 21-4.3-4.3'],
  ] as const)('draws a decorative %s icon in the tile', (kind, path) => {
    const icon = render({ kind }).querySelector('.source__icon svg') as SVGElement;

    expect(icon.getAttribute('aria-hidden')).toBe('true');
    expect(icon.innerHTML).toContain(path);
  });

  it('renders a name that holds markup as text', () => {
    const source = render({ name: '<img src=x>Grace' });

    expect(source.querySelector('.table__title')?.textContent).toBe('<img src=x>Grace');
    expect(source.querySelector('img')).toBeNull();
  });
});
