import axios from 'axios';

/**
 * API JSON errors from the F# backend: `{ code, message }` with string `code`
 * (e.g. "unauthorized", "invalid_credentials"). Use HTTP `response.status` for
 * numeric status; do not rely on a numeric `code` in the body.
 */
export type LogbookApiError = {
  code: string;
  message: string;
};

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function hasStringMessage(
  data: unknown,
): data is { message: string } {
  return (
    isObject(data) && typeof (data as { message: unknown }).message === 'string'
  );
}

export const isUnauthorized = (error: unknown) =>
  axios.isAxiosError(error) && error.response?.status === 401;

export const getApiErrorMessage = (error: unknown, fallback: string) => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (typeof data === 'string') return data;
    if (hasStringMessage(data)) return data.message;
    if (isObject(data) && typeof data.error === 'string') return data.error;
    return error.message || fallback;
  }

  if (error instanceof Error) {
    return error.message || fallback;
  }

  return fallback;
};
