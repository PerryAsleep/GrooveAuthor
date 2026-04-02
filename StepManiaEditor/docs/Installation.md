# Installation

`GrooveAuthor` requires [.Net Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

## Windows

`GrooveAuthor` requires Windows 10 or greater.

1. Download the latest `win-x64` `zip` file from the [Releases](https://github.com/PerryAsleep/GrooveAuthor/releases) page and extract it to a desired location.
2. To run `GrooveAuthor` open the `GrooveAuthor` folder and run `GrooveAuthor.exe`.

## Linux

1. Download the latest `linux-x64` `tar.gz` file from the [Releases](https://github.com/PerryAsleep/GrooveAuthor/releases) page and extract it.
2. Ensure you have [.Net Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed. If you encounter any errors installing see [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux) for detailed instructions.
3. Inside the extracted directory run the `grooveauthor-install.sh` script with `sudo`. The [grooveauthor-install.sh](../../StepManiaEditorLinux/install.sh) script will install `GrooveAuthor` to `/opt` and set up a desktop entry in `/usr/share/applications`
    ```
    sudo ./grooveauthor-install.sh
    ```
4. When complete you can delete the `tar.gz` file and the extracted contents.

### Arch Linux

> [!WARNING]
> The AUR is maintained by third parties, and packages may be out-of-date.

You can install via the AUR with `paru`
```sh
paru -S grooveauthor-bin
```
or `yay`
```sh
yay -S grooveauthor-bin
```
## MacOS

`GrooveAuthor` requires MacOS 11.0 or greater.

1. Download the latest `mac` `dmg` file for your architecture from the [Releases](https://github.com/PerryAsleep/GrooveAuthor/releases) page and run it. If your Mac has an Apple Silicon CPU you should use the `arm64` release. If your Mac has an Intel CPU you should use the `x64` release.
2. Ensure you have [.Net Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed. If you encounter any errors installing see [Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos) for detailed instructions.
3. Copy `GrooveAuthor.app` to the `/Applications` directory.
4. Run the following command to remove the quarantine that Apple places on unsigned applications.
    ```
    xattr -dr com.apple.quarantine /Applications/GrooveAuthor.app
    ```