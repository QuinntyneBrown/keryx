import { Injectable } from '@angular/core';
import type { OpenIssuePage, RunDetail, RunPage, RunQuery, RunsApi } from '@kx/api';
import { readJson } from '../session/read-json';

/** The run history reads over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpRunsApi implements RunsApi {
  list(query: RunQuery, signal: AbortSignal): Promise<RunPage> {
    const params = new URLSearchParams({
      worker: 'collection',
      ...(query.problems ? { problems: 'true' } : {}),
      page: String(query.page),
      page_size: String(query.pageSize),
    });
    return readJson<RunPage>(`/admin/v1/runs?${params}`, signal);
  }

  get(id: string, signal: AbortSignal): Promise<RunDetail> {
    return readJson<RunDetail>(`/admin/v1/runs/${encodeURIComponent(id)}`, signal);
  }

  openIssues(pageSize: number, signal: AbortSignal): Promise<OpenIssuePage> {
    const params = new URLSearchParams({ state: 'open', page_size: String(pageSize) });
    return readJson<OpenIssuePage>(`/admin/v1/issues?${params}`, signal);
  }
}
