# See.Net 开发计划

## 1. 项目概述

See.Net 是一个基于 .NET 10 的 Windows 桌面文件预览与编辑工具，核心体验对标 macOS Finder 的「空格快速预览」（Quick Look）：

- 在文件列表中选中任意文件，按空格键即可弹出预览层，无需打开完整应用即可快速查看内容。
- 文本 / 代码类文件以可读文本形式预览，并支持进入编辑模式直接修改后保存。
- 二进制文件以十六进制编辑器展示，支持逐字节编辑、跳转、搜索等操作。
- 支持图片等常见媒体类型的直接预览。

项目目标：提供一款轻量、快速、可处理大文件的本地文件查看与编辑工具。

## 2. 关键假设（待用户确认）

以下假设基于需求推断，若与预期不符，请审核时指出：

| 编号 | 假设 | 影响 |
| --- | --- | --- |
| A1 | 目标平台为 Windows 桌面应用，使用 WPF | 决定 UI 框架与项目结构；若需跨平台可换 Avalonia |
| A2 | 「空格预览」指类 Quick Look 的快速预览交互（列表选中 + 空格键弹出预览层） | 决定主界面需要文件列表视图 |
| A3 | 预览内容同时支持只读查看与编辑保存 | 决定编辑器的保存与脏标记机制 |
| A4 | 需要处理的文件可能很大（如日志、数据库备份），不能整文件读入内存 | 决定流式读取与窗口化显示策略 |
| A5 | 项目使用 MIT 许可证开源（默认） | 仅影响仓库元数据 |

## 3. 技术选型

| 方向 | 选型 | 理由 |
| --- | --- | --- |
| 框架 | .NET 10 (`net10.0-windows`) | 目标环境已安装 10.0.302 SDK |
| UI | WPF + MVVM | Windows 原生体验好，成熟稳定 |
| MVVM 工具包 | CommunityToolkit.Mvvm | 官方维护，代码生成减少样板代码 |
| 文本编辑 | AvalonEdit（ICSharpCode.AvalonEdit） | 成熟的 WPF 文本编辑器，支持语法高亮、撤销重做、大文件虚拟化 |
| 十六进制编辑 | 自研 WPF 控件 | 现有 NuGet 方案少且维护不活跃，自研可控且便于按需求定制 |
| 图片预览 | WPF 内置 `BitmapImage` + 解码缩放 | 满足常见格式，无需第三方库 |
| 单元测试 | xUnit | .NET 生态标准 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 服务解耦，便于测试与扩展 |

## 4. 功能需求拆解

### 4.1 核心功能（P0，首版必须完成）

1. 文件浏览
   - 文件夹导航（前进 / 后退 / 上级目录）
   - 文件列表（名称、大小、修改时间、类型）
   - 文件打开方式：目录树 + 列表，支持拖放文件进入窗口
2. 空格快速预览
   - 列表选中文件后按空格键弹出预览浮层
   - 再次按空格或 Esc 关闭预览层
   - 在预览层内支持上下键切换文件（保持 Quick Look 交互习惯）
3. 文件类型识别
   - 按扩展名与文件头（magic bytes）双通道识别
   - 分类：文本 / 代码、二进制、图片、未知
4. 文本预览与编辑
   - 文本类文件只读预览，语法高亮
   - 支持进入编辑模式：修改、撤销重做、脏标记、保存 / 另存为
   - 编码识别与切换（UTF-8、GB2312 等常见编码，默认 UTF-8）
5. 二进制十六进制编辑
   - 三栏布局：偏移量（Offset）、十六进制字节（Hex）、ASCII 区
   - 字节级编辑，支持插入 / 删除 / 覆盖字节
   - 跳转到指定偏移量
   - 按十六进制串或 ASCII 文本搜索
   - 选择区复制（Hex / ASCII / C 数组 三种格式）
   - 修改标记（已编辑字节高亮）与保存
6. 图片预览
   - 常见格式（PNG、JPG、BMP、GIF、WebP、SVG 按位图渲染）直接显示
   - 缩放（适应窗口 / 实际大小 / 自定义缩放）
7. 保存策略
   - 编辑后统一走「保存」流程：脏标记提示、另存为支持
   - 保存前自动备份原文件为 `文件名.原始时间戳.bak`（可配置开关）
   - 文本与十六进制编辑共享同一份字节流语义，保证切换视图时数据一致

### 4.2 增强功能（P1，版本迭代）

1. 最近打开文件列表与固定（Pin）
2. 文件内容搜索（当前文件内全文搜索，支持正则）
3. 多标签页同时打开多个文件
4. 文件变更监控（外部修改时提示刷新 / 重新加载）
5. 主题切换（浅色 / 深色）与字体设置
6. 命令行打开（`See.Net.exe <file>`）与右键菜单「用 See.Net 打开」
7. 安装包（MSIX 或 Inno Setup）

