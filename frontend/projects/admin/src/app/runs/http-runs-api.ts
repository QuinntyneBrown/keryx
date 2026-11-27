import { Injectable } from '@angular/core';
import type { RunPage, RunQuery, RunsApi } from '@kx/api';
import { readJson } from '../session/read-json';

/** The run history reads over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpRunsApi implements RunsApi {
  list(query: RunQuery, signal: AbortSignal): Promise<RunPage> {
    const params = new URLSearchParams({
      worker: 'collection',
      page: String(query.page),
      page_size: String(query.pageSize),
    });
    return readJson<RunPage>(`/admin/v1/runs?${params}`, signal);
  }
}
