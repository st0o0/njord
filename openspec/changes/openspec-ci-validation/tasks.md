## 1. Decide (blocked on the user)

- [ ] 1.1 Discuss design.md Open questions Q1-Q6 with the user; record answers under Decisions in design.md
- [ ] 1.2 Decide how FunkArr (`aa6952f`) is aligned with the outcome (keep, amend, or replace)

## 2. Implement (only after 1.x; skeleton, refine once decided)

- [ ] 2.1 Create the agreed workflow/job per Q1 with the agreed install method, pin, scope, blocking mode and action pinning (Q2-Q4, Q6)
- [ ] 2.2 Wire the root guard into CI if agreed (Q5), using the script from `openspec-hygiene`
- [ ] 2.3 Lint the workflow locally (`actionlint` if available) and compare against `.github/workflows/ci.yml`

## 3. Validation

- [ ] 3.1 `openspec validate openspec-ci-validation` passes
- [ ] 3.2 `git diff --stat` shows only the agreed `.github/` files and this change; nothing under `src/`
- [ ] 3.3 Commit with a Conventional Commit message (`ci: ...`); do not push
