# 👀 VRCFaceTracking

Provides eye tracking and lip tracking in VRChat by providing a bridge between your tracking hardware and VRChat's OSC server.
## [Get started here!](https://docs.vrcft.io/docs/intro/getting-started)

[![Discord](https://discord.com/api/guilds/849300336128032789/widget.png)](https://discord.com/invite/vrcft)

## 🎥 Demo

[![](https://i.imgur.com/iQkw12C.jpg)](https://youtu.be/ZTVnh8aaf9U)

## 🛠 Avatar Setup

For this app to work, you'll need to be using an avatar with the correct parameters or an avatar config file with the correct mappings. The system is designed to control your avatar's eyes and lips via simple blend states but what the parameters control is completely up to you.

### [List of Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/)

## 👀 [Eye Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#eye-tracking-parameters)

### [Eye Tracking Setup Guide](https://github.com/benaclejames/VRCFaceTracking/wiki/Eye-Tracking-Setup)

It's not required to use all of these parameters. In fact, you don't need to use any of them if you intend on using VRChat's built-in eye tracking system. Similar to the setup of parameters with Unity Animation Controllers, these are all case-sensitive and must be copied **EXACTLY** as shown into your Avatar's base parameters. A typical setup might look something like this:<br>
![](https://i.imgur.com/kfJD1Bl.png)

We strongly encourage you to [consult the docs](https://docs.vrcft.io) for a setup guide and more info as to what each parameter does

## :lips: [Lip and Face Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#expression-tracking-parameters)

There are a large number of parameters you can use for lip and face tracking. 

### [Combined Lip Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#addtional-simplified-tracking-parameters) - Combined parameters to group mutually exclusive face shapes.

## 🔨 Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)
- A desktop environment on Linux, or Windows 10/11 for the Windows build

On Windows, Visual Studio 2022 or newer with the **.NET desktop development** workload can also be used. You can verify that the required SDK is installed with:

```console
dotnet --list-sdks
```

The output should include a `10.0.x` SDK.

### Clone the repository

```console
git clone https://github.com/benaclejames/VRCFaceTracking.git
cd VRCFaceTracking
```

### Restore and build

Restore all NuGet dependencies, then build the complete solution:

```console
dotnet restore VRCFaceTracking.sln
dotnet build VRCFaceTracking.sln --configuration Debug
```

The build produces the main application, its core library, SDK, and module process. A successful build is also a good check before opening a pull request.

To build only the desktop application:

```console
dotnet build VRCFaceTracking/VRCFaceTracking.csproj --configuration Debug
```

### Run locally

Run the application directly from the source tree:

```console
dotnet run --project VRCFaceTracking/VRCFaceTracking.csproj --configuration Debug
```

The process started by `dotnet run` uses native libraries for the host platform and architecture. The project currently includes native libraries for Windows x64, Linux x64 and ARM64, and macOS for the core components.

### Publish a Release build

Both the main application and the module process must be published together. The following commands match the platform builds used by the repository's publish workflow.

#### Windows x64

Run these commands from an x64 or ARM64 Windows development machine:

```console
dotnet publish VRCFaceTracking/VRCFaceTracking.csproj --configuration Release --runtime win-x64 --output ./publish/windows
dotnet publish VRCFaceTracking.ModuleProcess/VRCFaceTracking.ModuleProcess.csproj --configuration Release --runtime win-x64 --output ./publish/windows
```

#### Linux x64

```console
dotnet publish VRCFaceTracking/VRCFaceTracking.csproj --configuration Release --runtime linux-x64 --output ./publish/linux
dotnet publish VRCFaceTracking.ModuleProcess/VRCFaceTracking.ModuleProcess.csproj --configuration Release --runtime linux-x64 --output ./publish/linux
```

#### Linux ARM64

```console
dotnet publish VRCFaceTracking/VRCFaceTracking.csproj --configuration Release --runtime linux-arm64 --output ./publish/linux-arm64
dotnet publish VRCFaceTracking.ModuleProcess/VRCFaceTracking.ModuleProcess.csproj --configuration Release --runtime linux-arm64 --output ./publish/linux-arm64
```

The generated executables and native dependencies are placed in the specified `publish/<platform>` directory. `publish/` is intended as a build output directory and should not be committed.

### Clean build output

Remove generated build and publish artifacts:

```console
dotnet clean VRCFaceTracking.sln
```

To remove the publish directories as well:

```console
rm -rf publish
```

On Windows, use `Remove-Item -Recurse -Force publish` instead.

### Troubleshooting

- **Missing or unsupported SDK:** Install the latest .NET 10 SDK and confirm `dotnet --list-sdks` reports `10.0.x`.
- **Restore or package failures:** Check your network and NuGet configuration, then retry with `dotnet restore VRCFaceTracking.sln --force --no-cache`.
- **Native library or runtime errors:** Publish the application for the same operating system and architecture on which it will run.
- **Visual Studio cannot load the solution:** Update Visual Studio and install or update the .NET desktop development workload, then reopen the solution.

## ⛓ External Modules

Use the module registry to download addons and add support for your hardware!
