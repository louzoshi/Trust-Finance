import { ApiError } from "../lib/http";

export function errorMessages(error: unknown): string[] {
  if (error instanceof ApiError) return error.errors;
  if (error instanceof Error) return [error.message];
  return ["Something went wrong. Please try again."];
}

export function ErrorList({ error }: { error: unknown }) {
  if (!error) return null;
  return (
    <ul className="error-list" role="alert">
      {errorMessages(error).map((message) => (
        <li key={message}>{message}</li>
      ))}
    </ul>
  );
}
