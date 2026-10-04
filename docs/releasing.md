# Releasing

Packages are published to nuget.org by the **Release** workflow
([`.github/workflows/release.yml`](../.github/workflows/release.yml)). It never runs on its own:
someone has to start it from the Actions tab.

## Versioning

Both packages always ship with the same version, and `SmileySecure.Net.Serial` depends on exactly the
`SmileySecure.Net` version it was built with.

| Where it was built | Version | Example |
| --- | --- | --- |
| Release workflow | `<VersionPrefix>-preview.<run number>` | `1.0.0-preview.3` |
| Anywhere else (local, CI) | `<VersionPrefix>-dev` | `1.0.0-dev` |

- `VersionPrefix` in [`Directory.Build.props`](../Directory.Build.props) is the release line,
  currently `1.0.0`. Change it there, in a pull request, to start a new line.
- The number after `preview.` is the Release workflow's run number. It only ever goes up, so every
  release has a unique version and nuget.org sorts them correctly (`preview.10` is newer than
  `preview.9`). A failed or dry run uses up a number, so gaps are normal.
- Packages built outside the workflow carry `-dev`, so a locally built package can never be
  confused with a published one.
- `1.0.0-preview.N` is a SemVer pre-release. NuGet hides it unless the consumer asks for
  pre-releases (`dotnet add package SmileySecure.Net --prerelease`), and it sorts below the eventual `1.0.0`.

## Running a release

1. Merge to `main` and let CI go green. The workflow refuses to run on any other branch.
2. **Actions > Release > Run workflow**, branch `main`.
   - Tick **dry run** to build, test and pack without publishing. The packages are attached to the
     run as the `packages` artifact so they can be inspected.
3. The workflow builds, runs the full test suite (everything except `Hardware`), packs both
   packages and then:
   - pushes both `.nupkg` files and their `.snupkg` symbol packages to nuget.org;
   - tags the commit `v<version>` and creates a GitHub release marked as a pre-release, with the
     packages attached and notes generated from the merged pull requests.

nuget.org validates and indexes a new package before it appears in search, which usually takes a
few minutes.

The push does not use `--skip-duplicate`, so a 409 Conflict from nuget.org fails the run instead of
passing silently. A re-run keeps its run number and so its version; if the first attempt already
published, start a new run instead.

## One-time setup

Publishing uses nuget.org [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing):
GitHub proves to nuget.org which repository and workflow is running, and nuget.org hands back an API
key that lasts about an hour. There is no long-lived API key to create, store or rotate.

### nuget.org

1. Sign in at nuget.org, open your user menu > **Trusted Publishing** and create a policy:
   - **Policy name:** anything, for example `ssp-github-release`.
   - **Package owner:** your account (or the organisation that will own the packages).
   - **Repository owner:** `jetedrow`
   - **Repository:** `SmileySecure.Net`
   - **Workflow file:** `release.yml`
   - **Environment:** leave blank. The workflow doesn't use a GitHub environment.
2. A new policy shows as *partially active* until the first successful release uses it; that is
   expected.

The policy is tied to the workflow file name, so renaming `release.yml` means editing the policy.

### GitHub

1. **Settings > Secrets and variables > Actions**, add a repository variable (Variables tab) or a
   repository secret (Secrets tab); the workflow reads either:
   - **Name:** `NUGET_USER`
   - **Value:** your nuget.org *username* (the profile name, not your email address).

A variable is the natural fit because the username is not sensitive, but a secret works too. There
are no other secrets to add: the tag and the GitHub release use the workflow's own token, and the
nuget.org key is minted per run.

## Going stable

When `1.0.0` is ready, change `PRERELEASE_LABEL` handling in the workflow to publish without a
suffix (pass `-p:Version=$(VersionPrefix)` instead of `-p:VersionSuffix=...`) and drop
`--prerelease` from the release step, then bump `VersionPrefix` to the next line for the previews
that follow.
