import { TestBed } from '@angular/core/testing';
import { Avatar } from '../public-api';

describe('Avatar', () => {
  it('shows the initials on a disc hidden from assistive technology', () => {
    const fixture = TestBed.createComponent(Avatar);
    fixture.componentRef.setInput('initials', 'DA');
    fixture.detectChanges();

    const disc = (fixture.nativeElement as HTMLElement).querySelector('span')!;

    expect(disc.getAttribute('class')).toBe('avatar');
    expect(disc.textContent?.trim()).toBe('DA');
    expect(disc.getAttribute('aria-hidden')).toBe('true');
  });
});