## 5. 项目结构

```
See.Net/
├─ See.Net.sln
├─ See.Net/                        # WPF 主应用
│  ├─ App.xaml / App.xaml.cs       # 启动、DI 容器、全局异常处理
│     ├─ MainWindow.xaml(.cs)      # 主窗口：导航栏 + 文件列表 + 预览层
│     ├─ Models/
│     │  ├─ FileEntry.cs           # 文件条目（名称/路径/大小/类型分类）
│     │  ├─ DocumentModel.cs       # 文件文档抽象（字节流语义、脏标记）
│     │  ├─ HexDocument.cs         # 十六进制文档（窗口化字节访问）
│     │  └─ TextDocument.cs        # 文本文档（编码、加载、保存）
│     ├─ ViewModels/
│     │  ├─ MainViewModel.cs       # 导航与文件列表
│  │  ├─ PreviewViewModel.cs    # 预览层状态机（文本/Hex/图片/关闭）
│  │  ├─ HexEditorViewModel.cs  # 十六进制编辑逻辑
│  │  └─ TextEditorViewModel.cs # 文本编辑逻辑
│  ├─ Views/
│  │  ├─ PreviewPane.xaml       # 预览浮层容器
│  │  ├─ HexEditorView.xaml     # 十六进制编辑器视图
│  │  └─ TextView.xaml          # 文本编辑器视图
│  ├─ Controls/
│  │  └─ HexEditor/             # 自研十六进制控件（渲染、输入、选择）
│  ├─ Services/
│  │  ├─ FileTypeDetector.cs    # 扩展名 + Magic Bytes 识别
│  │  ├─ FileSystemService.cs   # 目录列举、文件信息
│  │  ├─ EncodingService.cs     # 编码检测与转换
│  │  ├─ FileWatcherService.cs  # 外部变更监控（P1）
│  │  └─ SettingsService.cs     # 设置持久化（JSON）
│  └─ Infra/
│     ├─ RelayCommand.cs        # 或使用 CommunityToolkit 生成命令
│     └─ VisualTreeHelper.cs    # 列表项键盘交互辅助
See.Net.Tests/                   # xUnit 单元测试
├─ HexDocumentTests.cs           # 窗口化读取、编辑、保存正确性
├─ HexFormatTests.cs             # 三栏格式化、大小写、分组
├─ FileTypeDetectorTests.cs      # 类型识别用例
├─ EncodingTests.cs              # 编码检测用例
└─ SearchTests.cs                # Hex/文本搜索
```

## 6. 核心模块设计要点

### 6.1 文件类型识别

- 优先按扩展名匹配内置表（约 200 种常见类型），命中后按 Magic Bytes 二次校验。
- 未知扩展名时仅靠 Magic Bytes 判断文本 / 二进制。
- 判定为文本的规则：前 4 KB 不含 NUL 字节，且可被目标编码无损解码。
- 结果分类：Text / Code / Binary / Image / Unknown。

### 6.2 十六进制编辑器（自研控件）

- 数据模型 `HexDocument` 与 UI 控件解耦：控件只负责渲染与输入，文档负责字节存取。
- 大文件支持：只将可视区域的字节窗口加载到内存（默认 1 MB 窗口 + 前后预取），滚动时增量加载；编辑记录以「偏移 + 新旧字节」补丁形式保存，落盘时统一合并写入。
- 渲染性能：基于 `VirtualizingStackPanel` 或自绘 `DrawingVisual`，避免为每个字节创建控件。
- 输入体验：按字节 / 半字节输入自动前进；支持 Tab 在 Hex 与 ASCII 区切换；支持键盘选择、Ctrl+C 复制、Ctrl+F 搜索。
- 脏字节高亮：蓝色表示新增 / 覆盖，红色表示删除（删除以标记实现，保存时应用）。

### 6.3 文本编辑

- 预览模式：AvalonEdit 设为只读，按文件类型启用高亮。
- 编辑模式：启用编辑、撤销重做栈、行号、脏标记；`Ctrl+S` 保存，`Ctrl+Shift+S` 另存为。
- 编码：默认 UTF-8；检测失败或乱码时可手动切换编码并重新加载；保存时按所选编码写出。
- 大文本：AvalonEdit 自带文档分段存储，单文件建议限制 100 MB 内可流畅编辑，更大文件降级为只读预览（计划文档中明确此边界）。

### 6.4 预览层交互

- 预览层为覆盖在列表上的浮层面板（非独立窗口），快捷键空格切换开 / 关，Esc 关闭，方向键切换文件。
- 空格在有焦点编辑框内时不触发预览（避免与输入冲突）。
- 切换文件时若当前文件有未保存修改，先弹出保存确认，再切换。

