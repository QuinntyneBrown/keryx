import { Injectable } from '@angular/core';
import type { OverviewApi, OverviewStatus } from '@kx/api';
import { readJson } from '../session/read-json';

/** Overview's reads over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpOverviewApi implements OverviewApi {
  status(signal: AbortSignal): Promise<OverviewStatus> {
    return readJson<OverviewStatus>('/admin/v1/overview/status', signal);
  }
}
