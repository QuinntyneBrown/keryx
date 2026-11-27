import { Injectable } from '@angular/core';
import {
  type ArticleCounts,
  type ArticleDetail,
  type ArticleListQuery,
  type ArticlePage,
  type ArticlesApi,
  type Region,
} from '@kx/api';
import { readJson } from '../session/read-json';

/** The admin articles over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpArticlesApi implements ArticlesApi {
  list(query: ArticleListQuery, signal: AbortSignal): Promise<ArticlePage> {
    const search = new URLSearchParams({
      page: String(query.page),
      page_size: String(query.pageSize),
    });
    if (query.status) search.set('status', query.status);
    if (query.q) search.set('q', query.q);
    if (query.region) search.set('region', query.region);
    if (query.category) search.set('category', query.category);
    return readJson<ArticlePage>(`/admin/v1/articles?${search}`, signal);
  }

  counts(signal: AbortSignal): Promise<ArticleCounts> {
    return readJson<ArticleCounts>('/admin/v1/articles/counts', signal);
  }

  get(id: string, signal: AbortSignal): Promise<ArticleDetail> {
    return readJson<ArticleDetail>(`/admin/v1/articles/${encodeURIComponent(id)}`, signal);
  }

  async regions(signal: AbortSignal): Promise<Region[]> {
    return (await readJson<{ items: Region[] }>('/admin/v1/regions', signal)).items;
  }
}
