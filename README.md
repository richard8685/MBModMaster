# MBModMaster

MBModMaster是一个面向《Mount & Blade II: Bannerlord》的轻量级Mod管理器，用于扫描游戏Mod、调整加载顺序、保存启动器配置，并辅助安装压缩包形式的Mod。

## 功能

- 自动定位Bannerlord游戏目录。
- 扫描`Modules`目录中的Mod信息。
- 根据依赖关系和基础组件规则排序加载顺序。
- 保存单人模式启动器配置。
- 支持拖拽调整Mod顺序。
- 支持从压缩包安装Mod。

## 环境要求

- Windows 10/11
- .NET 10 SDK
- Mount & Blade II: Bannerlord

## 项目结构

```text
src/MBModMaster/                 WPF桌面应用
tests/MBModMaster.SmokeTests/    冒烟测试
```

## 构建

```powershell
dotnet build
```

## 生成便携版

```powershell
powershell -ExecutionPolicy Bypass -File tools/publish-portable.ps1
```

便携版会输出到`artifacts/portable/MBModMaster-win-x64/`，同时生成`artifacts/portable/MBModMaster-win-x64.zip`。该版本为self-contained发布，用户不需要额外安装.NET运行时。

## 运行测试

```powershell
dotnet run --project tests/MBModMaster.SmokeTests
```

## 说明

这是个人开发中的项目，功能和界面仍可能调整。使用前建议备份Bannerlord启动器配置文件。
