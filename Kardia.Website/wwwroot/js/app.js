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

  /* Forms that save as soon as a choice changes (sleep goal, appearance). */
  document.querySelectorAll("form[data-autosubmit]").forEach(function (form) {
    form.addEventListener("change", function () { form.submit(); });
  });

  /* Print or save as PDF. */
  document.querySelectorAll("[data-print]").forEach(function (button) {
    button.addEventListener("click", function () { window.print(); });
  });

  /* AI coach on the AI screen: start an analysis, then wait for the report (a local model can take minutes). */
  var analyse = document.querySelector("[data-ai-analyse]");
  if (analyse) {
    var wait = document.querySelector("[data-ai-wait]");
    var failure = document.querySelector("[data-ai-error]");
    var poll = function () {
      fetch("/coach/state", { headers: { "Accept": "application/json" }, credentials: "same-origin" })
        .then(function (r) { return r.json(); })
        .then(function (result) {
          if (result.isValid && result.data && result.data.running) { setTimeout(poll, 3000); return; }
          location.reload();
        })
        .catch(function () { setTimeout(poll, 5000); });
    };
    analyse.addEventListener("click", function () {
      analyse.disabled = true;
      analyse.querySelector("[data-label]").textContent = "Analysing…";
      if (wait) wait.hidden = false;
      if (failure) failure.hidden = true;
      post("/coach/analyse", { topic: "Overview" }).then(function (result) {
        if (!result.isValid) { alert(errorText(result)); analyse.disabled = false; return; }
        setTimeout(poll, 2500);
      });
    });
    if (analyse.getAttribute("data-running") === "true") setTimeout(poll, 3000);
  }

  /* AI cards on every screen: a short report about that screen, written in place. */
  document.querySelectorAll("[data-ai-card]").forEach(function (card) {
    var topic = card.getAttribute("data-topic");
    var date = card.getAttribute("data-date");
    var query = "?topic=" + encodeURIComponent(topic) + (date ? "&date=" + date : "");
    var go = card.querySelector("[data-ai-go]");
    var q = function (sel) { return card.querySelector(sel); };
    var timer = null;

    var item = function (tag, text, cls) { var el = document.createElement(tag); el.textContent = text; if (cls) el.className = cls; return el; };

    var paint = function (state) {
      if (!state || !state.enabled) { card.hidden = true; return; }
      card.hidden = false;
      var running = Boolean(state.running);
      go.disabled = running;
      go.querySelector("[data-label]").textContent = running ? "Analysing…" : state.report ? "Again" : "Ask AI";
      q("[data-ai-wait]").hidden = !running;
      q("[data-ai-error]").hidden = !state.error;
      q("[data-ai-error]").textContent = state.error || "";
      var report = state.report;
      q("[data-ai-body]").hidden = !report;
      if (!report) return;
      q("[data-ai-headline]").textContent = report.headline || "Your coach's report";
      q("[data-ai-summary]").textContent = report.summary || "";
      var actions = q("[data-ai-actions]");
      actions.innerHTML = "";
      (report.actions || []).forEach(function (a) { actions.appendChild(item("li", a)); });
      actions.hidden = !(report.actions || []).length;
      var lists = q("[data-ai-lists]");
      lists.innerHTML = "";
      (report.wentWell || []).forEach(function (a) { lists.appendChild(item("li", a, "Good")); });
      (report.watch || []).forEach(function (a) { lists.appendChild(item("li", a, "Caution")); });
      q("[data-ai-more]").hidden = !lists.children.length;
      var when = new Date(report.generatedAt);
      q("[data-ai-meta]").textContent = "Written " + when.toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) +
        " from " + report.daysAnalysed + " days of data · " + (state.model || "AI") + " · can be wrong, not medical advice";
    };

    var load = function () {
      return fetch("/coach/state" + query, { headers: { "Accept": "application/json" }, credentials: "same-origin" })
        .then(function (r) { return r.json(); })
        .then(function (result) {
          if (!result.isValid) return;
          var state = result.data;
          paint(state);
          clearTimeout(timer);
          if (state && state.running) { timer = setTimeout(load, 3000); return; }
        })
        .catch(function () { });
    };

    go.addEventListener("click", function () {
      go.disabled = true;
      q("[data-ai-wait]").hidden = false;
      q("[data-ai-error]").hidden = true;
      post("/coach/analyse", { topic: topic, date: date }).then(function (result) {
        if (!result.isValid) { go.disabled = false; q("[data-ai-wait]").hidden = true; alert(errorText(result)); return; }
        paint(result.data);
        timer = setTimeout(load, 2500);
      });
    });

    load();
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
