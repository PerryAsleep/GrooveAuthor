# Building From Source

Building from source requires Windows 10 or greater and Microsoft Visual Studio Community 2022.

1. Clone the repository and init submodules.
	```
	git clone https://github.com/PerryAsleep/GrooveAuthor.git
	cd ./GrooveAuthor
	git submodule update --init --recursive
	```
2. Open `GrooveAuthor.sln` with Visual Studio.
3. Set `StepManiaEditor` as your Startup Project and build.

## Troubleshooting

### MonoGame Content Builder

If you experience errors building related to `mgcb-editor-windows` you may need to:
1. Delete the directories beginning with `dotnet-mgcb` in `%USERPROFILE%\.nuget\packages\`
2. In Visual Studio open the PowerShell through Tools > Command Line > Developer PowerShell.
3. Run `cd .\StepManiaEditor` then `dotnet tool restore`

### NuGet Packages

If you experience errors building related to `NuGet package restore failed` these checks may help you perform a [NuGet Package Restore](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore):
1. Ensure `Allow NuGet to download missing packages` and `Automatically check for missing packages during build in Visual Studio` are checked in Visual Studio from Tools > NuGet Package Manager > Package Manager Settings.
2. In Visual Studio open the PowerShell through Tools > Command Line > Developer PowerShell.
3. Run `dotnet restore`. Try building again. If that doesn't work continue with the steps below.
4. Clear your NuGet cache by running `dotnet nuget locals all --clear`
5. Run `dotnet restore` and try building again.