// Runs synchronously in <head>, before the stylesheet paints, so a dark-theme user
// never sees a white flash. The choice is per browser and lives in localStorage.
(function () {
  var KEY = "tf.theme";

  function read() {
    try { return localStorage.getItem(KEY); } catch (_) { return null; }
  }

  function apply(theme) {
    document.documentElement.dataset.theme = theme;
    try { localStorage.setItem(KEY, theme); } catch (_) { /* private mode */ }
    return theme;
  }

  var stored = read();
  var initial = stored === "light" || stored === "dark"
    ? stored
    : (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  document.documentElement.dataset.theme = initial;

  window.tfTheme = {
    current: function () { return document.documentElement.dataset.theme || "light"; },
    toggle: function () { return apply(this.current() === "dark" ? "light" : "dark"); }
  };
})();

// Runtime toggles for the attributes App.razor stamps on <html> at render time.
// Kept here so a preference can be flipped without a round trip that re-renders the page.
window.tfUi = {
  setAttribute: function (name, value) {
    document.documentElement.setAttribute(name, value);
    return value;
  },
  scrollToTop: function () {
    window.scrollTo({ top: 0, behavior: "smooth" });
  },
  // Stamped by the layout on its first interactive render. Until then the page is the
  // prerendered HTML and a click lands on nothing; anything that drives the UI from
  // outside — the E2E suite — waits for this rather than guessing.
  markInteractive: function () {
    document.documentElement.dataset.interactive = "true";
  }
};
