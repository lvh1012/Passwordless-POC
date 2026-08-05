# Task 4 fix round 1 review package (Git unavailable)

Previous finding: `LoginPath = "/"` still causes CookieAuthenticationHandler to append `?ReturnUrl=%2Fdashboard`, while the Task 4 acceptance test requires an exact `/` redirect. The fix must make anonymous protected-page redirects go directly to `/` and preserve production cookie security settings. Inspect the current Program.cs, page test, and appended report; check fix-scope breakage only.

