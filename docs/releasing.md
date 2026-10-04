# Releasing

Packages are published to nuget.org by the **Release** workflow
([`.github/workflows/release.yml`](../.github/workflows/release.yml)). It never runs on its own:
someone has to start it from the Actions tab.

## Versioning

Both packages always ship with the same version, and `SSP.net.Serial` depends on exactly the
`SSP.net` version it was built with.

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
  pre-releases (`dotnet add package SSP.net --prerelease`), and it sorts below the eventual `1.0.0`.

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

Re-running a failed run is safe: the push uses `--skip-duplicate`, and a re-run keeps its run
number, so it publishes the same version.

## One-time setup

### nuget.org API key

1. Sign in at nuget.org, open **API Keys** and create a key:
   - **Key name:** anything, for example `ssp-github-release`.
   - **Scopes:** *Push* > *Push new packages and package versions*.
   - **Glob pattern:** `SSP.net*` (covers both packages).
   - **Expiration:** up to 365 days. Note the date; the workflow fails with a 403 once it expires.
2. Copy the key. nuget.org shows it only once.

### GitHub

1. **Settings > Environments > New environment**, named `nuget`.
   - Optional: add yourself under **Required reviewers**, so each release waits for a click
     before anything is pushed to nuget.org.
   - Optional: under **Deployment branches and tags**, restrict it to `main`.
2. In that environment, **Add environment secret**:
   - **Name:** `NUGET_API_KEY`
   - **Value:** the key from nuget.org.

A repository secret with the same name (**Settings > Secrets and variables > Actions**) also works
if you would rather skip the environment. Nothing else is needed: the tag and the GitHub release
are created with the workflow's own token.

## Going stable

When `1.0.0` is ready, change `PRERELEASE_LABEL` handling in the workflow to publish without a
suffix (pass `-p:Version=$(VersionPrefix)` instead of `-p:VersionSuffix=...`) and drop
`--prerelease` from the release step, then bump `VersionPrefix` to the next line for the previews
that follow.
