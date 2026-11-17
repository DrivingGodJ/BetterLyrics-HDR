# Install BetterLyrics HDR

[简体中文](INSTALL.HDR.md) | **English**

Download the Windows x64 `.msix` and `BetterLyrics-HDR-Publisher.cer` from the [release](https://github.com/DrivingGodJ/BetterLyrics-HDR/releases/latest). The package includes the .NET and Windows App SDK runtimes.

1. For your first installation, import the publisher certificate from that release into the local computer's **Trusted People** store. You can also run the following command in an elevated PowerShell session.
2. Double-click the `.msix`, let Windows finish installing it, then open **BetterLyrics HDR** from the Start menu.

```powershell
Import-Certificate -FilePath .\BetterLyrics-HDR-Publisher.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

This fork uses a self-signed certificate. Later versions signed with the same certificate can be installed directly as updates. BetterLyrics HDR has its own Windows app identity and data directory, so it can coexist with the upstream app.

## HDR lyrics

Open **Lyrics Window Manager → Lyrics Effects → Glow Effect → HDR lyrics glow**. The switch is disabled when HDR is off on the display assigned to the lyrics window. The **Windows HDR settings** link opens the system controls for your displays.

The moving front of ordinary timed lyrics can be up to 25% brighter than SDR reference white. A glowing front can reach the display's reported HDR peak. The letters behind it fade with the glow.

## Optional services

Use the app's plugin manager to install community plugins. Discord Rich Presence and Last.fm use your own service credentials. Set these as **user environment variables** in Windows and restart BetterLyrics HDR.

| Variable | Purpose |
| --- | --- |
| `BETTERLYRICS_DISCORD_APP_ID` | Discord application ID |
| `BETTERLYRICS_LASTFM_API_KEY` | Last.fm API key |
| `BETTERLYRICS_LASTFM_SHARED_SECRET` | Last.fm shared secret |

After configuring both Last.fm credentials, authorize Last.fm within BetterLyrics HDR. The app stores the authorized session in Windows Credential Manager.

## Build from source

Install the .NET 10 SDK on Windows x64. Clone into a short path and run these commands from the repository root.

```powershell
$project = 'src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3/BetterLyrics.WinUI3.csproj'
dotnet restore $project -p:Platform=x64 -r win-x64 -p:SelfContained=true --source https://api.nuget.org/v3/index.json --source https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json
dotnet build $project --no-restore -c Release -p:Platform=x64 -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false
```

Use [New-ReleasePackage.ps1](../scripts/New-ReleasePackage.ps1) to package the resulting `bin/x64/Release/net10.0-windows10.0.26100.0/win-x64` directory as an MSIX. Pass the Windows SDK directory that contains `makeappx.exe` and `signtool.exe`, and the thumbprint of a code-signing certificate in your Personal store. Its Subject must match the Publisher in [Package.appxmanifest](<../src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3 (Package)/Package.appxmanifest>).
