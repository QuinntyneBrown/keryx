import type { SourceHealth, SourceKind } from './sources-api';

/** What `POST /admin/v1/sources` takes. */
export interface NewSource {
  kind: SourceKind;
  address: string;
  organization_name: string;
  region: string;
}

/** A source as `POST` and `GET /admin/v1/sources/{id}` answer it. */
export interface SourceDetail {
  id: string;
  kind: SourceKind;
  address: string;
  organization_name: string;
  region: string;
  health: SourceHealth;
  created_at: string;
  last_fetched_at: string | null;
  last_succeeded_at: string | null;
}
