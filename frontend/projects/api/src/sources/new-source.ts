import type { SourceHealth, SourceKind, SourceTestResult } from './sources-api';

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

/** What "Edit source" sends: the kind is not editable (L2-003). */
export interface SourceChanges {
  address: string;
  organization_name: string;
  region: string;
}

/** `PUT /admin/v1/sources/{id}`'s answer: the saved source and one test fetch of its address. */
export interface SourceSaved {
  source: SourceDetail;
  test: SourceTestResult;
}
