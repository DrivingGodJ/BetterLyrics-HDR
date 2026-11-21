[简体中文](INSTALL.LITE.md) | **English**

# Install BetterLyrics Lite

Download the Windows x64 `.msix` installer and the publisher certificate, `BetterLyrics-HDR-Publisher.cer`, from the [latest release](https://github.com/DrivingGodJ/BetterLyrics-HDR/releases/latest). The installer includes the .NET and Windows App SDK runtimes.

1. For a first-time install, import the publisher certificate from the release into **Local Machine → Trusted People**. You can also run the command below in an elevated PowerShell window. Skip this step if the same certificate is already installed.
2. Double-click the `.msix` installer, then open **BetterLyrics Lite** from the Start menu.

```powershell
Import-Certificate -FilePath .\BetterLyrics-HDR-Publisher.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Install a newer Lite package over an existing Lite installation to update it. The previous BetterLyrics HDR edition has its own app identity and data directory and can coexist with Lite.

## Build from source

Install the .NET 10 SDK on Windows x64. Clone into a short path and run these commands from the repository root.

```powershell
$project = 'src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3/BetterLyrics.WinUI3.csproj'
dotnet restore $project -p:Platform=x64 -r win-x64 -p:SelfContained=true --source https://api.nuget.org/v3/index.json --source https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json
dotnet build $project --no-restore -c Release -p:Platform=x64 -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false
```

Use [New-ReleasePackage.ps1](../scripts/New-ReleasePackage.ps1) to package the resulting `bin/x64/Release/net10.0-windows10.0.26100.0/win-x64` directory as an MSIX. Pass the Windows SDK directory that contains `makeappx.exe` and `signtool.exe`, and the thumbprint of a code-signing certificate in your Personal store. Its Subject must match the Publisher in [Package.appxmanifest](<../src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3 (Package)/Package.appxmanifest>).
