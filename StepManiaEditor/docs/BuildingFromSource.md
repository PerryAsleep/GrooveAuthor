# Building From Source

## Windows

Building from source requires Windows 10 or greater and Microsoft Visual Studio Community 2022.

1. Clone the repository and init submodules.
	```
	git clone https://github.com/PerryAsleep/GrooveAuthor.git
	cd ./GrooveAuthor
	git submodule update --init --recursive
	```
2. Open `GrooveAuthor.sln` with Visual Studio.
3. Set `StepManiaEditorWindows` as your Startup Project and build.

### Troubleshooting

#### MonoGame Content Builder

If you experience errors building related to `mgcb-editor-windows` you may need to:
1. Delete the directories beginning with `dotnet-mgcb` in `%USERPROFILE%\.nuget\packages\`
2. In Visual Studio open the PowerShell through Tools > Command Line > Developer PowerShell.
3. Run `cd .\StepManiaEditor` then `dotnet tool restore`

#### NuGet Packages

If you experience errors building related to `NuGet package restore failed` these checks may help you perform a [NuGet Package Restore](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore):
1. Ensure `Allow NuGet to download missing packages` and `Automatically check for missing packages during build in Visual Studio` are checked in Visual Studio from Tools > NuGet Package Manager > Package Manager Settings.
2. In Visual Studio open the PowerShell through Tools > Command Line > Developer PowerShell.
3. Run `dotnet restore`. Try building again. If that doesn't work continue with the steps below.
4. Clear your NuGet cache by running `dotnet nuget locals all --clear`
5. Run `dotnet restore` and try building again.

## Linux

> [!NOTE]
>The instructions below assume Ubuntu and VSCode. You can likely use different distros and development tools but they aren't officially supported.

1. Follow the instructions from MonoGame for [setting up a Linux development environment](https://docs.monogame.net/articles/getting_started/1_setting_up_your_os_for_development_ubuntu.html).
2. Clone the repository and init submodules.
	```
	git clone https://github.com/PerryAsleep/GrooveAuthor.git
	cd ./GrooveAuthor
	git submodule update --init --recursive
	```
3. Open the `GrooveAuthor` directory in VSCode.
4. Run `Linux Debug` or `Linux Release`.

## Mac OS

Building from source requires MacOS 11.0 or greater.

> [!NOTE]
>The instructions below assume VSCode. You can likely use different development tools but they aren't officially supported.

1. Follow the instructions from MonoGame for [setting up a Mac OS development environment](https://docs.monogame.net/articles/getting_started/1_setting_up_your_os_for_development_macos.html).
    1. Note that `GrooveAuthor` does not compile MonoGame effects when building for MacOS. Effects are compiled for OpenGL on Windows and checked in to source control due to issues running MGCB tools on MacOS. As such you do not need to follow the `Setup Wine For Effect Compilation` steps.
2. If you do not have `codesign` installed, install it. The simplest method is likely to install XCode through the App Store.
3. Clone the repository and init submodules.
	```
	git clone https://github.com/PerryAsleep/GrooveAuthor.git
	cd ./GrooveAuthor
	git submodule update --init --recursive
	```
4. Open the `GrooveAuthor` directory in VSCode.
5. Run any of the `MacOS` configurations appropriate for your architecture.