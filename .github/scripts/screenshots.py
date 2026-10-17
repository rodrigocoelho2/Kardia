"""Creates an account on a running Kardia in demo mode (every new account gets four months of sample Apple Watch data)
and screenshots every screen on a computer and a phone.

Usage: python screenshots.py <base-url> <output-folder> [--sync]
--sync first makes an Apple Watch key in Settings and posts a Health Auto Export payload to /api/sync with it (CI runs
this against SQL Server to check the whole path). Fails (exit 1) when a page throws a JavaScript error or the sync fails.
"""
import pathlib
import sys

from playwright.sync_api import sync_playwright

base = sys.argv[1].rstrip("/")
out = pathlib.Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
errors = []
sync = "--sync" in sys.argv[3:]

SAMPLE = {"data": {"metrics": [
    {"name": "heart_rate_variability", "units": "ms", "data": [{"date": "2026-10-16 06:58:00 +0100", "qty": 68}]},
    {"name": "resting_heart_rate", "units": "count/min", "data": [{"date": "2026-10-16 00:00:00 +0100", "qty": 52}]},
    {"name": "step_count", "units": "count", "data": [{"date": "2026-10-16 00:00:00 +0100", "qty": 9842}]},
    {"name": "heart_rate", "units": "count/min", "data": [
        {"date": f"2026-10-16 08:{m:02d}:00 +0100", "Avg": 120 + m} for m in range(30, 60)]},
    {"name": "sleep_analysis", "units": "hr", "data": [
        {"startDate": "2026-10-15 23:48:00 +0100", "endDate": "2026-10-16 00:20:00 +0100", "value": "Core"},
        {"startDate": "2026-10-16 00:20:00 +0100", "endDate": "2026-10-16 01:05:00 +0100", "value": "Deep"},
        {"startDate": "2026-10-16 01:05:00 +0100", "endDate": "2026-10-16 07:52:00 +0100", "value": "REM"}]}],
    "workouts": [{"name": "Outdoor Run", "start": "2026-10-16 08:30:00 +0100", "end": "2026-10-16 09:00:00 +0100",
                  "activeEnergyBurned": {"qty": 412, "units": "kcal"}, "distance": {"qty": 6.1, "units": "km"}}]}}

SCREENS = [
    ("today", "/"),
    ("recovery", "/recovery"),
    ("sleep", "/sleep"),
    ("strain", "/strain"),
    ("age", "/age"),
    ("journal", "/journal"),
    ("settings", "/settings"),
    ("health", "/health"),
    ("plan", "/plan"),
    ("trends", "/trends?days=90"),
    ("report", "/report"),
    ("coach", "/coach"),
    ("stress", "/stress"),
    ("calendar", "/calendar"),
    ("breathe", "/breathe"),
]


def watch(page, label):
    page.on("pageerror", lambda e: errors.append(f"{label}: {e}"))


def reveal(page):
    page.evaluate("document.querySelectorAll('.reveal').forEach(e => e.classList.add('in'))")


def sign_in(page):
    page.goto(base + "/create-account")
    if page.locator("input[name=confirmPassword]").count():
        page.fill("input[name=userName]", "Rodas")
        page.fill("input[name=password]", "recovery-2026")
        page.fill("input[name=confirmPassword]", "recovery-2026")
        page.fill("input[name=birthDate]", "1996-05-14")
        page.select_option("select[name=sex]", "Male")
        page.click("button[type=submit]")
        page.wait_for_load_state("networkidle")
    if "/sign-in" in page.url or "/create-account" in page.url:
        page.goto(base + "/sign-in")
        page.fill("input[name=userName]", "Rodas")
        page.fill("input[name=password]", "recovery-2026")
        page.click("button[type=submit]")
        page.wait_for_load_state("networkidle")


with sync_playwright() as p:
    browser = p.chromium.launch()
    contexts = {
        "desktop": dict(viewport={"width": 1440, "height": 940}),
        "phone": dict(viewport={"width": 390, "height": 844}, device_scale_factor=3, is_mobile=True, has_touch=True),
    }
    for name, options in contexts.items():
        context = browser.new_context(**options)
        page = context.new_page()
        watch(page, name)

        for shot, path in [("landing", "/"), ("sign-in", "/sign-in"), ("create-account", "/create-account")]:
            page.goto(base + path)
            reveal(page)
            page.wait_for_timeout(1500)
            page.screenshot(path=str(out / f"{name}-{shot}.png"))
            if shot == "landing":
                page.screenshot(path=str(out / f"{name}-{shot}-full.png"), full_page=True)

        sign_in(page)
        if sync and name == "desktop":
            page.goto(base + "/settings")
            result = page.evaluate(
                """async sample => {
                  const token = document.querySelector('meta[name="request-verification-token"]').content;
                  const made = await (await fetch('/settings/sync-key', { method: 'POST', headers: { RequestVerificationToken: token } })).json();
                  if (!made.isValid) return { step: 'key', made };
                  const sent = await fetch('/api/sync', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Kardia-Key': made.data.key }, body: JSON.stringify(sample) });
                  const wrong = await fetch('/api/sync', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Kardia-Key': 'kardia_sk_wrong' }, body: '{}' });
                  return { step: 'sync', status: sent.status, body: await sent.text(), wrong: wrong.status };
                }""",
                SAMPLE,
            )
            print("sync:", result)
            if result.get("status") != 200 or result.get("wrong") != 401:
                errors.append(f"sync failed: {result}")
        for shot, path in SCREENS:
            page.goto(base + path)
            if shot == "coach" and page.locator("[data-ai-analyse]").count() > 0 and page.locator(".coach-lead").count() == 0:
                # With an AI provider set up, ask for a report and wait for it.
                page.click("[data-ai-analyse]")
                try:
                    page.wait_for_selector(".coach-lead", timeout=90000)
                except Exception as error:  # noqa: BLE001 — no provider reachable: keep the page as it is
                    print("ai coach:", error)
            page.wait_for_timeout(1700)
            page.screenshot(path=str(out / f"{name}-{shot}.png"))
            page.screenshot(path=str(out / f"{name}-{shot}-full.png"), full_page=True)

        # The light theme.
        context.add_cookies([{"name": "kardia-theme", "value": "light", "url": base}])
        for shot, path in [("today-light", "/"), ("strain-light", "/strain")]:
            page.goto(base + path)
            page.wait_for_timeout(1700)
            page.screenshot(path=str(out / f"{name}-{shot}.png"))
        context.clear_cookies(name="kardia-theme")
        context.close()
    browser.close()

for error in errors:
    print("page error:", error)
print(f"{len(list(out.glob('*.png')))} screenshots in {out}")
sys.exit(1 if errors else 0)
