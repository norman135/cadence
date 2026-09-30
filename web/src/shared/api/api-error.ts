/** RFC 9457 problem details, as returned by the Cadence API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  /** Stable machine-readable error code, e.g. `auth.invalid_credentials`. */
  code?: string;
  /** Validation failures keyed by camelCase field name. */
  errors?: Record<string, string[]>;
}

/** A non-successful API response. Carries the problem details body when the server sent one. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${String(status)}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** The stable error code, when the server sent one. */
  get code(): string | undefined {
    return this.problem?.code;
  }

  /** True for 4xx responses: retrying the same request will not help. */
  get isClientError(): boolean {
    return this.status >= 400 && this.status < 500;
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    const contentType = response.headers.get('Content-Type') ?? '';
    let problem: ProblemDetails | undefined;

    if (contentType.includes('json')) {
      try {
        problem = (await response.json()) as ProblemDetails;
      } catch {
        problem = undefined;
      }
    }

    return new ApiError(response.status, problem);
  }
}

/** A user-facing message for any error thrown by an API call. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.status >= 500
      ? 'Something went wrong on our side. Please try again.'
      : error.message;
  }
  return 'Could not reach the server. Check your connection and try again.';
}
