---
name: release
description: Use when releasing a new minor version of the Voice Agents Unity package to GitHub
---

# Minor Release

Releases a new **minor** version: increments the middle digit of the semver and resets the patch digit to 0 (e.g. `0.1.2` → `0.2.0`).

## Steps

### 1. Determine next version

Read `package.json` for `"version": "X.Y.Z"`. Next version is `X.(Y+1).0`.

### 2. Update version

Edit `package.json`: set `"version": "X.(Y+1).0"`. This is the only place the version lives — there is no `pyproject.toml`, `.csproj`, or `AssemblyInfo` to keep in sync. Do **not** touch the `"unity"` field (minimum Editor version) unless the release genuinely raises it.

### 3. Update CHANGELOG.md

Prepend a new section at the top (after the `# Changelog` heading) for the new version, following the existing `## [X.Y.Z] YYYY-MM-DD` style:

```markdown
## [X.(Y+1).0] YYYY-MM-DD

### New features
...

### Improvements
...

### Infrastructure / Documentation (if applicable)
...
```

Summarise commits since the previous tag:

```bash
git log vX.Y.Z..HEAD --oneline
```

Call out anything that affects consumers of the package specifically: changes to public event structs or commands on `EventBus`, renamed/moved components, new package dependencies in `package.json`, changes to the named-pipe protocol the servers must support, and changes to the sample scene.

### 4. Update RELEASE.md

Replace the entire contents of `RELEASE.md` with only the body of the new changelog entry (the sections under the new version heading). **No top-level header** — start directly with `### New features` or equivalent. This file is the GitHub release notes.

### 5. Commit, tag, push

```bash
git add package.json CHANGELOG.md RELEASE.md
git commit -m "Release vX.(Y+1).0: <one-line summary>"
git tag -a vX.(Y+1).0 HEAD -m "<same one-line summary>"
git push origin main --follow-tags
```

The tag **must point to HEAD** and must match `package.json`'s `version` exactly — Unity Package Manager resolves `#vX.Y.Z` git URLs by tag, and a mismatch between the tag and `package.json` confuses the Package Manager's version display.

### 6. Create the GitHub release

There is no package registry step (no PyPI equivalent): the git tag *is* the release. Publish it on GitHub so it shows up under Releases:

```bash
gh release create vX.(Y+1).0 -F RELEASE.md -t "vX.(Y+1).0"
```

If `.github/workflows/release.yml` exists and triggers on `v*` tags, skip this step — the push in step 5 already created the release from `RELEASE.md`.

## What users get

Consumers install or upgrade with a tagged git URL in Package Manager (**+ ▸ Add package from git URL…**):

```
https://github.com/stefanwebb/unity-voice-agents.git#vX.(Y+1).0
```
