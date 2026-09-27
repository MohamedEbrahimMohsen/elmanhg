// mirrors Core.Exceptions.ExceptionErrorCodes.UnhandledException
export const unhandledErrorCode = 'UNHANDLED_EXCEPTION';

export const networkErrorCode = 'NETWORK_ERROR';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly codes: readonly string[],
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get code(): string {
    return this.codes[0] ?? unhandledErrorCode;
  }
}
