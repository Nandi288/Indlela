// Indlela — tiny progressive enhancement. Everything works without JavaScript.
(function () {
  // One-tap copy buttons on the manual handoff panel (UC-13 alt 1a).
  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-copy]');
    if (!btn || !navigator.clipboard) return;
    navigator.clipboard.writeText(btn.getAttribute('data-copy')).then(function () {
      var original = btn.textContent;
      btn.textContent = 'Copied';
      btn.setAttribute('aria-live', 'polite');
      setTimeout(function () { btn.textContent = original; }, 1500);
    });
  });
})();

// Light / dark mode toggle (the choice is remembered on this device).
(function () {
  var root = document.documentElement;
  function sync() {
    var dark = root.getAttribute('data-theme') === 'dark';
    document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
      b.setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
      b.setAttribute('aria-pressed', String(dark));
    });
  }
  document.addEventListener('click', function (e) {
    if (!e.target.closest('[data-theme-toggle]')) return;
    var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    try { localStorage.setItem('indlela-mode', next); } catch (err) { }
    sync();
  });
  sync();
})();
