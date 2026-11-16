# Browser smoke test

Real Chromium → API → PostgreSQL → UI. API responses are not mocked. The existing Development-only sign-in and
checked 50-word sample are used. External auth, AI and paid speech are deliberately not configured: this is not
evidence of Telegram/Google login, real audio, or an actual iPhone/Android test.

Requirements: .NET 10, PostgreSQL (user `lugat`, password `lugat`, CREATEDB), Node 22.

```sh
dotnet build -c Release
npm ci --prefix tests/browser
npx --prefix tests/browser playwright install --with-deps chromium
npx --prefix tests/browser playwright test --config tests/browser/playwright.config.mjs
```

The config starts a Development app on port 5248 with a separate `lugat_browser` database. Two projects check
390px light/dark layouts. Screenshots and traces are retained on failure. CI runs the same commands.
Override `LUGAT_BROWSER_POSTGRES` for another isolated database. Never point this test at production.
`LUGAT_BROWSER_URL` uses an already running **isolated Development** app instead of starting one.

Story: Dev sign-in → first unit table → reveal/rate a card → answer unit test using the published catalog → pass →
reload persisted result → next unit. Hidden correct/accepted answers must not be exposed in the test DTO.

CI also dumps this sample database, restores it into a fresh `lugat_rehearsal_ci`, and runs the light-mode story
again with sample seeding disabled. This verifies the restored app flow, not just table counts.

For release acceptance, also test listening and pronunciation with configured services on real iPhone and Android;
first load from Instagram, Korean IME, offline/retry, and PWA update remain device checks.
