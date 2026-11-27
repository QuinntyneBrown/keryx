import { TestBed } from '@angular/core/testing';
import { SkipLink } from '../public-api';

function render(target?: string) {
  const fixture = TestBed.createComponent(SkipLink);
  if (target) fixture.componentRef.setInput('target', target);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  document.body.prepend(element);
  return element.querySelector('a')!;
}

describe('SkipLink', () => {
  let main: HTMLElement;

  beforeEach(() => {
    main = document.createElement('main');
    main.id = 'main';
    main.tabIndex = -1;
    main.innerHTML = '<a href="/runs">Runs</a>';
    document.body.appendChild(main);
  });

  afterEach(() => (document.body.innerHTML = ''));

  it('says "Skip to content" and points at the main landmark', () => {
    const link = render();

    expect(link.getAttribute('class')).toBe('skip-link');
    expect(link.textContent?.trim()).toBe('Skip to content');
    expect(link.getAttribute('href')).toBe('#main');
  });

  it('moves focus to main without loading the page again', () => {
    const link = render();
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    link.dispatchEvent(click);

    expect(click.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(main);
  });

  it('gives a target that cannot take focus a tabindex first', () => {
    main.removeAttribute('tabindex');
    const link = render();

    link.click();

    expect(main.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(main);
  });

  it('skips to another target when given one', () => {
    const content = document.createElement('section');
    content.id = 'content';
    document.body.appendChild(content);
    const link = render('content');

    link.click();

    expect(link.getAttribute('href')).toBe('#content');
    expect(document.activeElement).toBe(content);
  });
});
