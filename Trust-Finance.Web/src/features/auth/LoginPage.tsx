import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { login } from "../../api/account";
import { ErrorList } from "../../components/ErrorList";
import { useAuth } from "./AuthContext";

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const token = await login({ email, password });
      signIn(token);
      navigate("/");
    } catch (err) {
      setError(err);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={handleSubmit}>
        <div className="auth-brand">
          <span className="brand-mark" aria-hidden="true" />
          Trust Finance
        </div>
        <h1>Sign in</h1>

        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            required
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        <ErrorList error={error} />

        <button type="submit" className="btn-primary" disabled={submitting}>
          {submitting ? "Signing in…" : "Sign in"}
        </button>

        <p className="auth-switch">
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </form>
    </div>
  );
}
