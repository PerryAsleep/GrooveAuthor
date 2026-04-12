# Packaging Builds

> [!NOTE]
>Packaging builds should only be done as part of preparing a new release.

1. Prior to packaging builds, ensure the version has been updated and committed. See [Updating Version](UpdatingVersion.md).
2. Push a new version tag of the form `v<semantic version>`, e.g. `v1.0.0`.
3. The `Build Release` github workflow should trigger automatically and upload artifacts for all platforms to a new draft release.