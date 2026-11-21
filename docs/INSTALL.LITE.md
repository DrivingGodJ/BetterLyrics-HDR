**简体中文** | [English](INSTALL.LITE.en.md)

# 安装 BetterLyrics Lite

从 [最新 Release](https://github.com/DrivingGodJ/BetterLyrics-HDR/releases/latest) 下载 Windows x64 的 `.msix` 安装包和发布者证书 `BetterLyrics-HDR-Publisher.cer`。安装包已包含 .NET 和 Windows App SDK 运行时。

1. 首次安装时，将 Release 提供的发布者证书导入**本地计算机 → 受信任的人**。也可在管理员 PowerShell 中运行下方命令。已安装同一张证书时可跳过这步。
2. 双击 `.msix` 安装，从开始菜单打开 **BetterLyrics Lite**。

```powershell
Import-Certificate -FilePath .\BetterLyrics-HDR-Publisher.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Lite 新版可直接覆盖安装旧版 Lite。旧版 BetterLyrics HDR 使用独立的应用标识和数据目录，可与 Lite 并存。

## 从源码构建

在 Windows x64 上安装 .NET 10 SDK。将仓库放在较短的路径下，从仓库根目录运行。

```powershell
$project = 'src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3/BetterLyrics.WinUI3.csproj'
dotnet restore $project -p:Platform=x64 -r win-x64 -p:SelfContained=true --source https://api.nuget.org/v3/index.json --source https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json
dotnet build $project --no-restore -c Release -p:Platform=x64 -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false
```

使用 [New-ReleasePackage.ps1](../scripts/New-ReleasePackage.ps1) 将上一步生成的 `bin/x64/Release/net10.0-windows10.0.26100.0/win-x64` 封装为 MSIX。传入包含 `makeappx.exe` 和 `signtool.exe` 的 Windows SDK 目录，以及个人证书库中的代码签名证书指纹。证书的 Subject 必须和 [Package.appxmanifest](<../src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3 (Package)/Package.appxmanifest>) 中的 Publisher 匹配。
