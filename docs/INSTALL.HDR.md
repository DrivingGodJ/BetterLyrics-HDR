# 安装 BetterLyrics HDR

**简体中文** | [English](INSTALL.HDR.en.md)

从 [Release](https://github.com/DrivingGodJ/BetterLyrics-HDR/releases/latest) 下载 Windows x64 的 `.msix` 和 `BetterLyrics-HDR-Publisher.cer`。安装包自带 .NET 和 Windows App SDK 运行时。

1. 首次安装时，将该 Release 的发布者证书导入本地计算机的**受信任的人**证书库。也可以在管理员 PowerShell 中运行下列命令。
2. 双击 `.msix`，等待 Windows 完成安装，然后从开始菜单打开 **BetterLyrics HDR**。

```powershell
Import-Certificate -FilePath .\BetterLyrics-HDR-Publisher.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

证书由该分支自签名。后续使用同一证书签名的版本可直接安装更新。BetterLyrics HDR 使用独立应用标识和数据目录，可以与上游版本并存。

## HDR 歌词

打开**歌词窗口管理 → 歌词效果 → 辉光效果 → HDR 歌词发光**。歌词窗口所在显示器关闭 HDR 时，开关呈灰色。

逐字歌词的普通前沿最高比原字色亮 50%。白色辉光的前端可达到显示器报告的 HDR 峰值，扫过的字母跟随辉光逐渐暗下去。前沿和外侧光晕均跟随歌词颜色。

## 可选服务

社区插件可以继续通过应用内插件管理安装。Discord Rich Presence 和 Last.fm 使用你自己的服务凭据。在 Windows 的**用户环境变量**中设置以下变量，然后重启 BetterLyrics HDR。

| 变量 | 用途 |
| --- | --- |
| `BETTERLYRICS_DISCORD_APP_ID` | Discord 应用 ID |
| `BETTERLYRICS_LASTFM_API_KEY` | Last.fm API Key |
| `BETTERLYRICS_LASTFM_SHARED_SECRET` | Last.fm Shared Secret |

设置好 Last.fm 的两项凭据后，可以在 BetterLyrics HDR 中授权 Last.fm。应用把授权会话保存在 Windows 凭据管理器。

## 从源码构建

在 Windows x64 上安装 .NET 10 SDK。将仓库放在较短的路径下，从仓库根目录运行。

```powershell
$project = 'src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3/BetterLyrics.WinUI3.csproj'
dotnet restore $project -p:Platform=x64 -r win-x64 -p:SelfContained=true --source https://api.nuget.org/v3/index.json --source https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json
dotnet build $project --no-restore -c Release -p:Platform=x64 -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false
```

使用 [New-ReleasePackage.ps1](../scripts/New-ReleasePackage.ps1) 将上一步生成的 `bin/x64/Release/net10.0-windows10.0.26100.0/win-x64` 封装为 MSIX。传入包含 `makeappx.exe` 和 `signtool.exe` 的 Windows SDK 目录，以及个人证书库中的代码签名证书指纹。证书的 Subject 必须和 [Package.appxmanifest](<../src/BetterLyrics.DotNet/BetterLyrics.WinUI3/BetterLyrics.WinUI3 (Package)/Package.appxmanifest>) 中的 Publisher 匹配。
