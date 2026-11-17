param(
    [Parameter(Mandatory)][string]$BuildOutput,
    [Parameter(Mandatory)][string]$StagingDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$SdkBin,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [ValidateSet('x64', 'x86', 'arm64')][string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\BetterLyrics.DotNet\BetterLyrics.WinUI3\BetterLyrics.WinUI3 (Package)'
$build = (Resolve-Path -LiteralPath $BuildOutput).Path
$sdk = (Resolve-Path -LiteralPath $SdkBin).Path
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"

[xml]$manifest = Get-Content -Raw -LiteralPath (Join-Path $project 'Package.appxmanifest')
if ($certificate.Subject -ne $manifest.Package.Identity.Publisher -or !$certificate.HasPrivateKey) {
    throw 'The signing certificate must have a private key and match the publisher in Package.appxmanifest.'
}
$manifest.Package.Identity.SetAttribute('ProcessorArchitecture', $Architecture)
$manifest.Package.Applications.Application.Executable = 'BetterLyrics.WinUI3.exe'
$manifest.Package.Applications.Application.EntryPoint = 'Windows.FullTrustApplication'

if (Test-Path -LiteralPath $StagingDirectory) { throw 'Choose a new staging directory.' }
New-Item -ItemType Directory -Path $StagingDirectory, $OutputDirectory -Force | Out-Null
$staging = (Resolve-Path -LiteralPath $StagingDirectory).Path
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path

& robocopy.exe $build $staging /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XF '*.pdb' '*.lib' '*.exp' 'AppxManifest.xml'
if ($LASTEXITCODE -ge 8) { throw "Could not stage the build. Robocopy exit code $LASTEXITCODE." }
Copy-Item -LiteralPath (Join-Path $staging 'BetterLyrics.WinUI3.pri') -Destination (Join-Path $staging 'resources.pri') -Force
Copy-Item -LiteralPath (Join-Path $project 'Images') -Destination $staging -Recurse
foreach ($name in 'StoreLogo','Square150x150Logo','Square44x44Logo','Wide310x150Logo','SmallTile','LargeTile') {
    Copy-Item -LiteralPath (Join-Path $staging "Images\$name.scale-100.png") -Destination (Join-Path $staging "Images\$name.png")
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\LICENSE') -Destination $staging
$manifest.Save((Join-Path $staging 'AppxManifest.xml'))

$package = Join-Path $output ('BetterLyrics-HDR-{0}-{1}.msix' -f $manifest.Package.Identity.Version, $Architecture)
& (Join-Path $sdk 'makeappx.exe') pack /d $staging /p $package /o
if ($LASTEXITCODE) { throw "MakeAppx failed with exit code $LASTEXITCODE." }
& (Join-Path $sdk 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint $package
if ($LASTEXITCODE) { throw "SignTool failed with exit code $LASTEXITCODE." }
Export-Certificate -Cert $certificate -FilePath (Join-Path $output 'BetterLyrics-HDR-Publisher.cer') -Force | Out-Null
Get-FileHash -Algorithm SHA256 -LiteralPath $package