### 6.5 保存安全

- 保存采用「写临时文件 + 原子替换」策略，避免写入中途崩溃损坏原文件。
- 保存前默认生成 `.bak` 备份；成功后删除临时文件，失败则保留原文件并提示。
- 文件以共享读方式打开（`FileShare.ReadWrite`）避免锁定，但编辑保存时以独占写打开临时文件。

## 7. 性能与大文件策略

| 场景 | 策略 |
| --- | --- |
| 目录列举 | 异步列举 + UI 分批刷新，避免卡顿 |
| 文本预览 | 前 1 MB 快速预览 + 完整加载按钮；超 100 MB 只读 |
| 十六进制浏览 | 窗口化读取，内存峰值与文件大小无关 |
| 十六进制编辑 | 补丁式记录，避免全量内存拷贝 |
| 图片预览 | 解码时按显示尺寸降采样（`DecodePixelWidth`） |
| 保存 | 流式写入，不整文件载入 |

## 8. 开发里程碑

| 阶段 | 内容 | 验收标准 |
| --- | --- | --- |
| M1 脚手架 | 解决方案、WPF 工程、MVVM 基础设施、DI、xUnit 测试工程 | 应用可启动，显示空主窗口，测试可运行 |
| M2 文件浏览 | 目录导航、文件列表、拖放、文件类型识别 | 可浏览本机任意目录，类型分类正确率达标 |
| M3 空格预览 | 预览层浮层、快捷键、文本只读预览、图片预览 | 选中文件按空格可预览文本 / 图片，Esc 关闭 |
| M4 Hex 编辑器 | 自研控件：三栏渲染、滚动、选择、复制 | 1 GB 文件可流畅滚动查看，内存占用稳定 |
| M5 编辑能力 | Hex 字节编辑、文本编辑模式、脏标记、保存 / 另存为 / 备份 | 编辑后可正确保存，备份可用，撤销重做正常 |
| M6 搜索与跳转 | Hex / 文本搜索、偏移跳转 | 在 1 GB 文件内搜索定位正确 |
| M7 打磨与发布 | 主题、设置持久化、命令行打开、图标、发布配置 | 可通过安装包安装使用，日常操作流畅 |

每个阶段完成时都会进行自测（单元测试 + 手工用例），M7 前集中处理缺陷。

## 9. 测试策略

- 单元测试覆盖纯逻辑层：类型识别、编码检测、Hex 文档读写、格式化、搜索、保存补丁合并。
- UI 控件采用冒烟测试 + 手工测试用例清单（键盘操作、大文件滚动、边界偏移）。
- 关键路径（编辑 → 保存 → 重新打开）做数据一致性校验测试。

## 10. 风险与对策

| 风险 | 对策 |
| --- | --- |
| 自研 Hex 控件开发量大 | M4 单独成阶段，先实现最小可用版（查看 + 覆盖编辑），再迭代增强 |
| 超大文件编辑性能 | 窗口化 + 补丁记录，明确首版支持边界（建议 2 GB 内可编辑） |
| 编码识别不准导致乱码 | 提供手动编码切换；识别算法使用多种启发式并给置信度 |
| 保存损坏风险 | 临时文件 + 原子替换 + 自动备份三重保障 |
| AvalonEdit 对 .NET 10 的兼容性 | 使用社区活跃分支；若不可用则评估自研文本控件（备选） |

## 11. 待确认问题

审核时请重点确认以下内容：

1. UI 框架：默认 WPF（仅 Windows）。是否需要跨平台（则改用 Avalonia）？
2. 预览交互：空格预览是否需要独立的「文件浏览主界面」，还是仅支持拖入单个文件后按空格预览？
3. 编辑范围：首版是否需要图片编辑？默认仅支持查看。
4. 大文件边界：2 GB 内可编辑、更大文件只读，是否可接受？
5. 发布形态：是否需要安装包（默认提供 MSIX）？

计划经确认后，将按 M1 至 M7 顺序开工。

## 12. 已确认决策与完成状态

用户已确认以下决策：

| 决策点 | 结论 |
| --- | --- |
| UI 技术 | WPF（仅 Windows） |
| 空格预览交互 | 文件列表中选中文件后按空格弹出预览层；未选中文件时空格不响应 |
| 图片编辑 | 首版仅预览，编辑后续再加 |
| 大文件边界 | 1 GB 以内可编辑（十六进制窗口化实现）；文本预览上限 100 MB，更大走十六进制视图 |
| 发布形态 | MSIX（自包含 win-x64，脚本支持自签名） |
| 数据目录 | 用户文档目录下 `See.Net`（设置、备份、日志） |

当前进度：M1 至 M7 已全部实现并通过构建与单元测试，MSIX 打包脚本已产出可安装包。

