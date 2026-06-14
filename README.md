# MBModMaster

MBModMaster是一个面向《Mount & Blade II: Bannerlord》的轻量级Mod管理器，用于扫描游戏Mod、调整加载顺序、保存启动器配置，并辅助安装压缩包形式的Mod。

## 下载与使用

普通用户建议下载便携版压缩包：

```text
MBModMaster-win-x64.zip
```

使用方式：

1. 下载`MBModMaster-win-x64.zip`。
2. 解压到任意文件夹。
3. 双击`MBModMaster.exe`启动。

便携版是self-contained发布，已经内置运行所需的.NET组件。新电脑无需额外安装.NET运行时。

## 使用前提

- Windows 10/11 x64。
- 已安装《Mount & Blade II: Bannerlord》。
- 建议首次使用前备份Bannerlord启动器配置文件。

如果Windows提示文件来自互联网或SmartScreen拦截，可以在文件属性中解除锁定，或在SmartScreen界面选择「更多信息」后继续运行。

## 功能

- 自动定位Bannerlord游戏目录。
- 扫描`Modules`目录中的Mod信息。
- 根据依赖关系和基础组件规则排序加载顺序。
- 保存单人模式启动器配置。
- 支持拖拽调整Mod顺序。
- 支持从压缩包安装Mod。

## 开发环境

- Windows 10/11
- .NET 10 SDK
- Mount & Blade II: Bannerlord

## 项目结构

```text
src/MBModMaster/                 WPF桌面应用
tests/MBModMaster.SmokeTests/    冒烟测试
tools/                           发布脚本
```

## 构建

```powershell
dotnet build
```

## 运行测试

```powershell
dotnet run --project tests/MBModMaster.SmokeTests
```

## 生成便携版

```powershell
powershell -ExecutionPolicy Bypass -File tools/publish-portable.ps1
```

生成结果：

```text
artifacts/portable/MBModMaster-win-x64/
artifacts/portable/MBModMaster-win-x64.zip
```

`artifacts/`目录是本地发布产物目录，不会提交到源码仓库。

## 说明

这是个人开发中的项目，功能和界面仍可能调整。程序会读取和写入Bannerlord官方启动器配置，请在调整加载顺序前做好备份。
