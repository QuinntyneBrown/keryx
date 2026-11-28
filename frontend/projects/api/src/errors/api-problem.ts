/**
 * A refusal from the Keryx API: its status, the `code` a screen branches on and the `trace_id`
 * a person quotes as a reference. A request that got no answer has status 0. Screens show a
 * generic message, never the server's words.
 */
export class ApiProblem extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
    readonly reference?: string,
    readonly errors?: Readonly<Record<string, readonly string[]>>,
    /** The run in progress, when a start was refused with `run_in_progress`. */
    readonly runId?: string,
  ) {
    super(`Keryx API request failed (${status}${code ? ` ${code}` : ''})`);
  }
}
