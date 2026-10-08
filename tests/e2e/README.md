# Browser scripts (Playwright)

Standalone Node scripts that drive a running R2WAI instance. They are not part of `dotnet test`;
the .NET test projects live next to this folder (`tests/R2WAI.*.Tests`).

Run from the repo root (screenshot paths are root-relative):

```sh
export R2WAI_TEST_EMAIL=... R2WAI_TEST_PASSWORD=...
# optional: R2WAI_BASE_URL (default http://localhost:3001), R2WAI_API_URL (default http://localhost:5000)
npm run e2e:pages
```

| Script | npm | Purpose |
|---|---|---|
| `check-all-pages.mjs` | `e2e:pages` | Log in and screenshot every primary page |
| `enterprise-audit.mjs` | `e2e:audit` | Full UI audit across admin/operator pages |
| `full-cycle.mjs` | `e2e:full-cycle` | Workflow execute + assistant chat round trip |
| `final-verify.mjs` | `e2e:verify` | Smoke check of home, runs, chat, operations |
| `page-usefulness-audit.mjs` | `e2e:usefulness` | Per-page usefulness screenshots |
| `runs-tab-check.mjs`, `runs-visual.mjs` | — | Runs tab spot checks |
| `seed-data.mjs` | `e2e:seed` | Seed demo data through the API |

Shared config is in `helpers/test-env.mjs`. Output goes to `screenshots/` (git-ignored).

Component-level Playwright specs for the React client are in `src/R2WAI.Client/e2e`.