## 13. Office 双引擎预览（2026-08 增补）

### 方案

Office（Word / Excel / PPT）文档预览采用双引擎并存、一键切换：

| 引擎 | 实现 | 定位 |
| --- | --- | --- |
| 结构化视图（默认） | `See.Net.Core/Office`：DocumentFormat.OpenXml 3.5.1 解析 docx/xlsx/pptx；自研 RTF 字节级 tokenizer、ODF（zip + content.xml）读取器 | 秒开、离线、可单测；Excel SAX 流式读取 + 1 万行/表上限 |
| 网页渲染视图 | WebView2（虚拟主机映射 webassets）+ 离线内嵌 mammoth 1.12.1（docx）/ SheetJS 0.20.3（xlsx、旧 xls）/ PPTXjs 1.21.1（pptx，尽力渲染） | 高保真排版；文件字节经 WebResourceRequested 拦截流式回吐，不经 base64 消息 |

### 边界

- 预览只读，不进入编辑/保存链路。
- 旧版 .doc / .ppt（OLE2 二进制）两种引擎均不支持 → 提示卡片 + 十六进制兜底；.xls 由 SheetJS 兜底（仅网页视图）。
- WebView2 运行时缺失时结构化视图不受影响，仅网页模式降级；MSIX 包内 WebView2 userDataFolder 显式指向 `%LOCALAPPDATA%\See.Net\WebView2`。
- JS 渲染库经 `scripts/fetch-office-libs.ps1` 固定版本 + SHA-256 校验拉取，随包分发（许可合规见 NOTICE）。

### 状态

已完成：Core 解析层与 42 项单元测试（含 docx/xlsx/pptx 生成断言、RTF GBK 十六进制转义、ODF zip 构造、大表截断）、渲染页与拉取脚本、OfficeContentViewModel / OfficeView / OfficeWebHost 双引擎切换、PreviewPane 模板注册、README/本计划更新。

## 14. Markdown / 网页 / 音频预览（2026-08 增补）

### 方案

三类新预览沿用「ContentKind 分发 + DataTemplate 映射」链路；WebView2 侧把原 OfficeWebHost 抽取为 `WebViewHostBase` 基类（共享运行时探测、环境创建、生命周期），四个宿主（Office / Markdown / Html / Audio）均为薄子类。

| 类型 | 实现 | 要点 |
| --- | --- | --- |
| Markdown | Markdig 1.3.2（Core 层，MIT）渲染 HTML → WebView2 容器页 + 自研 GitHub 风格离线 CSS | 默认渲染视图；源码模式组合 TextContentViewModel（编辑 / 保存 / 编码全套复用）；`DisableHtml()` 转义原始 HTML；md 所在目录映射为 mdcontent.local（相对图片解析）；渲染上限 200 万字符 |
| 网页 | WebView2 文件所在目录映射 preview.local + 直接导航映射 URL | 脚本启用（用户决策）；相对引用天然按原目录解析；顶级导航 / 新窗口一律外部浏览器；只读源码懒加载（TextContentViewModel `allowEdit:false`） |
| 音频 | WebView2 自研播放页（无第三方 JS）+ /data 拦截 | 实现 HTTP Range（206 + Content-Range + 限长 SubStream）供 Chromium 媒体栈 seek；播放 / 进度 / 音量 / 倍速 / 循环；元数据（时长 / 大小 / 近似比特率） |

### 边界

- Markdown 内嵌原始 HTML 被转义为文本（预览任意来源文件不执行脚本）；超限或运行时缺失自动落源码模式。
- 网页预览等同在浏览器打开不受信页面（脚本可读同目录文件并外发）；映射生命周期 = 预览实例，顶级导航与新窗口一律外开。文件名含 `#` / `?` 时渲染不可用，自动切源码。
- 音频编解码取决于 WebView2 内核：WMA / AIFF / MIDI 不支持（error 上报后降级提示卡片），mp3 / wav / flac / ogg / m4a 支持。运行时缺失时降级为提示卡片（附十六进制入口）。
- 新增 Core 纯逻辑与单测：MarkdownRenderer（转义 / 超限）、RangeSpec（单区间解析），FileTypeDetector 新增 Markdown / WebPage / Audio 三分类。

### 状态

已完成：WebViewHostBase 抽取与 OfficeWebHost 派生化、Core 三分类与 Markdig/RangeRequest 及 14 项新单测（合计 60 项全绿）、Markdown/Web/Audio 三组 VM + 宿主 + 视图、webassets 新增 markdown-preview / markdown.css / audio-player（全部自研无新增第三方 JS）、PreviewPane 与 ShellPreviewWindow 模板注册（顺带补齐 Shell 侧缺失的 Office 模板）、README / NOTICE / 本计划更新。
