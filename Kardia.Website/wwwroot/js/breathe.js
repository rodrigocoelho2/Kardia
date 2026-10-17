/* Kardia — guided breathing: the orb grows as you breathe in, holds, and shrinks as you breathe out. */
(function () {
  "use strict";
  var box = document.querySelector("[data-breathe]");
  if (!box) return;

  var orb = box.querySelector("[data-orb]");
  var phaseText = box.querySelector("[data-phase]");
  var countText = box.querySelector("[data-count]");
  var leftText = box.querySelector("[data-left]");
  var cyclesText = box.querySelector("[data-cycles]");
  var start = box.querySelector("[data-start]");
  var startLabel = start.querySelector("[data-label]");
  var done = box.querySelector("[data-done]");
  var doneText = box.querySelector("[data-done-text]");
  var log = box.querySelector("[data-log]");
  var token = (document.querySelector('meta[name="request-verification-token"]') || {}).content || "";

  var minutes = 1;
  var timing = [5.5, 0, 5.5, 0];
  var names = ["Breathe in", "Hold", "Breathe out", "Hold"];
  var timer = null, ticker = null, endsAt = 0, cycles = 0, running = false, wake = null;

  function fmt(seconds) {
    seconds = Math.max(0, Math.ceil(seconds));
    return Math.floor(seconds / 60) + ":" + String(seconds % 60).padStart(2, "0");
  }

  function showLeft() { leftText.textContent = fmt(running ? (endsAt - Date.now()) / 1000 : minutes * 60); }

  function pick(selector, attribute, value) {
    box.closest(".row").querySelectorAll(selector).forEach(function (el) { el.setAttribute(attribute, String(el === value)); });
  }

  document.querySelectorAll("[data-minutes]").forEach(function (button) {
    button.addEventListener("click", function () {
      if (running) return;
      minutes = Number(button.getAttribute("data-minutes"));
      pick("[data-minutes]", "aria-pressed", button);
      showLeft();
    });
  });

  document.querySelectorAll("[data-pattern]").forEach(function (button) {
    button.addEventListener("click", function () {
      if (running) return;
      timing = button.getAttribute("data-timing").split(",").map(Number);
      pick("[data-pattern]", "aria-checked", button);
    });
  });

  function phase(index) {
    if (!running) return;
    if (Date.now() >= endsAt && index === 0) { finish(true); return; }
    var seconds = timing[index];
    if (!seconds) { phase((index + 1) % 4); return; }
    if (index === 0) { cycles++; cyclesText.textContent = cycles === 1 ? "1 breath" : cycles + " breaths"; }
    phaseText.textContent = names[index];
    orb.style.transitionDuration = seconds + "s";
    orb.classList.toggle("in", index === 0 || index === 1);
    orb.setAttribute("data-step", String(index));
    if (navigator.vibrate) { try { navigator.vibrate(index === 0 ? 30 : 15); } catch (e) { /* not supported */ } }
    var until = Date.now() + seconds * 1000;
    clearInterval(ticker);
    var tick = function () { countText.textContent = Math.max(1, Math.ceil((until - Date.now()) / 1000)); showLeft(); };
    tick();
    ticker = setInterval(tick, 250);
    timer = setTimeout(function () { phase((index + 1) % 4); }, seconds * 1000);
  }

  function finish(complete) {
    running = false;
    clearTimeout(timer);
    clearInterval(ticker);
    box.classList.remove("running");
    orb.classList.remove("in");
    orb.style.transitionDuration = "1.2s";
    phaseText.textContent = complete ? "Done" : "Ready";
    countText.textContent = "";
    startLabel.textContent = "Start";
    showLeft();
    if (wake) { try { wake.release(); } catch (e) { /* ignore */ } wake = null; }
    if (complete) {
      done.hidden = false;
      doneText.textContent = cycles + " slow breaths in " + minutes + (minutes === 1 ? " minute." : " minutes.");
    }
  }

  start.addEventListener("click", function () {
    if (running) { finish(false); return; }
    running = true;
    cycles = 0;
    cyclesText.textContent = "";
    done.hidden = true;
    endsAt = Date.now() + minutes * 60 * 1000;
    box.classList.add("running");
    startLabel.textContent = "Stop";
    if (navigator.wakeLock) { navigator.wakeLock.request("screen").then(function (lock) { wake = lock; }).catch(function () { /* fine */ }); }
    phase(0);
  });

  log.addEventListener("click", function () {
    var body = new URLSearchParams();
    body.append("date", box.getAttribute("data-date"));
    body.append("tag", "Meditation");
    body.append("on", "true");
    log.setAttribute("aria-busy", "true");
    fetch("/journal/set", { method: "POST", headers: { "RequestVerificationToken": token, "Accept": "application/json" }, body: body, credentials: "same-origin" })
      .then(function (response) { return response.json(); })
      .then(function (result) {
        log.querySelector("[data-label]").textContent = result && result.isValid ? "Logged in today's journal" : "Couldn't log it. Try again.";
        if (result && result.isValid) log.disabled = true;
      })
      .catch(function () { log.querySelector("[data-label]").textContent = "Couldn't log it. Try again."; })
      .finally(function () { log.removeAttribute("aria-busy"); });
  });

  document.addEventListener("visibilitychange", function () { if (document.hidden && running) finish(false); });
  showLeft();
})();
