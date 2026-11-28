import { Injectable } from '@angular/core';
import type { SettingsApi, SettingsDto } from '@kx/api';
import { antiforgeryToken } from '../session/antiforgery';
import { readJson } from '../session/read-json';

/** Settings over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpSettingsApi implements SettingsApi {
  getSettings(signal: AbortSignal): Promise<SettingsDto> {
    return readJson<SettingsDto>('/admin/v1/settings', signal);
  }

  async saveSettings(settings: SettingsDto, signal: AbortSignal): Promise<SettingsDto> {
    const token = await antiforgeryToken(signal);
    return readJson<SettingsDto>('/admin/v1/settings', signal, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify(settings),
    });
  }
}
