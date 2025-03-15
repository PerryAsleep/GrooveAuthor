# Packaging Builds

> [!NOTE]
>Packaging builds should only be done as part of preparing a new release.

1. Prior to packaging builds, ensure the version has been updated and committed. See [Updating Version](UpdatingVersion.md).
2. Ensure a tag exists for the new release.
3. Ensure a Windows machine and a Mac machine are synced to the commit for the release tag.
4. On a Windows machine run the `PackageBuild` project. This will package Windows and Linux builds to the `Releases` directory. The `PackageBuild` project assumes [WSL](https://learn.microsoft.com/en-us/windows/wsl/) and [7-Zip](https://www.7-zip.org/) are installed.
5. On a Mac machine run `sudo package-build.sh` under `StepManiaEditorMacOS`. This will package a MacOS build to the `Releases` directory. This script assumes XCode command line tools are installed.