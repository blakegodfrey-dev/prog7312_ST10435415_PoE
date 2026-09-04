import { ApiError } from "./ApiError";

const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL ?? ""
).replace(/\/+$/, "");

function buildApiUrl(endpoint) {
  if (!API_BASE_URL) {
    throw new ApiError(
      "The Smart-X API address has not been configured.",
      {
        title: "Client configuration error",
        detail: "Set VITE_API_BASE_URL before starting the React client.",
      },
    );
  }

  const normalizedEndpoint = endpoint.startsWith("/")
    ? endpoint
    : `/${endpoint}`;

  return `${API_BASE_URL}${normalizedEndpoint}`;
}

function isFormData(body) {
  return typeof FormData !== "undefined" && body instanceof FormData;
}

async function readResponse(response, responseType) {
  if (response.status === 204) {
    return null;
  }

  if (responseType === "blob") {
    return response.blob();
  }

  if (responseType === "text") {
    return response.text();
  }

  const contentType = response.headers.get("content-type") ?? "";

  if (
    contentType.includes("application/json") ||
    contentType.includes("application/problem+json")
  ) {
    return response.json();
  }

  const text = await response.text();
  return text || null;
}

function createApiError(response, payload) {
  const problem =
    payload && typeof payload === "object" && !Array.isArray(payload)
      ? payload
      : {};

  const validationMessage = Object.values(problem.errors ?? {})
    .flat()
    .find(Boolean);

  const fallbackMessage =
    typeof payload === "string" && payload
      ? payload
      : `API request failed with status ${response.status}.`;

  const message =
    problem.detail ??
    validationMessage ??
    problem.title ??
    fallbackMessage;

  return new ApiError(message, {
    status: response.status,
    title: problem.title ?? "API request failed",
    detail: problem.detail ?? null,
    errors: problem.errors ?? null,
    traceId: problem.traceId ?? null,
    instance: problem.instance ?? null,
  });
}

export async function apiRequest(
  endpoint,
  {
    method = "GET",
    body = undefined,
    headers: suppliedHeaders = {},
    responseType = "auto",
    signal = undefined,
    ...fetchOptions
  } = {},
) {
  const headers = new Headers(suppliedHeaders);
  headers.set("Accept", "application/json");

  let requestBody = body;

  if (body !== undefined && body !== null && !isFormData(body)) {
    if (typeof body !== "string") {
      requestBody = JSON.stringify(body);
    }

    if (!headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json");
    }
  }

  let response;

  try {
    response = await fetch(buildApiUrl(endpoint), {
      ...fetchOptions,
      method,
      headers,
      body: requestBody,
      signal,
    });
  } catch (error) {
    if (error?.name === "AbortError") {
      throw error;
    }

    if (error instanceof ApiError) {
      throw error;
    }

    throw new ApiError(
      "Unable to connect to the Smart-X API.",
      {
        title: "Network error",
        detail:
          "Check that the API is running and that the configured address is correct.",
        cause: error,
      },
    );
  }

  const payload = await readResponse(response, responseType);

  if (!response.ok) {
    throw createApiError(response, payload);
  }

  return payload;
}

export const apiClient = {
  get(endpoint, options = {}) {
    return apiRequest(endpoint, {
      ...options,
      method: "GET",
    });
  },

  post(endpoint, body, options = {}) {
    return apiRequest(endpoint, {
      ...options,
      method: "POST",
      body,
    });
  },

  put(endpoint, body, options = {}) {
    return apiRequest(endpoint, {
      ...options,
      method: "PUT",
      body,
    });
  },

  delete(endpoint, options = {}) {
    return apiRequest(endpoint, {
      ...options,
      method: "DELETE",
    });
  },
};
