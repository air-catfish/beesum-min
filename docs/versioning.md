# Versioning

The project follows [Semantic Versioning 2.0.0](https://semver.org/).

## Before 1.0

`0.x` is explicitly unstable: a minor bump (`0.1` → `0.2`) may contain breaking
changes to the public API. Patch releases (`0.1.1`) are bug fixes only.

Pin an exact version if you build on `Beesum.Core` before 1.0:

```xml
<PackageReference Include="Beesum.Core" Version="[0.2.0]" />
```

## After 1.0

- Breaking changes only in a major version.
- A public API is deprecated for at least one minor release before removal.
- Deprecations use `[Obsolete]` with a migration hint.

## Packages

`Beesum.Core`, `Beesum.Testing`, and the `beesum-min` tool are versioned
together. A tool release may lag a library release only if the CLI did not change.

Pre-release versions use a suffix (`0.2.0-rc.1`) and are not picked up by default
by NuGet ranges.

## Support

Only the latest minor of the current major gets security fixes. There are no
maintained LTS branches before 1.0.

## Release cadence

No fixed schedule. Patches when needed, minors when a feature set is coherent and
tested. Release notes live in `docs/release-notes/`.
