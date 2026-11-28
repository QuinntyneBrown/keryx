import { Injectable } from '@angular/core';
import type { ApiUsageApi, ApiUsageDto } from '@kx/api';
import { readJson } from '../session/read-json';

/** The consumer API's usage, over fetch with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpApiUsageApi implements ApiUsageApi {
  get(signal: AbortSignal): Promise<ApiUsageDto> {
    return readJson<ApiUsageDto>('/admin/v1/api-usage', signal);
  }
}
