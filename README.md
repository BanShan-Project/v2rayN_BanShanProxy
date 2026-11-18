# 半山代理 v2rayN (Windows / Avalonia)

Windows-only fork of [v2rayN](https://github.com/2dust/v2rayN), using the Avalonia UI project only.
It adds the BanShan account login and a protected account subscription.

## Download / 下载

[https://github.com/BanShan-Project/v2rayN_BanShanProxy/releases](https://github.com/BanShan-Project/v2rayN_BanShanProxy/releases)

## Platform / 平台

| Platform | x64 | arm64 |
| --- | --- | --- |
| Windows 10/11 | ✅ | ✅ |

Only the Avalonia UI is kept. The WPF UI, WebAPI, Linux/macOS packaging and release-signing
workflows were removed from this fork.

## Build / 构建

Requires the .NET 10 SDK on Windows:

```powershell
dotnet publish .\v2rayN\v2rayN.Desktop\v2rayN.Desktop.csproj -c Release -r win-x64 -p:SelfContained=true
```

The release workflow builds Windows x64 and arm64 packages and uploads them directly to
GitHub Releases. It does not sign or verify package signatures.

## Release / 发布

Push a tag such as `v7.25.7`, or run the `Release Windows (Avalonia)` workflow with `tag=v7.25.7`.
The tag doubles as the application version: the workflow stamps it with `-p:Version=` so that the
built `v2rayN.exe` reports the same version the updater compares against.

Package layout matters. `AmazTool` (the in-app updater) drops the first path segment of every zip
entry and skips entries that have no directory, so the archive must contain exactly one top level
folder:

```
v2rayN-windows-64/
  v2rayN.exe
  AmazTool.exe
  bin/
    xray/  sing_box/  mihomo/  geoip.dat  ...
```

`ServiceLib/Services/UpdateService.cs` requests `v2rayN-windows-64-desktop.zip` when it detects the
Avalonia build (`libHarfBuzzSharp.dll` next to the exe), so the workflow publishes both
`v2rayN-windows-64.zip` and `v2rayN-windows-64-desktop.zip` (identical content, plus the arm64 pair).

## Upstream / 上游

Based on [2dust/v2rayN](https://github.com/2dust/v2rayN), GPL-3.0. The original project and
documentation remain the reference for the proxy client itself.
