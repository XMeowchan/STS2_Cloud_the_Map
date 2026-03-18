# STS2 Mod Template

这是一个为《Slay the Spire 2》准备的通用 Mod 模板工作区。

它基于原项目的构建、部署、安装器和发版流程整理而来，但已经移除了所有卡牌数据相关逻辑，包括：

- 小黑盒采集器
- 数据同步器
- 卡牌统计数据文件
- 遥测服务
- GitHub Pages 数据发布
- 卡牌悬浮窗 / 排序 / 详情面板逻辑
- 自动拉远程数据、自动更新等项目定制逻辑

现在模板只保留一套最小但完整的 STS2 Mod 开发骨架：

- Mod 初始化入口
- 本地 `config.json` 加载
- Harmony Patch 装配入口
- 可选的“从联机 Mod 列表隐藏自己”补丁
- 本地部署脚本
- 便携包与安装器打包脚本
- GitHub Release 发布脚本

## 开始前先改什么

建议先编辑 `mod_manifest.json`：

- `id`: Mod ID，建议使用 ASCII、无空格
- `name`: 展示名称
- `author`: 作者名
- `description`: 中英描述
- `version`: 当前版本号
- `has_pck` / `has_dll`: 声明是否携带 `.pck` / `.dll`
- `affects_gameplay`: 是否参与联机玩法一致性校验
- `dependencies`: 可选依赖 Mod ID 列表

模板的构建脚本、部署脚本、安装器和发布脚本都会优先读取这里的 `id` 和 `name`，并在最终产物中生成游戏当前要求的 `<mod_id>.json` 外置清单文件。

## 当前目录结构

| 路径 | 作用 |
| --- | --- |
| `src/` | Mod 的 C# 代码和 csproj |
| `pack_assets/` | 打进 `.pck` 的静态资源 |
| `scripts/` | 构建、部署、安装、打包、发布脚本 |
| `installer/` | Inno Setup 安装器脚本 |
| `.github/workflows/` | 自托管 Windows Runner 的发布工作流 |

## 你通常会改哪些地方

### 1. 写 Mod 逻辑

在 `src/` 下添加你的功能代码和 Harmony patches。

当前模板里保留了：

- `ModEntry.cs`: 初始化入口
- `ModConfig.cs`: 本地配置读取
- `MultiplayerModListPatches.cs`: 可选隐藏联机校验名单

### 2. 放资源

把要打包进 `.pck` 的资源放到 `pack_assets/` 下面。

默认目录是 `pack_assets/Sts2ModTemplate/`。如果你改了 `mod_manifest.json` 里的 `id`，打包脚本会优先尝试寻找同名目录；找不到时会退回到 `pack_assets/` 下的第一个子目录。

### 3. 调整配置

`config.json` 现在只有两个基础选项：

- `enabled`
- `hide_from_multiplayer_mod_list`

如果你的 Mod 需要更多配置，可以直接扩展 `src/ModConfig.cs` 和根目录 `config.json`。

## 常用命令

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## 模板默认约束

- 默认不引入联网、遥测、自动更新、远程数据依赖
- 默认不引入任何外部数据采集链路
- 优先保持“本地可构建、本地可部署、本地可打包”
- 所有改动都应顺带考虑 `deploy / portable / installer / release` 四条链路

## 建议的下一步

1. 先改 `mod_manifest.json`
2. 再改 `pack_assets` 里的本地化字符串
3. 然后开始往 `src/` 里加你真正需要的功能
4. 第一次本地验证优先跑 `deploy.ps1`
