# Releasing Smart.NET

All three packages (`Smart.NET.Core`, `Smart.NET.Abstractions`, and
`Smart.NET.Jev`) use the same SemVer version. The release tag is the
authoritative version source; `Directory.Build.props` supplies
`0.1.0-alpha.1` only as the default for local development. A release tag such
as `v0.1.0-alpha.2` is converted to package version `0.1.0-alpha.2` by CI.

## One-time setup

1. Ensure the GitHub repository URL
   `https://github.com/smartdotnet/Smart.NET` is publicly accessible and the
   NuGet package IDs are available or belong to the publishing account.
2. On nuget.org, open the account's **Trusted Publishing** settings and add a
   GitHub policy with:
   - Repository owner: `smartdotnet`
   - Repository: `Smart.NET`
   - Workflow file: `publish.yml` (enter the filename only, not the
     `.github/workflows/` path)
   - Environment: `release`
3. In GitHub repository settings, create the `release` environment. Require
   reviewers if release approval is desired; the workflow does not hard-code
   an approval rule.
4. Add the NuGet profile username (not the account email) as the `NUGET_USER`
   secret for the `release` environment. Do not create or store a long-lived
   NuGet API key.
5. Confirm the repository's default branch and that the `Build` workflow
   succeeds there before the first release.

## Create a release

1. Merge release-ready changes to the default branch and confirm CI is green.
2. Select the next unused SemVer version. Use a prerelease suffix during early
   development, for example `0.1.0-alpha.1`, `0.1.0-alpha.2`,
   `0.1.0-beta.1`, then `0.1.0`.
3. Create and push the tag from the release commit:

   ```sh
   git tag v0.1.0-alpha.1
   git push origin v0.1.0-alpha.1
   ```

4. The tag triggers `.github/workflows/publish.yml`. It validates the tag,
   restores, builds, tests, packs and inspects all three NuGet packages before
   requesting an OIDC credential and publishing.
5. Verify the package versions and metadata on nuget.org, then create or
   verify the corresponding GitHub Release.

Publishing is tag-only. Pull requests and normal branch pushes never publish.
NuGet package versions are immutable: if a version already exists, the push
fails. Correct a mistaken release by incrementing the version and tagging a
new release; do not try to replace or overwrite the published package.

## Inspect packages locally

From a clean checkout with the pinned .NET SDK:

```sh
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet pack --configuration Release --no-build
```

Packages and symbols are written to `artifacts/`. Open each `.nupkg` or
`.snupkg` in NuGet Package Explorer, or inspect its ZIP contents. Confirm the
package ID and version, dependencies and `net10.0` target, embedded
`README.md`, MIT license metadata and `LICENSE`, repository URL and commit
metadata, and PDB files in the symbol package. Check for unneeded files,
local paths, test binaries, `.git` content, and secrets before distribution.
