/* Kardia — the app's small bits of behaviour: journal tags, the Apple Watch key, copy buttons and confirmations.
   Pages are rendered on the server; this only handles what changes without a reload. */
(function () {
  "use strict";
  var token = (document.querySelector('meta[name="request-verification-token"]') || {}).content || "";

  function post(url, data) {
    var body = new URLSearchParams();
    Object.keys(data || {}).forEach(function (key) { body.append(key, data[key]); });
    return fetch(url, {
      method: "POST",
      headers: { "RequestVerificationToken": token, "Accept": "application/json" },
      body: body,
      credentials: "same-origin"
    }).then(function (response) {
      if (response.status === 401) { location.href = "/sign-in?returnUrl=" + encodeURIComponent(location.pathname + location.search); throw new Error("signed out"); }
      return response.json();
    });
  }

  function errorText(result) {
    return (result && result.validationErrors && result.validationErrors.length ? result.validationErrors.map(function (e) { return e.message; }).join(" ") : "Something went wrong. Try again.");
  }

  /* Journal tags: one tap marks or unmarks, the server answers with the day's journal. */
  document.querySelectorAll("[data-journal]").forEach(function (box) {
    var date = box.getAttribute("data-date");
    box.addEventListener("click", function (event) {
      var button = event.target.closest("button[data-tag]");
      if (!button || button.getAttribute("aria-busy") === "true") return;
      var on = button.getAttribute("aria-pressed") !== "true";
      button.setAttribute("aria-pressed", String(on));
      button.setAttribute("aria-busy", "true");
      post("/journal/set", { date: date, tag: button.getAttribute("data-tag"), on: on })
        .then(function (result) {
          if (!result.isValid) { button.setAttribute("aria-pressed", String(!on)); alert(errorText(result)); return; }
          var status = document.querySelector("[data-journal-status]");
          if (status) status.textContent = on ? "Saved: " + button.textContent.trim() : "Removed: " + button.textContent.trim();
        })
        .catch(function () { button.setAttribute("aria-pressed", String(!on)); })
        .finally(function () { button.removeAttribute("aria-busy"); });
    });
  });

  /* Copy buttons: data-copy="#id" or data-copy-text="…". */
  document.addEventListener("click", function (event) {
    var button = event.target.closest("[data-copy], [data-copy-text]");
    if (!button) return;
    var text = button.getAttribute("data-copy-text");
    if (text === null) { var source = document.querySelector(button.getAttribute("data-copy")); text = source ? (source.value || source.textContent).trim() : ""; }
    if (!text) return;
    var done = function () {
      var label = button.querySelector("[data-label]");
      var old = label ? label.textContent : null;
      if (label) label.textContent = "Copied";
      button.classList.add("copied");
      setTimeout(function () { if (label) label.textContent = old; button.classList.remove("copied"); }, 1600);
    };
    if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).then(done);
    else { var area = document.createElement("textarea"); area.value = text; document.body.appendChild(area); area.select(); document.execCommand("copy"); area.remove(); done(); }
  });

  /* Apple Watch key: make one (shown once) or revoke it. */
  var keyPanel = document.querySelector("[data-key-panel]");
  if (keyPanel) {
    var show = keyPanel.querySelector("[data-key-new]");
    keyPanel.addEventListener("click", function (event) {
      var create = event.target.closest("[data-key-create]");
      var revoke = event.target.closest("[data-key-revoke]");
      if (create) {
        if (keyPanel.getAttribute("data-has-key") === "true" && !confirm("Make a new key? The old one stops working: update Health Auto Export with the new one.")) return;
        create.disabled = true;
        post("/settings/sync-key").then(function (result) {
          create.disabled = false;
          if (!result.isValid) { alert(errorText(result)); return; }
          keyPanel.setAttribute("data-has-key", "true");
          show.hidden = false;
          show.querySelector("code").textContent = result.data.key;
          keyPanel.querySelectorAll("[data-key-value]").forEach(function (el) { el.textContent = result.data.key; });
          var hint = keyPanel.querySelector("[data-key-hint]");
          if (hint) hint.textContent = result.data.status.keyHint;
          show.scrollIntoView({ behavior: "smooth", block: "center" });
        });
      }
      if (revoke) {
        if (!confirm("Revoke the key? Health Auto Export and the Shortcut stop syncing until you make a new one.")) return;
        post("/settings/sync-key/revoke").then(function (result) {
          if (!result.isValid) { alert(errorText(result)); return; }
          location.reload();
        });
      }
    });
  }

  /* Setup guides: Health Auto Export / Shortcut tabs. */
  document.querySelectorAll("[data-tabs]").forEach(function (group) {
    var buttons = group.querySelectorAll("[role=tab]");
    buttons.forEach(function (button) {
      button.addEventListener("click", function () {
        buttons.forEach(function (other) {
          var on = other === button;
          other.setAttribute("aria-selected", String(on));
          var panel = document.getElementById(other.getAttribute("aria-controls"));
          if (panel) panel.hidden = !on;
        });
      });
    });
  });

  /* Admin: delete an account. */
  document.querySelectorAll("[data-delete-user]").forEach(function (button) {
    button.addEventListener("click", function () {
      if (!confirm("Delete " + button.getAttribute("data-name") + " and all their data? This can't be undone.")) return;
      post("/settings/users/delete", { identifier: button.getAttribute("data-delete-user") }).then(function (result) {
        if (!result.isValid) { alert(errorText(result)); return; }
        button.closest("tr").remove();
      });
    });
  });
})();
