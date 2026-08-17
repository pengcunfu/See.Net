# See.Net

基于 .NET 10 + WPF 的 Windows 桌面文件预览与编辑工具，核心交互对标 macOS Finder 的「空格快速预览」（Quick Look）。

## 功能

- 文件浏览：目录导航（前进 / 后退 / 上级）、拖放文件、路径直达。
- 空格预览：在文件列表中选中文件后按空格，立即弹出预览层；再按空格或 Esc 关闭，↑/↓ 在预览层内切换文件。
- 文本 / 代码预览与编辑：语法高亮、编码识别（UTF-8 / UTF-16 / GB18030 等）、编辑模式、撤销重做（AvalonEdit）、保存。
- 二进制十六进制编辑器：自研控件，三栏布局（偏移 / Hex / ASCII），支持字节编辑（覆盖 / 插入 / 删除）、偏移跳转、Hex 搜索、多格式复制、编辑字节高亮。
- 图片预览：常见格式直接预览，支持适应窗口、实际大小、缩放。
- 大文件：十六进制视图窗口化读取，内存占用与文件大小无关；1 GB 以内文件可流畅编辑。
- 数据安全：保存走临时文件 + 原子替换，保存前自动备份到用户文档目录。

## 快捷键

| 按键 | 功能 |
| --- | --- |
| 空格 | 打开 / 关闭预览（未选中文件时不响应） |
| Esc | 关闭预览 |
| ↑ / ↓ | 预览内切换上一个 / 下一个文件 |
| Enter | 打开预览（文件夹则进入） |
| 双击 | 打开预览（文件夹则进入） |

十六进制编辑器内：

| 按键 | 功能 |
| --- | --- |
| 0-9 A-F | 输入十六进制 |
| Tab | 切换 Hex / ASCII 区 |
| Insert | 切换覆盖 / 插入模式 |
| Delete / Backspace | 删除字节 |
| Ctrl+A | 全选 |
| Ctrl+C | 复制 Hex 字符串 |
| Ctrl+Shift+C | 复制 ASCII 文本 |
| Ctrl+Alt+C | 复制 C 数组 |
| Ctrl+Home / Ctrl+End | 跳到文件头 / 尾 |

## 技术栈

| 方向 | 选型 |
| --- | --- |
| 框架 | .NET 10（`net10.0-windows`） |
| UI | WPF + MVVM（CommunityToolkit.Mvvm） |
| 文本编辑 | AvalonEdit |
| 十六进制编辑器 | 自研控件（IScrollInfo 虚拟化滚动） |
| 测试 | xUnit |
| 发布 | MSIX（自包含 win-x64，可选自签名） |

## 项目结构

```
See.Net/
├─ See.Net.slnx
├─ See.Net.Core/           # 核心逻辑：HexDocument、类型识别、编码检测（无 UI 依赖）
├─ See.Net/                # WPF 主应用：视图、视图模型、自研 Hex 控件
├─ See.Net.Tests/          # xUnit 单元测试
├─ packaging/
│  ├─ MSIX/                # AppxManifest、SDK 打包工具还原工程
│  └─ assets/              # 应用图标资源（脚本生成）
└─ scripts/
   ├─ generate-assets.ps1  # 生成 PNG / ICO 图标
   └─ package-msix.ps1     # 发布并打包 MSIX
```

## 构建与运行

```powershell
dotnet build See.Net.slnx -c Release
dotnet run --project See.Net
```

运行测试：

```powershell
dotnet test See.Net.Tests
```

## MSIX 打包

打包脚本会通过 NuGet 还原 Windows SDK BuildTools（MakeAppx / signtool），无需单独安装 Windows SDK：

```powershell
# 生成图标资源（首次或修改后执行）
powershell -ExecutionPolicy Bypass -File scripts/generate-assets.ps1

# 打包（含自签名证书，证书保存在当前用户证书库）
powershell -ExecutionPolicy Bypass -File scripts/package-msix.ps1 -SelfSign

# 不带签名
powershell -ExecutionPolicy Bypass -File scripts/package-msix.ps1
```

产物位于 `artifacts/msix/See.Net_<版本>_x64.msix`。

安装示例：

```powershell
Add-AppxPackage -Path "D:\Projects\DevTools\See.Net\artifacts\msix\See.Net_1.0.0.0_x64.msix" -AllowUnsigned
```

> 自签名包需要先信任签名证书，或在开发者模式下使用 `-AllowUnsigned`。正式发布请使用受信任的代码签名证书。

## 数据目录

应用数据统一存放在用户文档目录下的 `See.Net` 文件夹：

```
Documents/
└─ See.Net/
   ├─ settings.json    # 设置（上次目录、Hex 字号、每行字节数、备份开关等）
   ├─ Backups/         # 保存前自动备份
   └─ Logs/            # 未处理异常日志
```

## 使用边界

- 十六进制编辑器：1 GB 以内文件可流畅编辑；更大文件仍可查看（虚拟化滚动）。
- 文本预览 / 编辑：100 MB 以内自动加载；超过 100 MB 时提示改用十六进制视图（避免大文本加载拖垮内存）。
- 图片仅支持预览，编辑功能后续版本加入。

## 后续规划

- 最近打开与固定、多标签页
- 文件内容全文搜索（正则）
- 外部修改监控与提示
- 深色主题、设置界面
- 右键菜单「用 See.Net 打开」
