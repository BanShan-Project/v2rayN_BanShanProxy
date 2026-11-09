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

## Upstream / 上游

Based on [2dust/v2rayN](https://github.com/2dust/v2rayN), GPL-3.0. The original project and
documentation remain the reference for the proxy client itself.
