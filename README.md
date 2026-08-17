# See.Net

基于 .NET 10 + WPF 的 Windows 桌面文件预览与编辑工具，核心交互对标 macOS Finder 的「空格快速预览」（Quick Look）。

## 功能

- 文件浏览：目录导航（前进 / 后退 / 上级）、拖放文件、路径直达。
- 空格预览：在文件列表中选中文件后按空格，立即弹出预览层；再按空格或 Esc 关闭，↑/↓ 在预览层内切换文件。
- 文本 / 代码预览与编辑：语法高亮、编码识别（UTF-8 / UTF-16 / GB18030 等）、编辑模式、撤销重做（AvalonEdit）、保存。
- 二进制十六进制编辑器：自研控件，三栏布局（偏移 / Hex / ASCII），支持字节编辑（覆盖 / 插入 / 删除）、偏移跳转、Hex 搜索、多格式复制、编辑字节高亮。
- 图片预览：常见格式直接预览，支持适应窗口、实际大小、缩放。
- Office 文档双引擎预览：docx / xlsx / pptx（含 docm / xlsm / pptm）、RTF、ODF（odt / ods / odp）。
  - 结构化视图（默认）：DocumentFormat.OpenXml / 自研 RTF·ODF 解析，秒开、可单测；Word 按标题/段落/表格渲染，Excel 多工作表 DataGrid（每表上限 1 万行，超出截断提示），PPT 逐页卡片。
  - 网页渲染视图：WebView2 + 离线内嵌 mammoth / SheetJS / PPTXjs，接近原样的高保真排版；预览顶部一键切换。旧版 .xls 由 SheetJS 兜底（仅网页视图）；.doc / .ppt 两种引擎皆不支持，提示后可转十六进制。
  - 首次构建需运行 `scripts/fetch-office-libs.ps1` 拉取固定版本的 JS 渲染库（SHA-256 校验）。
- Markdown 预览：默认渲染视图（Markdig + 自研 GitHub 风格离线样式，表格 / 任务列表 / 围栏代码 / 删除线 / 自动锚点）；一键切换源码模式，编辑 / 保存 / 编码切换全套可用；md 内相对图片按所在目录解析。
- 网页预览：本地 HTML（.html / .htm / .xhtml）以 WebView2 按原目录渲染，脚本启用、相对引用（./img、style.css）天然生效；一键切换只读源码。
- 音频预览：mp3 / wav / flac / ogg / m4a / aac 等常见格式播放（WebView2 自研播放页），支持进度拖动、5 秒快进退、音量、倍速、循环；元数据展示（时长 / 大小 / 近似比特率）。
- PDF 预览：WebView2 / Chromium 内置 PDF 查看器，支持缩放与翻页（只读）。
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
| Office 解析 | DocumentFormat.OpenXml 3.5.1 + 自研 RTF / ODF 读取器 |
| Office 高保真渲染 | WebView2 + mammoth.js 1.12.1 / SheetJS 0.20.3 / PPTXjs 1.21.1（离线内嵌） |
| Markdown 渲染 | Markdig 1.3.2 + 自研 GitHub 风格离线 CSS（WebView2 承载） |
| 网页 / 音频 / PDF 渲染 | WebView2（目录映射 / 自研离线播放页 / Chromium PDF 查看器；音频与 PDF 经未映射域拦截实现 HTTP Range） |
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
- Office 预览：只读（不进入编辑/保存链路）；Excel 每个工作表预取上限 1 万行、Word 上限 1 万块，超出显示截断提示；网页渲染视图依赖 WebView2 运行时（缺失时结构化视图不受影响）；PPTXjs 为尽力渲染（上游已停止维护），复杂版式以结构化视图为准。
- PDF 预览：依赖 WebView2 运行时（缺失时降级为提示卡片）；使用 Chromium 内置 PDF 查看器，支持缩放与翻页；只读。
- 图片仅支持预览，编辑功能后续版本加入。
- Markdown 预览：内嵌原始 HTML 被转义显示为文本（防脚本执行，原始 HTML 不参与渲染）；渲染输入上限 200 万字符，超出提示切源码模式；渲染视图依赖 WebView2 运行时（缺失时自动进入源码模式，编辑能力不受影响）。
- 网页预览：脚本默认启用 —— 预览不受信任的 HTML 等同于在浏览器中打开它；文件所在目录映射进 WebView2 沙箱（相对引用所需），映射随预览关闭即销毁，顶级导航与新窗口一律转交系统浏览器。文件名含 `#` / `?` 时渲染不可用，自动切源码模式。
- 音频预览：依赖 WebView2 运行时（缺失时降级为提示卡片）；编解码能力取决于 WebView2 内核（Chromium）—— WMA / AIFF / MIDI 不支持，mp3 / wav / flac / ogg (vorbis·opus) / m4a (aac) 支持，不支持的格式给出明确提示。

## 后续规划

- 最近打开与固定、多标签页
- 文件内容全文搜索（正则）
- 外部修改监控与提示
- 深色主题、设置界面
- 右键菜单「用 See.Net 打开」
