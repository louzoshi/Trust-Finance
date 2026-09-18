import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { login, register } from "../../api/account";
import { ErrorList } from "../../components/ErrorList";
import { useAuth } from "./AuthContext";

export function RegisterPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await register({ name, email, password });
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
        <h1>Create account</h1>

        <label>
          Name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            autoComplete="name"
            minLength={3}
            maxLength={40}
            required
          />
        </label>

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
            autoComplete="new-password"
            minLength={6}
            maxLength={20}
            required
          />
        </label>

        <ErrorList error={error} />

        <button type="submit" className="btn-primary" disabled={submitting}>
          {submitting ? "Creating account…" : "Create account"}
        </button>

        <p className="auth-switch">
          Already registered? <Link to="/login">Sign in</Link>
        </p>
      </form>
    </div>
  );
}
