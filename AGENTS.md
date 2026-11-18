# AGENTS.md

这是 v2rayN 的一个 Windows 分支，只留了 Avalonia 版，另外接了半山代理的账号登录。上游 2dust/v2rayN 更新很勤，所以这个仓库的日常就是隔一阵把上游合进来，同时别把自己那点改动弄丢。这份笔记是踩过的坑攒出来的，改代码前值得看一眼。

## 目录长什么样

`v2rayN/` 下面是整个 .NET 解决方案。`ServiceLib` 是共用逻辑和 ViewModel，桌面和测试都靠它；`v2rayN.Desktop` 是 Avalonia 界面；`AmazTool` 是升级器，跑起来会把自己和程序一起换掉；`ServiceLib.Tests` 用 TUnit；`GlobalHotKeys` 是 submodule，构建前得拉下来，不然编译直接失败。

WPF 版（`v2rayN/v2rayN`）和 WebAPI 版（`v2rayN/v2rayN.WebAPI`）已经从仓库里删掉了。上游还在维护它们，所以每次合并都会冒出一堆冲突，这是正常的。

`.github/workflows` 只剩两个文件：`release-windows.yml` 管发版，`test.yml` 跑单测。

## 怎么构建

要 .NET 10 SDK，命令没别的花样：

```powershell
dotnet build .\v2rayN\v2rayN.slnx -c Release
dotnet test .\v2rayN\ServiceLib.Tests\ServiceLib.Tests.csproj -c Release
```

发布分两次，AmazTool 带裁剪，主程序不带：

```powershell
dotnet publish .\v2rayN\AmazTool\AmazTool.csproj -c Release -r win-x64 -p:SelfContained=true -p:PublishTrimmed=true -o out/win-x64
dotnet publish .\v2rayN\v2rayN.Desktop\v2rayN.Desktop.csproj -c Release -r win-x64 -p:SelfContained=true -o out/win-x64
```

编译会出六十来个警告，基本都是 `RXUIBIND010`，上游就有，不用管。`out/` 和 `TestResults/` 别提交。

## 几个碰了会出事的点

**包结构**是最容易翻车的地方。zip 里所有文件必须装在同一个顶层目录 `v2rayN-windows-64/` 下面，exe、dll、bin 全在里面，根目录不许有散文件。

原因是 AmazTool 升级时按 `entry.FullName.Split('/')` 切开、扔掉第一段、剩下的拼成目标路径，而且长度只有 1 的条目直接 `continue` 跳过。所以把 exe 放在 zip 根上，用户点更新之后主程序根本不动，只有 bin 会被刷一遍；手工解压的话 exe 和 `bin/` 还分处两级，程序找不到核心，打开就是没有节点的样子。

判断方法很土但管用：解开包，根目录下应该只有一个 `v2rayN-windows-64`，别的什么都没有。

**包名要发两份**。`UpdateService` 里有段判断：exe 旁边有 `libHarfBuzzSharp.dll` 就认为是 Avalonia 版，把下载地址里的 `.zip` 换成 `-desktop.zip`。只发普通名字的话，用户点更新就是 404。所以 x64 和 arm64 各发两个文件，内容完全一样。

**版本号**从 tag 来。workflow 里用 `-p:Version=<tag 去掉 v>`，不然程序里写死 7.25.6，你发 7.25.7 用户每次启动都被提示更新，更新完还是 7.25.6，无限循环。

四段号（7.25.6.1）也能用。比较是 major.minor.patch.revision 一路比下去，第四段只在同一个 patch 里往上加：发了 7.25.7 之后就别再回头发 7.25.6.x，客户端会当成旧版本，不提示。程序集版本每段最大 65534。

**字体**这块上游改了机制但没改完，是个隐藏的坑。`GlobalStyles.axaml` 里现在有这么一条，几乎把所有控件都框进去了：

```xml
<Setter Property="TextElement.FontSize" Value="{DynamicResource AppFontSize}" />
<Setter Property="TextElement.FontFamily" Value="{DynamicResource AppFontFamily}" />
```

而 `AppFontSize` 只在 `ThemeSettingViewModel` 里写，条件是配置里的 `CurrentFontSize >= 8`。全新安装这个值是 0，资源压根没写进去，Setter 解析不出来，字号就掉到 Avalonia 默认的 12；Semi 主题本来是 14，所以字看着变小了。

现在 `App.axaml` 里给了默认值 14 和行高 27（行高大约等于 `20 + 字号/2`），`ThemeSettingViewModel` 在没配置时也会写 14。改字号的话这两个数要一起动。

还有个坑：别把 `AppFontFamily` 写成字符串资源。`DynamicResource` 不做类型转换，`<x:String>` 塞进去，控件要 FontFamily 的时候直接抛 `InvalidCastException`，程序启动就崩，日志里只有一句 `Arg_InvalidCastException`，很难查。字体族交给 `FontManagerOptions` 的 `DefaultFamilyName`（Noto Sans SC）就行。

