### Bug fixes
- Ship `.meta` files for `package.json` and the root `README.md`, `CHANGELOG.md`, `LICENSE.md`, `RELEASE.md`, so installing the package no longer generates untracked `.meta` files in the consumer's project
- Stop shipping the internal design-note markdown (previously `docs/`), which Unity imported as TextAssets

### Infrastructure / Documentation
- `release.yml` workflow: pushing a `vX.Y.Z` tag now verifies it matches `package.json` and creates the GitHub release from `RELEASE.md`
- Release skills updated accordingly
