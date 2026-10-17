/* Kardia — landing page motion: the scroll reveal, the tilting phone, the live heart rate and the "move last night"
   recovery demo. Every number here is an example; the demo uses a simplified copy of the app's recovery formula. */
(function () {
  "use strict";
  var root = document.querySelector(".lp");
  if (!root) return;
  var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  /* Reveal sections as they scroll in. */
  if ("IntersectionObserver" in window && !still) {
    root.classList.add("js-reveal");
    var seen = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) { entry.target.classList.add("in"); seen.unobserve(entry.target); }
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });
    root.querySelectorAll(".reveal").forEach(function (el) { seen.observe(el); });
  }

  /* Tilt the phone with the pointer; let it drift on its own when idle. */
  var art = root.querySelector(".lp-art");
  var phone = root.querySelector("[data-tilt]");
  if (art && phone) {
    var idle = true, t0 = performance.now();
    var tilt = function (rx, ry) { phone.style.transform = "rotateX(" + rx + "deg) rotateY(" + ry + "deg)"; };
    art.addEventListener("pointermove", function (e) {
      if (e.pointerType === "touch") return;
      idle = false;
      var r = art.getBoundingClientRect();
      tilt(-((e.clientY - r.top) / r.height - 0.5) * 12, ((e.clientX - r.left) / r.width - 0.5) * 16);
    });
    art.addEventListener("pointerleave", function () { idle = true; t0 = performance.now(); });
    if (still) tilt(2, -6);
    else (function drift(now) {
      if (idle) { var k = (now - t0) / 1000; tilt(Math.sin(k * 0.6) * 4 + 2, Math.cos(k * 0.45) * 8 - 6); }
      requestAnimationFrame(drift);
    })(performance.now());
  }

  /* The floating heart rate breathes around 68. */
  var bpm = root.querySelector("[data-bpm]");
  if (bpm && !still) {
    var value = 68;
    setInterval(function () {
      if (document.hidden) return;
      value = Math.max(62, Math.min(76, value + Math.round((Math.random() - 0.5) * 4)));
      bpm.textContent = value;
    }, 1600);
  }

  /* Move last night: HRV, resting heart rate and sleep → recovery, zone and strain target. */
  var demo = root.querySelector("[data-demo]");
  if (demo) {
    var input = function (name) { return demo.querySelector('[data-in="' + name + '"]'); };
    var output = function (name) { return demo.querySelector('[data-out="' + name + '"]'); };
    var ring = demo.querySelector("[data-ring] svg");
    var values = ring ? ring.querySelectorAll(".ring-val") : [];
    var knob = ring ? ring.querySelector(".ring-knob") : null;
    var stops = ring ? ring.querySelectorAll("stop") : [];
    var LENGTH = 565.49;
    var NEED = 495;
    var PALETTES = {
      High: ["#C6FFE6", "#5BE3A6", "#1FB57A"],
      Medium: ["#FFE7A3", "#F5C451", "#E0A21C"],
      Low: ["#FFC2C2", "#FF6B6B", "#E04848"]
    };
    var WORDS = {
      High: ["Ready to push", "var(--rec)", "Your body is ready: intervals, a long ride or a heavy session will pay off."],
      Medium: ["Steady day", "var(--mid)", "A normal day: train, but keep the hardest work for a green day."],
      Low: ["Take it easy", "var(--low)", "Keep it light: walk, stretch, sleep early. Hard training today costs more than it gives."]
    };

    var duration = function (minutes) { return Math.floor(minutes / 60) + "h " + String(minutes % 60).padStart(2, "0") + "m"; };
    var clamp = function (v, lo, hi) { return Math.max(lo, Math.min(hi, v)); };

    var paint = function () {
      var hrv = Number(input("hrv").value), rhr = Number(input("rhr").value), sleep = Number(input("sleep").value);
      [["hrv", -40, 40], ["rhr", -6, 10], ["sleep", 240, 600]].forEach(function (s) {
        var el = input(s[0]);
        el.style.setProperty("--p", ((Number(el.value) - s[1]) / (s[2] - s[1]) * 100) + "%");
      });
      output("hrv").textContent = (hrv > 0 ? "+" : hrv < 0 ? "−" : "±") + Math.abs(hrv) + "%";
      output("rhr").textContent = (rhr > 0 ? "+" : rhr < 0 ? "−" : "±") + Math.abs(rhr) + " bpm";
      output("sleep").textContent = duration(sleep);

      /* Same shape as the app: z-scores against your normal, weighted, through a logistic curve. */
      var hrvZ = clamp(Math.log(1 + hrv / 100) / 0.16, -3, 3);
      var rhrZ = clamp(-rhr / 3, -3, 3);
      var performance = Math.min(100, sleep / NEED * 100);
      var sleepZ = clamp((performance - 85) / 10, -3, 2);
      var signal = 0.45 * hrvZ + 0.25 * rhrZ + 0.2 * sleepZ;
      var score = Math.round(clamp(100 / (1 + Math.exp(-1.3 * (signal + 0.35))), 1, 99));
      var zone = score >= 67 ? "High" : score >= 34 ? "Medium" : "Low";
      var centre = clamp(5.5 + score * 0.115, 4, 19);

      output("score").textContent = score;
      output("zone").textContent = WORDS[zone][0];
      output("zone").style.color = WORDS[zone][1];
      output("advice").textContent = WORDS[zone][2];
      output("target").textContent = (centre - 1).toFixed(1) + "–" + (centre + 1).toFixed(1);

      var fraction = score / 100, length = fraction * LENGTH, angle = fraction * 2 * Math.PI;
      values.forEach(function (circle) { circle.setAttribute("stroke-dasharray", length.toFixed(1) + " " + LENGTH); });
      if (knob) { knob.setAttribute("cx", (110 + 90 * Math.cos(angle)).toFixed(1)); knob.setAttribute("cy", (110 + 90 * Math.sin(angle)).toFixed(1)); }
      stops.forEach(function (stop, i) { stop.setAttribute("stop-color", PALETTES[zone][i] || PALETTES[zone][2]); });
    };

    demo.addEventListener("input", paint);
    paint();
  }
})();