**半山的订阅**是 fork 唯一的功能差异，保护逻辑一条都不能掉。固定 `SubId = "banshan"`，订阅在列表里隐藏，节点不能编辑、删除、复制、导出、分享、排序、移动分组；备份的时候从临时副本里剥掉，恢复完再剥一次。

这些判断散在 `ConfigDbHandler`（`AddSubItem` / `DeleteSubItem` / `MoveToGroup`）、`ProfilesViewModel` 二十来处、`SubEditViewModel`、`SubSettingViewModel`、`BackupAndRestoreViewModel` 里。合并上游后最好数一下 `BanShanHandler` 的出现次数，掉了就是合丢了。

## 怎么合上游

```powershell
git fetch upstream master
git merge upstream/master
```

冲突基本是两类。一类是 `v2rayN/v2rayN` 和 `v2rayN/v2rayN.WebAPI` 下面被我们删掉的文件，上游改了它们，直接 `git rm -rf` 保持删除。另一类是我们改过的函数和上游重构撞上，手动合，把 BanShan 的判断接到新的 API 后面。

上游重构 ServiceLib 很频繁，最近一轮的映射关系记一下：

```
AppManager.Instance.GetSubItem / SubItems / ProfileItems  →  ConfigDbQuery 的同名方法
AppManager.Instance.Config                                →  ConfigTextHandler.Config（可写）
                                                             ConfigState.Instance.Current（只读）
ConfigHandler.AddSubItem(config, item)                     →  ConfigDbHandler.AddSubItem(item)
ConfigHandler.DeleteSubItem(config, id)                    →  ConfigDbHandler.DeleteSubItem(id)
ConfigHandler.CopyServer(config, list)                     →  ConfigDbHandler.CopyServer(list)
```

合完看一眼 `git diff upstream/master --stat -- v2rayN/ServiceLib v2rayN/v2rayN.Desktop`，如果只剩 BanShan 相关那十几个文件就对了。

顺带说一句，merge 那个提交本身可能编不过，因为冲突解决和 API 适配分在了两个提交里。要 bisect 的话别拿它当判定点，以 HEAD 为准。

## 写代码的习惯

界面文案是中文的直接写在代码里，不用往 `ResUI` 里加新键，上游自己的继续用 `ResUI.*`。

`GlobalUsings` 里已经有 `ServiceLib.Handler.Data`、`SQLite`、`System.Text.Json.Nodes`、`ReactiveUI` 那些，新文件一般不用再加 using。

ViewModel 属性用源生成器写：`[Reactive] public partial string Xxx { get; set; }`。

测试是 TUnit，断言用 `BeEqualTo`，不是 `Be`：

```csharp
[Test]
[Arguments("7.25.6", "v7.25.6.1", true)]
public async Task SomeTest(string a, string b, bool c)
{
    await value.Should().BeEqualTo(expected);
    await flag.Should().BeTrue();
}
```

文件存 UTF-8 无 BOM，提交信息写中文。

## 发版

推 tag 就会触发 `release-windows.yml`：

```powershell
git push origin master
git push origin v7.25.6.1
```

流程大概是：两个架构各 publish 一遍，删掉 pdb/debug/sym，下载 2dust/v2rayN-core-bin 的包当底座，把产物塞进 `v2rayN-windows-<arch>` 目录用 7-Zip 合并进去，断言所有条目都在这个目录下，复制一份成 `-desktop`，最后上传四个文件。

别用 `System.IO.Compression` 的 `ZipArchive.Update` 去改 core-bin 那个包，会报"本地文件头已损坏"，试过。7-Zip 稳。

## 改完自己先验一遍

build 零错误；test 全绿（现在 290 个用例）；publish 出来的 exe 版本跟 tag 一致；包根目录没有散文件；启动能看到主窗口，"配置项"三个字的渲染高度约 15px（13px 说明掉回 12 号了）；最后拿 AmazTool 对着包跑一遍，exe 被换掉、没生成嵌套目录。

## 其他零碎

这台机器上不一定有全局的 git 和 .NET SDK，我一般用便携版 MinGit 和 SDK zip 凑合。真要用 `dotnet`，把 SDK 目录加进 PATH 就行。

AmazTool 升级时 `bin` 里已存在的文件不会覆盖，这是上游故意的，别当漏打包去改。

`guiNDB.db` 和 `guiNConfig.json` 这两个名字在 `BanShanHandler` 里是写死的，上游哪天改名要跟着改。

登录用的是自己 new 的 `HttpClient`，`UseProxy = false`，直连 `proxy.yinshaoze.asia`。要是那个域名在某些网络下连不上，得改成走系统代理。
