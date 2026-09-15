---
name: patch
description: Use when releasing a new patch version of the Voice Agents Unity package to GitHub
---

# Patch Release

Releases a new **patch** version: increments the last digit of the semver (e.g. `0.3.0` → `0.3.1`).

## Steps

### 1. Determine next version

Read `package.json` for `"version": "X.Y.Z"`. Next version is `X.Y.(Z+1)`.

### 2. Update version

Edit `package.json`: set `"version": "X.Y.(Z+1)"`. This is the only place the version lives — there is no `pyproject.toml`, `.csproj`, or `AssemblyInfo` to keep in sync. Do **not** touch the `"unity"` field (minimum Editor version) unless the release genuinely raises it.

### 3. Update CHANGELOG.md

Prepend a new section at the top (after the `# Changelog` heading) for the new version, following the existing `## [X.Y.Z] YYYY-MM-DD` style:

```markdown
## [X.Y.(Z+1)] YYYY-MM-DD

### Bug fixes
...

### Improvements (if applicable)
...
```

Summarise commits since the previous tag:

```bash
git log vX.Y.Z..HEAD --oneline
```

Call out anything that affects consumers of the package specifically: changes to public event structs or commands on `EventBus`, renamed/moved components, new package dependencies in `package.json`, changes to the named-pipe protocol the servers must support, and changes to the sample scene.

### 4. Update RELEASE.md

Replace the entire contents of `RELEASE.md` with only the body of the new changelog entry (the sections under the new version heading). **No top-level header** — start directly with `### Bug fixes` or equivalent. This file is the GitHub release notes.

### 5. Commit, tag, push

```bash
git add package.json CHANGELOG.md RELEASE.md
git commit -m "Release vX.Y.(Z+1): <one-line summary>"
git tag -a vX.Y.(Z+1) HEAD -m "<same one-line summary>"
git push origin main --follow-tags
```

The tag **must point to HEAD** and must match `package.json`'s `version` exactly — Unity Package Manager resolves `#vX.Y.Z` git URLs by tag, and a mismatch between the tag and `package.json` confuses the Package Manager's version display.

### 6. Confirm the GitHub release

Pushing the tag triggers `.github/workflows/release.yml`, which checks that the tag matches `package.json`'s version and creates the GitHub release with `RELEASE.md` as the notes. There is no package registry step (no PyPI equivalent): the git tag *is* the release.

Verify it appeared:

```bash
gh release view vX.Y.(Z+1)
```

If the workflow failed (e.g. tag/version mismatch), fix the cause and re-tag rather than creating the release by hand.

## What users get

Consumers install or upgrade with a tagged git URL in Package Manager (**+ ▸ Add package from git URL…**):

```
https://github.com/stefanwebb/unity-voice-agents.git#vX.Y.(Z+1)
```
