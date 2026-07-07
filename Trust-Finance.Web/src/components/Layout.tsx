import { useCallback, useEffect, useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../features/auth/AuthContext";

const THEME_KEY = "tf.theme";
type Theme = "light" | "dark";

function initialTheme(): Theme {
  const stored = localStorage.getItem(THEME_KEY);
  if (stored === "light" || stored === "dark") return stored;
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function useTheme() {
  const [theme, setTheme] = useState<Theme>(initialTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem(THEME_KEY, theme);
  }, [theme]);

  const toggle = useCallback(
    () => setTheme((current) => (current === "dark" ? "light" : "dark")),
    [],
  );

  return { theme, toggle };
}

export function Layout() {
  const { user, signOut } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();

  function handleSignOut() {
    signOut();
    navigate("/login");
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-mark" aria-hidden="true" />
          Trust Finance
        </div>
        <nav className="sidebar-nav">
          <NavLink to="/" end>
            Dashboard
          </NavLink>
          <NavLink to="/transactions">Transactions</NavLink>
          <NavLink to="/categories">Categories</NavLink>
        </nav>
        <div className="sidebar-footer">
          <button
            type="button"
            className="btn-ghost"
            onClick={toggle}
            aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
          >
            {theme === "dark" ? "Light theme" : "Dark theme"}
          </button>
        </div>
      </aside>

      <div className="app-main">
        <header className="topbar">
          <span className="topbar-user">{user?.name}</span>
          <button type="button" className="btn-ghost" onClick={handleSignOut}>
            Sign out
          </button>
        </header>
        <main className="page">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
