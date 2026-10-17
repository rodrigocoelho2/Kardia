/* Kardia — notifications on this phone or computer (Web Push, no third party). The browser subscribes with the
   server's public key; the server encrypts each notification for it and sends it through the browser's own push
   service. Needs HTTPS; on iPhone and iPad Kardia must be added to the Home Screen first (iOS 16.4+). */
(function () {
  "use strict";
  var card = document.querySelector("[data-push]");
  var token = (document.querySelector('meta[name="request-verification-token"]') || {}).content || "";
  var state = null, here = false;

  var supported = function () { return window.isSecureContext && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window; };
  var appleMobile = function () { return /iPhone|iPad|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1); };
  var installed = function () { return window.matchMedia("(display-mode: standalone)").matches || navigator.standalone === true; };

  function problem() {
    if (!window.isSecureContext) return "Notifications need HTTPS: open Kardia at its https address (" + location.host + " isn't).";
    if (!("serviceWorker" in navigator)) return "This browser has service workers off (private mode?), so it can't get notifications.";
    if (!("PushManager" in window) || !("Notification" in window)) {
      if (appleMobile()) {
        return installed()
          ? "iOS needs 16.4 or later and Kardia added from Safari: remove the icon, open Kardia in Safari, tap Share › Add to Home Screen, and open it from there."
          : "On iPhone and iPad: tap Share › Add to Home Screen, open Kardia from the new icon, then turn notifications on.";
      }
      return "This browser doesn't support web push. Try Safari, Chrome, Edge or Firefox.";
    }
    return null;
  }

  function api(method, url, body) {
    return fetch(url, {
      method: method,
      headers: { "RequestVerificationToken": token, "Accept": "application/json", "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
      credentials: "same-origin"
    }).then(function (r) { return r.json(); }).then(function (result) {
      if (!result.isValid) throw new Error((result.validationErrors || []).map(function (e) { return e.message; }).join(" ") || "Something went wrong.");
      return result.data;
    });
  }

  var bytes = function (text) {
    var b64 = text.replace(/-/g, "+").replace(/_/g, "/");
    var raw = atob(b64 + "===".slice((b64.length + 3) % 4));
    return Uint8Array.from(raw, function (c) { return c.charCodeAt(0); });
  };
  var base64Url = function (buffer) { return btoa(String.fromCharCode.apply(null, new Uint8Array(buffer))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, ""); };

  function keyOf(endpoint) {
    return crypto.subtle.digest("SHA-256", new TextEncoder().encode(endpoint)).then(function (digest) {
      return Array.from(new Uint8Array(digest)).map(function (b) { return b.toString(16).padStart(2, "0"); }).join("").slice(0, 12);
    });
  }

  function deviceLabel() {
    var a = navigator.userAgent;
    var device = /iPad/.test(a) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1) ? "iPad"
      : /iPhone/.test(a) ? "iPhone" : /Android/.test(a) ? "Android" : /Mac OS X/.test(a) ? "Mac" : /Windows/.test(a) ? "Windows" : /Linux/.test(a) ? "Linux" : "Device";
    var browser = /Edg\//.test(a) ? "Edge" : /Firefox\//.test(a) ? "Firefox" : /Chrome\//.test(a) ? "Chrome" : /Safari\//.test(a) ? "Safari" : "Browser";
    return device + " · " + (installed() ? "Kardia app" : browser);
  }

  function registration() { return navigator.serviceWorker.register("/sw.js").then(function () { return navigator.serviceWorker.ready; }); }
  function current() {
    if (!supported()) return Promise.resolve(null);
    return navigator.serviceWorker.getRegistration().then(function (reg) { return reg ? reg.pushManager.getSubscription() : null; });
  }

  function load() {
    return api("GET", "/settings/push").then(function (data) { state = data; }).catch(function () { state = { enabled: false, publicKey: "", devices: [] }; })
      .then(function () {
        here = false;
        if (!supported() || Notification.permission !== "granted") return;
        return current().then(function (sub) {
          if (!sub) return;
          return keyOf(sub.endpoint).then(function (key) { here = state.devices.some(function (d) { return d.key === key; }); });
        });
      })
      .then(paint);
  }

  function enable(quiet) {
    if (!supported()) { if (!quiet) say(problem()); return Promise.resolve(); }
    var ask = Notification.permission === "granted" ? Promise.resolve("granted") : Notification.requestPermission();
    return Promise.resolve(ask).then(function (permission) {
      if (permission !== "granted") {
        if (!quiet) say(permission === "denied"
          ? (appleMobile() ? "Notifications are off for Kardia. Turn them on in Settings › Notifications › Kardia, then try again." : "Notifications are blocked for this site. Allow them next to the address bar and try again.")
          : "The prompt closed without an answer. Tap the button again and choose Allow.");
        return;
      }
      return registration().then(function (reg) {
        return reg.pushManager.getSubscription().then(function (sub) {
          var made = sub && sub.options && sub.options.applicationServerKey;
          if (sub && made && base64Url(made) !== state.publicKey) return sub.unsubscribe().then(function () { return null; });
          return sub;
        }).then(function (sub) {
          return sub || reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: bytes(state.publicKey) });
        });
      }).then(function (sub) {
        var json = sub.toJSON();
        return api("POST", "/settings/push/subscribe", { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth, device: deviceLabel() });
      }).then(function () { if (!quiet) say("Notifications are on for this device.", true); });
    }).catch(function (e) {
      if (!quiet) say(e && (e.name === "NotAllowedError" || e.name === "AbortError") ? "The browser refused notifications here. Private windows can't get them." : (e && e.message) || "Turning notifications on failed.");
    }).then(load);
  }

  function disable() {
    return current().then(function (sub) {
      if (!sub) return;
      return api("POST", "/settings/push/unsubscribe", { endpoint: sub.endpoint }).then(function () { return sub.unsubscribe(); });
    }).catch(function (e) { say(e.message); }).then(load);
  }

  function say(text, good) {
    var box = card.querySelector("[data-push-problem]");
    box.hidden = !text;
    box.textContent = text || "";
    box.className = "small-note " + (good ? "good-t" : "muted");
  }

  function paint() {
    var on = card.querySelector("[data-push-on]"), off = card.querySelector("[data-push-off]"), test = card.querySelector("[data-push-test]");
    var summary = card.querySelector("[data-push-summary]");
    var list = card.querySelector("[data-push-devices]");
    var reason = problem();
    on.hidden = here || !state.enabled || Boolean(reason);
    off.hidden = !here;
    test.hidden = !state.devices.length;
    summary.textContent = !state.enabled ? "turned off on this server" : here ? "on for this device" : reason ? "not available here" : "off on this device";
    if (reason && state.enabled) say(reason);
    list.innerHTML = "";
    state.devices.forEach(function (d) {
      var row = document.createElement("div");
      row.className = "list-row";
      row.innerHTML = '<span class="ico-chip sky"><svg class="ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 16V11a6 6 0 0 1 12 0v5l1.5 2h-15z"/><path d="M10 20h4"/></svg></span>' +
        '<div class="grow"><span class="top"><span></span></span><small></small></div>' +
        '<button type="button" class="btn ghost small">Remove</button>';
      row.querySelector(".top span").textContent = d.device || "Device";
      var small = row.querySelector("small");
      small.textContent = d.lastError ? "Last delivery failed: " + d.lastError : d.lastSentAt ? "Last notification " + new Date(d.lastSentAt + (/Z$/.test(d.lastSentAt) ? "" : "Z")).toLocaleString() : "No notification sent yet";
      if (d.lastError) small.className = "err";
      row.querySelector("button").addEventListener("click", function () {
        api("POST", "/settings/push/unsubscribe", { id: d.id }).catch(function () { }).then(load);
      });
      list.appendChild(row);
    });
  }

  if (card) {
    card.querySelector("[data-push-on]").addEventListener("click", function () { enable(false); });
    card.querySelector("[data-push-off]").addEventListener("click", disable);
    card.querySelector("[data-push-test]").addEventListener("click", function () {
      api("POST", "/settings/push/test").then(function (r) {
        say(r.failed ? "Sent to " + r.reached + " device(s); " + r.failed + " failed." : "Test sent to " + r.reached + " device(s).", !r.failed);
      }).catch(function (e) { say(e.message); }).then(load);
    });
    load();
  }

  // A browser that renewed its subscription on its own tells the page, which sends the new one.
  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.addEventListener("message", function (event) {
      if (event.data && event.data.type === "push-resubscribe") api("GET", "/settings/push").then(function (data) { state = data; return enable(true); }).catch(function () { });
    });
  }
})();
