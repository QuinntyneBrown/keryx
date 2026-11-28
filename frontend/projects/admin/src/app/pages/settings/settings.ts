import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { CURRENT_STAFF, SESSION_API, SETTINGS_API, type IntervalHours } from '@kx/api';
import {
  Alert,
  Btn,
  Chip,
  Empty,
  Field,
  Input,
  InputAffix,
  PageBody,
  PageHeader,
  Range,
  SaveBar,
  Segmented,
  SettingsSection,
  Switch,
  type AlertItem,
  type SegmentedChoice,
} from '@kx/components';
import {
  cronLine,
  fixFieldsBar,
  savedLine,
  fixFieldsTitle,
  thresholdHelp,
  type KeywordList,
  type SettingsField,
} from '@kx/domain';
import { HttpSettingsApi } from '../../settings/http-settings-api';
import type { LeavesSafely } from '../../settings/unsaved-changes-guard';
import { SettingsStore } from '../../settings/settings-store';

/**
 * Settings (docs/mocks/pages/settings/): the form of what Keryx collects, scores, places, keeps
 * and alerts on (L2-070), then the Account section of who is signed in, with "Sign out" (L2-041
 * AC3), which is not part of the settings.
 */
@Component({
  selector: 'kx-settings',
  imports: [
    Alert,
    Btn,
    Chip,
    Empty,
    Field,
    Input,
    InputAffix,
    PageBody,
    PageHeader,
    Range,
    SaveBar,
    Segmented,
    SettingsSection,
    Switch,
  ],
  providers: [{ provide: SETTINGS_API, useClass: HttpSettingsApi }, SettingsStore],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export default class Settings implements LeavesSafely {
  private readonly session = inject(SESSION_API);
  private readonly staff = inject(CURRENT_STAFF);
  private readonly router = inject(Router);
  private readonly abort = new AbortController();
  protected readonly store = inject(SettingsStore);

  protected readonly intervals: readonly SegmentedChoice[] = [
    { label: '1 hour', value: '1' },
    { label: '3 hours', value: '3' },
    { label: '6 hours', value: '6' },
    { label: '12 hours', value: '12' },
    { label: 'Day', value: '24' },
  ];
  /** The five sections the skeleton shows while the form loads. */
  protected readonly placeholders = [1, 2, 3, 4, 5];
  protected readonly schedule = computed(() => {
    const draft = this.store.draft();
    return draft ? cronLine(draft.interval_hours, new Date(), 'America/Toronto') : '';
  });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  protected readonly summary = computed<AlertItem[]>(() =>
    this.store.errors().map((error) => ({ id: error.field, text: error.summary })),
  );
  protected readonly saving = computed(() => this.store.saveState().status === 'saving');
  protected readonly savedText = computed(() => {
    const save = this.store.saveState();
    const draft = this.store.draft();
    return save.status === 'saved' && draft
      ? savedLine(draft.interval_hours, save.at, 'America/Toronto')
      : null;
  });
  protected readonly failureReference = computed(() => {
    const save = this.store.saveState();
    return save.status === 'failed' ? save.reference : null;
  });
  protected readonly fixTitle = computed(() => fixFieldsTitle(this.store.errors().length));
  protected readonly fixBar = computed(() => fixFieldsBar(this.store.errors().length));
  protected readonly thresholdHelp = computed(() =>
    thresholdHelp(this.store.draft()?.publish_threshold ?? 0),
  );

  protected readonly person = this.staff.profile;
  protected readonly busy = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.abort.abort());
    void this.store.load();
  }

  /** A field's message, when the last save found it out of range. */
  protected errorOf(field: SettingsField): string | null {
    return this.store.errors().find((error) => error.field === field)?.message ?? null;
  }

  /** "Save changes", or Enter in a field: a refused save moves focus to the summary. */
  protected async save(): Promise<void> {
    if (await this.store.save()) return;
    afterNextRender(() => this.focus('.alert--danger'), { injector: this.injector });
  }

  /**
   * Opens the Add dialog of a chip list (L2-072 AC3); a value it closes with joins the list,
   * marked new, and focus returns to Add. The CDK dialog and the dialog's code load when it is
   * first opened, not with the screen (ADR 0008).
   */
  protected async addTo(list: KeywordList): Promise<void> {
    const opener = document.activeElement;
    const values = this.store.draft()?.[list] ?? [];
    const [{ Dialog }, dialog] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./add-keyword-dialog/add-keyword-dialog'),
    ]);
    const ref = this.injector.get(Dialog).open<string>(dialog.AddKeywordDialog, {
      injector: this.injector,
      data: { list, values },
      ariaModal: true,
      ariaLabelledBy: dialog.ADD_KEYWORD_TITLE_ID,
      ariaDescribedBy: dialog.ADD_KEYWORD_DESCRIPTION_ID,
      autoFocus: dialog.ADD_KEYWORD_AUTO_FOCUS,
      restoreFocus: opener instanceof HTMLElement ? opener : true,
      backdropClass: 'kx-backdrop',
    });
    ref.closed.subscribe((value) => {
      if (value) this.store.add(list, value);
    });
  }

  /**
   * With unsaved changes, asks before Settings is left for another screen (L2-071 AC6): Leave
   * resolves true, and Stay, Close or Escape false, which keeps every entry.
   */
  async canLeave(): Promise<boolean> {
    if (!this.store.dirty()) return true;
    const [{ Dialog }, dialog] = await Promise.all([
      import('@angular/cdk/dialog'),
      import('./leave-settings-dialog/leave-settings-dialog'),
    ]);
    const ref = this.injector.get(Dialog).open<boolean>(dialog.LeaveSettingsDialog, {
      injector: this.injector,
      ariaModal: true,
      ariaLabelledBy: dialog.LEAVE_SETTINGS_TITLE_ID,
      ariaDescribedBy: dialog.LEAVE_SETTINGS_DESCRIPTION_ID,
      autoFocus: dialog.LEAVE_SETTINGS_AUTO_FOCUS,
      backdropClass: 'kx-backdrop',
    });
    return new Promise((resolve) => ref.closed.subscribe((leave) => resolve(leave === true)));
  }

  /** Follows a summary line to its field. */
  protected focusField(id: string): void {
    this.focus(`#${id}`);
  }

  private focus(selector: string): void {
    this.host.nativeElement.parentElement?.querySelector<HTMLElement>(selector)?.focus();
  }

  protected chooseInterval(value: string): void {
    this.store.set('interval_hours', Number(value) as IntervalHours);
  }

  protected navigate(href: string): void {
    void this.router.navigateByUrl(href);
  }

  protected async signOut(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.failed.set(false);
    try {
      await this.session.signOut(this.abort.signal);
    } catch {
      if (this.abort.signal.aborted) return;
      this.failed.set(true);
      this.busy.set(false);
      return;
    }
    if (this.abort.signal.aborted) return;
    this.staff.clear();
    await this.router.navigate(['/sign-in'], { queryParams: { state: 'signed-out' } });
  }
}
