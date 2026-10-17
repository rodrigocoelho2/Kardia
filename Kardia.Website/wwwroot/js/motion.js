/* Kardia — motion: page transitions (view transitions between pages, sliding between days and tabs), cards that come in
   as they're scrolled to, numbers that count up, the glass top bar on the phone, swiping between days and a thin
   loading bar. Pure progressive enhancement: without it (or with "reduce motion") every page works the same. */
(function () {
  "use strict";
  // Automated browsers (screenshots, tests) get the still version, so full-page captures show every card.
  var still = navigator.webdriver || (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
  var KEY = "kardia-nav-dir";

  /* The highlight behind the current item (the view transition glides it to the new one). */
  document.querySelectorAll(".nav a[aria-current='page'], .tabbar a[aria-current='page']").forEach(function (link) {
    var pill = document.createElement("span");
    pill.className = "nav-pill";
    pill.setAttribute("aria-hidden", "true");
    link.insertBefore(pill, link.firstChild);
  });

  /* iOS only shows :active with a touch listener on the page. */
  document.addEventListener("touchstart", function () { }, { passive: true });

  /* Direction of the next page change: tabs to the right slide in from the right, days forward too. */
  var remember = function (dir) { try { sessionStorage.setItem(KEY, dir + "|" + Date.now()); } catch (e) { } };
  var recall = function (clear) {
    try {
      var v = (sessionStorage.getItem(KEY) || "").split("|");
      if (clear) sessionStorage.removeItem(KEY);
      return v[0] && Date.now() - Number(v[1]) < 4000 ? v[0] : null;
    } catch (e) { return null; }
  };
  var tabs = Array.prototype.slice.call(document.querySelectorAll(".tabbar a"));
  var current = tabs.findIndex(function (a) { return a.getAttribute("aria-current") === "page"; });
  tabs.forEach(function (a, i) {
    a.addEventListener("click", function () { if (current >= 0 && i !== current) remember(i > current ? "next" : "prev"); });
  });
  var dateLinks = document.querySelectorAll(".datenav a, .week a");
  dateLinks.forEach(function (a) {
    a.addEventListener("click", function () {
      var href = a.getAttribute("href") || "";
      var m = /date=(\d{4}-\d{2}-\d{2})/.exec(href);
      var shown = (/date=(\d{4}-\d{2}-\d{2})/.exec(location.search) || [])[1];
      var target = m ? m[1] : "9999-12-31";
      var now = shown || "9999-12-31";
      if (target !== now) remember(target > now ? "next" : "prev");
    });
  });
  window.addEventListener("pageswap", function (e) {
    var d = recall(false);
    if (e.viewTransition && d && e.viewTransition.types) e.viewTransition.types.add(d);
  });
  window.addEventListener("pagereveal", function (e) {
    var d = recall(true);
    if (e.viewTransition && d && e.viewTransition.types) e.viewTransition.types.add(d);
  });

  /* The loading bar: shown when a page change takes a moment. */
  var bar = document.createElement("div");
  bar.className = "nav-progress";
  document.body.appendChild(bar);
  var barTimer = null;
  document.addEventListener("click", function (e) {
    var a = e.target.closest && e.target.closest("a[href]");
    if (!a || e.defaultPrevented || e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0 || a.target === "_blank") return;
    var url = new URL(a.href, location.href);
    if (url.origin !== location.origin || (url.pathname === location.pathname && url.search === location.search)) return;
    clearTimeout(barTimer);
    barTimer = setTimeout(function () { bar.classList.add("on"); }, 120);
  });
  document.addEventListener("submit", function () { barTimer = setTimeout(function () { bar.classList.add("on"); }, 120); });
  window.addEventListener("pageshow", function () { clearTimeout(barTimer); bar.classList.remove("on"); });

  /* The phone's top bar turns to glass once you scroll. */
  var top = document.querySelector(".phone-top");
  if (top) {
    var onScroll = function () { top.classList.toggle("scrolled", window.scrollY > 8); };
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();
  }

  if (still) return;

  /* Numbers count up the first time they're seen. */
  var countUp = function (root) {
    root.querySelectorAll(".ring-box .big, .tile .v, .hero-foot b, .pg-val, .metric .v, .cl-ratio, .age-big, .teaser .age-num, .bedtime-skip").forEach(function (el) {
      var node = el.firstChild;
      if (!node || node.nodeType !== 3) return;
      var text = node.nodeValue;
      var m = /^(\s*)(-?)([\d   ,]*\d)(\.(\d+))?(\s*)$/.exec(text);
      if (!m) return;
      var whole = m[3].replace(/[\s  ,]/g, "");
      var decimals = m[5] ? m[5].length : 0;
      var target = parseFloat(m[2] + whole + (m[5] ? "." + m[5] : ""));
      if (!isFinite(target) || Math.abs(target) < 1.5 && decimals === 0) return;
      var grouped = /[\s  ]/.test(m[3]) ? " " : /,/.test(m[3]) ? "," : "";
      var format = function (v) {
        var s = Math.abs(v).toFixed(decimals);
        var parts = s.split(".");
        if (grouped) parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, grouped);
        return m[1] + (v < 0 ? "-" : "") + parts.join(".") + m[6];
      };
      var start = null, duration = 900 + Math.min(500, Math.abs(target) * 2);
      var step = function (t) {
        if (start === null) start = t;
        var k = Math.min(1, (t - start) / duration);
        var eased = 1 - Math.pow(1 - k, 3);
        node.nodeValue = format(target * eased);
        if (k < 1) requestAnimationFrame(step); else node.nodeValue = text;
      };
      node.nodeValue = format(0);
      requestAnimationFrame(step);
    });
  };

  /* Cards come in as they're scrolled to; the first screenful plays as the page opens. */
  var cards = Array.prototype.slice.call(document.querySelectorAll(".page .rise"));
  if ("IntersectionObserver" in window) {
    var fold = window.innerHeight * 0.92;
    var watcher = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        watcher.unobserve(entry.target);
        entry.target.style.setProperty("--d", "0");
        entry.target.classList.remove("reveal-wait");
        countUp(entry.target);
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.06 });
    cards.forEach(function (card) {
      if (card.getBoundingClientRect().top > fold) {
        card.classList.add("reveal-wait");
        watcher.observe(card);
      } else {
        countUp(card);
      }
    });
  } else {
    cards.forEach(countUp);
  }

  /* Swipe left / right on a day screen to go to the next / previous day. */
  var nav = document.querySelector(".datenav");
  var page = document.querySelector(".page");
  if (nav && page && window.matchMedia("(pointer: coarse)").matches) {
    var links = nav.querySelectorAll("a");
    var prevLink = links[0] || null;
    var nextLink = links.length > 1 ? links[links.length - 1] : null;
    var hints = {};
    ["prev", "next"].forEach(function (k) {
      var h = document.createElement("div");
      h.className = "swipe-hint " + k;
      h.innerHTML = k === "prev"
        ? '<svg class="ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 5l-7 7 7 7"/></svg>'
        : '<svg class="ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 5l7 7-7 7"/></svg>';
      document.body.appendChild(h);
      hints[k] = h;
    });
    var x0 = 0, y0 = 0, dx = 0, tracking = false, decided = false, horizontal = false;
    var blocked = function (el) { return el.closest(".scroll-x, input, select, textarea, .tabbar, [data-no-swipe]"); };
    page.addEventListener("touchstart", function (e) {
      if (e.touches.length !== 1 || blocked(e.target)) { tracking = false; return; }
      x0 = e.touches[0].clientX; y0 = e.touches[0].clientY; dx = 0; tracking = true; decided = false; horizontal = false;
    }, { passive: true });
    page.addEventListener("touchmove", function (e) {
      if (!tracking) return;
      var mx = e.touches[0].clientX - x0, my = e.touches[0].clientY - y0;
      if (!decided && (Math.abs(mx) > 10 || Math.abs(my) > 10)) { decided = true; horizontal = Math.abs(mx) > Math.abs(my) * 1.4; }
      if (!horizontal) return;
      var link = mx < 0 ? nextLink : prevLink;
      dx = link ? mx : mx * 0.25;
      page.classList.add("swiping");
      page.style.transform = "translateX(" + (dx * 0.35) + "px)";
      page.style.opacity = String(1 - Math.min(0.35, Math.abs(dx) / 600));
      hints.prev.classList.toggle("ready", dx > 70 && Boolean(prevLink));
      hints.next.classList.toggle("ready", dx < -70 && Boolean(nextLink));
    }, { passive: true });
    page.addEventListener("touchend", function () {
      if (!tracking || !horizontal) { tracking = false; return; }
      tracking = false;
      hints.prev.classList.remove("ready"); hints.next.classList.remove("ready");
      var link = dx < -70 ? nextLink : dx > 70 ? prevLink : null;
      page.classList.remove("swiping");
      if (link) { link.click(); return; }
      page.classList.add("swipe-back");
      page.style.transform = ""; page.style.opacity = "";
      setTimeout(function () { page.classList.remove("swipe-back"); }, 380);
    });
  }
})();
