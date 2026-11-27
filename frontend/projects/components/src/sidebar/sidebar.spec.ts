import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Sidebar } from '../public-api';

@Component({
  imports: [Sidebar],
  template: `
    <kx-sidebar
      [navigation]="navigation"
      [nextCollection]="nextCollection"
      [countdown]="countdown"
      [schedule]="schedule"
      [collecting]="collecting"
      [userName]="userName"
      [userRole]="userRole"
    >
      <a slot="brand" class="e2e-brand" href="/">keryx</a>
      @for (link of links; track link) {
        <a class="e2e-link" href="/">{{ link }}</a>
      }
      <span slot="avatar" class="e2e-avatar">DA</span>
    </kx-sidebar>
  `,
})
class Host {
  links: string[] = ['Overview', 'Articles'];
  navigation = true;
  nextCollection?: string;
  countdown?: string;
  schedule?: string;
  collecting = false;
  userName?: string;
  userRole?: string;
}

function render(inputs: Partial<Host> = {}) {
  const fixture = TestBed.createComponent(Host);
  Object.assign(fixture.componentInstance, inputs);
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('aside.sidebar')!;
}

const text = (element: Element | null) => element?.textContent?.replace(/\s+/g, ' ').trim();

describe('Sidebar', () => {
  it('heads the frame with the lockup and the tag that names this surface', () => {
    const head = render().querySelector('.sidebar__head')!;

    expect(head.querySelector('.e2e-brand')).not.toBeNull();
    expect(text(head.querySelector('.sidebar__tag'))).toBe('Admin');
  });

  it('holds the links in one navigation landmark named "Primary"', () => {
    const nav = render().querySelector('nav.sidebar__nav')!;

    expect(nav.getAttribute('aria-label')).toBe('Primary');
    expect([...nav.querySelectorAll('.e2e-link')].map(text)).toEqual(['Overview', 'Articles']);
  });

  it('announces no navigation landmark while there is nothing to navigate to', () => {
    const sidebar = render({ navigation: false, links: [] });

    expect(sidebar.querySelector('nav')).toBeNull();
    expect(sidebar.querySelector('.sidebar__head .e2e-brand')).not.toBeNull();
  });

  it('has no footer until it is told the next collection or the person', () => {
    expect(render().querySelector('.sidebar__foot')).toBeNull();
  });

  it('states the next collection, its countdown and the schedule', () => {
    const foot = render({
      nextCollection: '12:00 PM',
      countdown: 'in 2h 14m',
      schedule: 'Every 6 hours, Toronto time',
    }).querySelector('.sidebar__foot')!;

    expect([...foot.querySelectorAll('.sidebar__caption')].map(text)).toEqual([
      'Next collection',
      'Every 6 hours, Toronto time',
    ]);
    expect(text(foot.querySelector('.sidebar__value'))).toBe('12:00 PM · in 2h 14m');
    expect(foot.querySelector('.sidebar__user')).toBeNull();
  });

  it('says "Collecting now…" in place of the next time while a run is in progress', () => {
    const foot = render({
      nextCollection: '12:00 PM',
      countdown: 'in 2h 14m',
      collecting: true,
    }).querySelector('.sidebar__foot')!;

    expect(text(foot.querySelector('.sidebar__value'))).toBe('Collecting now…');
  });

  it('names the signed-in person and their role beside the avatar', () => {
    const user = render({ userName: 'Daniel Adeyemi', userRole: 'Owner' }).querySelector(
      '.sidebar__user',
    )!;

    expect(user.querySelector('.e2e-avatar')).not.toBeNull();
    expect(text(user.querySelector('.sidebar__user-name'))).toBe('Daniel Adeyemi');
    expect(text(user.querySelector('.sidebar__caption'))).toBe('Owner');
  });
});
