# Releasing LinqCube

How a new `LinqCube` NuGet package is cut. Versioning is automated by **GitVersion.MsBuild**
(`GitVersion.yml`), the package is produced by `GeneratePackageOnBuild`, and the repo follows
**GitFlow**.

## Branching model (GitFlow)

- **`develop`** — where all work lands (fixes, features, `next-version` bumps). Builds here get a
  **prerelease** version (e.g. `10.2.0-alpha.0`). This is expected; do not try to force a clean version
  on `develop`.
- **`master`** — release branch. A release is a `--no-ff` merge of `develop` into `master` with the
  message **`Merge branch 'develop' for X.Y.Z release`**, then an annotated-or-lightweight tag
  **`X.Y.Z`**. Building `master` at the tag yields the **clean** version `X.Y.Z`.

The git history is the spec — every release looks like:

```
*   (tag: 10.1.1, master) Merge branch 'develop' for 10.1.1 release
|\
| * (develop) <the work + "set next-version 10.1.1">
```

## Versioning

- `GitVersion.yml` holds **`next-version`** — the floor for the next release. Bump it on `develop`
  (its own commit, message style `… set next-version X.Y.Z`) according to the change:
  - **patch** (`x.y.Z`) — bug fix, no API change.
  - **minor** (`x.Y.0`) — backwards-compatible feature.
  - **major** (`X.0.0`) — breaking change.
- `semantic-version-format: Loose`; `main` (and effectively `master`) is `mode: ContinuousDeployment`.
- **Gotcha:** you can **not** force the version with `dotnet … -p:Version=X.Y.Z` — GitVersion.MsBuild
  overwrites it (unless you also pass `-p:UpdateVersionProperties=false`). The clean version comes from
  being on `master` at the matching tag, *not* from a command-line override. So: cut the version by
  tagging the right commit, not by overriding.

## Release checklist

Run from the repo root (`C:\Projects\!Libs\linq-cube`).

1. **On `develop`, finish the work** and make sure `next-version` is set to the version you intend to
   ship.
2. **Tests green:**
   ```
   dotnet test LinqCube.sln -c Debug
   ```
   (or just `dotnet test LinqCube.Tests/LinqCube.Tests.csproj`)
3. **Commit** everything on `develop` and push (`git push origin develop`).
4. **Merge to master + tag** (matches the existing history exactly):
   ```
   git checkout master
   git merge --no-ff develop -m "Merge branch 'develop' for X.Y.Z release"
   git tag X.Y.Z
   git push origin master develop --tags
   ```
5. **Pack from `master`** (the tag → clean version → `LinqCube.X.Y.Z.nupkg`):
   ```
   dotnet pack LinqCube/LinqCube.csproj -c Release -o ./artifacts
   ```
   Confirm the file is `LinqCube.X.Y.Z.nupkg` (no `-alpha`/`+meta` suffix). The package id is **`LinqCube`**.
6. **Publish to nuget.org** (needs the maintainer's API key — this step is the maintainer's):
   ```
   dotnet nuget push ./artifacts/LinqCube.X.Y.Z.nupkg -s https://api.nuget.org/v3/index.json -k <APIKEY>
   ```
7. Back on `develop`, bump `next-version` to the *next* anticipated version for ongoing work.
