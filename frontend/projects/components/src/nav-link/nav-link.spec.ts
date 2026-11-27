import { TestBed } from '@angular/core/testing';
import { NavLink } from '../public-api';

function render(inputs: Record<string, unknown>) {
  const fixture = TestBed.createComponent(NavLink);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, link: (fixture.nativeElement as HTMLElement).querySelector('a')! };
}

describe('NavLink', () => {
  it('is a link marked current with aria-current="page" only when current', () => {
    expect(render({ href: '/runs', current: true }).link.getAttribute('aria-current')).toBe('page');
    expect(render({ href: '/runs' }).link.hasAttribute('aria-current')).toBe(false);
  });

  // L2-042 AC3: a badge only when something needs attention, named in words.
  it.each([
    [2, '2 failing sources', '2'],
    [0, '0 failing sources', null],
    [undefined, undefined, null],
  ])('given a count of %s, shows badge %s', (count, countLabel, shown) => {
    const { link } = render({ href: '/runs', count, countLabel });
    const badge = link.querySelector('.nav-link__badge');

    expect(badge?.textContent ?? null).toBe(shown);
    if (badge) expect(badge.getAttribute('aria-label')).toBe(countLabel);
  });

  it('emits navigate on a plain click and leaves a modified click to the browser', () => {
    const { fixture, link } = render({ href: '/settings' });
    const followed: string[] = [];
    fixture.componentInstance.navigate.subscribe((href) => followed.push(href));
    const plain = new MouseEvent('click', { bubbles: true, cancelable: true });
    const modified = new MouseEvent('click', { bubbles: true, cancelable: true, metaKey: true });

    link.dispatchEvent(plain);
    link.dispatchEvent(modified);

    expect(followed).toEqual(['/settings']);
    expect(plain.defaultPrevented).toBe(true);
    expect(modified.defaultPrevented).toBe(false);
  });
});
