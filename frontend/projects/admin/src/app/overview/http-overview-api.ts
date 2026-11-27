import { Injectable } from '@angular/core';
import type { OverviewApi, OverviewStatus, ShellSchedule } from '@kx/api';
import { readJson } from '../session/read-json';

/** Overview's reads over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpOverviewApi implements OverviewApi {
  status(signal: AbortSignal): Promise<OverviewStatus> {
    return readJson<OverviewStatus>('/admin/v1/overview/status', signal);
  }

  schedule(signal: AbortSignal): Promise<ShellSchedule> {
    return readJson<ShellSchedule>('/admin/v1/overview/schedule', signal);
  }
}
