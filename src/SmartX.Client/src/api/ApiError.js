export class ApiError extends Error {
  constructor(
    message,
    {
      status = 0,
      title = "Request failed",
      detail = null,
      errors = null,
      traceId = null,
      instance = null,
      cause = undefined,
    } = {},
  ) {
    super(message, { cause });

    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors;
    this.traceId = traceId;
    this.instance = instance;
  }

  get isNetworkError() {
    return this.status === 0;
  }

  get validationMessages() {
    if (!this.errors || typeof this.errors !== "object") {
      return [];
    }

    return Object.values(this.errors)
      .flatMap((messages) => (Array.isArray(messages) ? messages : [messages]))
      .filter(Boolean);
  }
}
