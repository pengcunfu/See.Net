import { useEffect } from 'react'
import { useLocation, Link } from 'react-router-dom'
import './Docs.css'

/* ── Sidebar config ── */
const sidebarSections = [
  {
    title: '开始使用',
    links: [
      { id: 'overview', label: '项目简介' },
      { id: 'installation', label: '安装说明' },
      { id: 'build', label: '构建与运行' },
      { id: 'data-directory', label: '数据目录' },
    ],
  },
  {
    title: '核心功能',
    links: [
      { id: 'preview', label: '空格快速预览' },
      { id: 'text-code', label: '文本 & 代码' },
      { id: 'hex-editor', label: '十六进制编辑' },
      { id: 'image', label: '图片预览' },
      { id: 'office', label: 'Office 文档' },
      { id: 'markdown', label: 'Markdown' },
      { id: 'web', label: '网页预览' },
      { id: 'audio', label: '音频播放' },
      { id: 'pdf', label: 'PDF 预览' },
    ],
  },
  {
    title: '参考',
    links: [
      { id: 'shortcuts', label: '快捷键' },
      { id: 'limitations', label: '使用边界' },
      { id: 'tech-stack', label: '技术栈' },
      { id: 'roadmap', label: '后续规划' },
      { id: 'faq', label: '常见问题' },
    ],
  },
]

/* ── Sidebar ── */
function Sidebar() {
  const { hash } = useLocation()

  useEffect(() => {
    const links = document.querySelectorAll('.sidebar-link')
    const sections = []
    links.forEach(link => {
      const id = link.getAttribute('href')?.slice(1)
      if (id) {
        const el = document.getElementById(id)
        if (el) sections.push({ link, el })
      }
    })
    if (!sections.length) return

    const update = () => {
      let current = sections[0]
      for (const s of sections) {
        if (s.el.getBoundingClientRect().top <= 100) current = s
      }
      links.forEach(l => l.classList.remove('active'))
      current.link.classList.add('active')
    }

    window.addEventListener('scroll', update, { passive: true })
    update()
    return () => window.removeEventListener('scroll', update)
  }, [hash])

  return (
    <aside className="docs-sidebar">
      {sidebarSections.map(sec => (
        <div className="sidebar-section" key={sec.title}>
          <div className="sidebar-section-title">{sec.title}</div>
          {sec.links.map(l => (
            <a href={`#${l.id}`} className="sidebar-link" key={l.id}>{l.label}</a>
          ))}
        </div>
      ))}
    </aside>
  )
}

/* ── Page ── */
export default function Docs() {
  const { hash } = useLocation()

  useEffect(() => {
    if (hash) {
      const el = document.getElementById(hash.slice(1))
      if (el) el.scrollIntoView({ behavior: 'smooth' })
    }
  }, [hash])

  return (
    <div className="docs-layout">
      <Sidebar />
      <main className="docs-main">
        {/* 项目简介 */}
        <section id="overview">
          <h1>See.Net 文档</h1>
          <p>
            See.Net 是一款基于 .NET 10 + WPF 的 Windows 桌面文件预览与编辑工具，核心交互对标 macOS Finder 的「空格快速预览」（Quick Look）。
            在资源管理器中选中文件后按空格，即可弹出预览浮窗，无需启动完整应用即可快速查看内容。
          </p>
          <h3>核心特性</h3>
          <ul>
            <li><strong>托盘常驻</strong> — 启动后只在系统托盘运行，不打开文件浏览器主窗口</li>
            <li><strong>空格预览</strong> — 在资源管理器中选中文件后按空格，弹出 Quick Look 式预览浮窗</li>
            <li><strong>文本 / 代码预览与编辑</strong> — 语法高亮、编码识别、编辑模式、撤销重做、保存</li>
            <li><strong>二进制十六进制编辑器</strong> — 自研控件，三栏布局，支持字节编辑、偏移跳转、搜索</li>
            <li><strong>Office 文档双引擎预览</strong> — 结构化视图秒开 + 网页渲染视图高保真排版</li>
            <li><strong>Markdown 预览</strong> — GitHub 风格渲染，一键切换源码模式</li>
            <li><strong>网页 / 音频 / PDF 预览</strong> — WebView2 驱动的多媒体预览</li>
            <li><strong>大文件支持</strong> — 十六进制视图窗口化读取，1 GB 以内可流畅编辑</li>
            <li><strong>数据安全</strong> — 保存走临时文件 + 原子替换，自动备份</li>
          </ul>
        </section>

        <hr />

        {/* 安装说明 */}
        <section id="installation">
          <h2>安装说明</h2>
          <h3>推荐方式：自包含版本</h3>
          <p>直接运行发布文件夹中的 See.exe 即可，无需安装 .NET 运行时或任何证书配置。</p>
          <ol>
            <li>前往 <a href="https://github.com/pengcunfu/See.Net/releases" target="_blank" rel="noopener">GitHub Releases</a> 下载最新版本</li>
            <li>解压到任意目录</li>
            <li>双击 <code>See.exe</code> 运行</li>
            <li>（可选）右键 → 发送到 → 桌面快捷方式</li>
          </ol>
          <h3>系统要求</h3>
          <table>
            <thead><tr><th>项目</th><th>要求</th></tr></thead>
            <tbody>
              <tr><td>操作系统</td><td>Windows 10 或更高版本 (64-bit)</td></tr>
              <tr><td>内存</td><td>4 GB 或更多</td></tr>
              <tr><td>磁盘空间</td><td>500 MB 可用空间</td></tr>
              <tr><td>WebView2 运行时</td><td>通常已预装（Office/PDF/音频预览需要）</td></tr>
            </tbody>
          </table>
          <div className="alert alert-info">
            <strong>提示：</strong>自包含版本大小约 184 MB，包含所有必要的运行时文件。可以将整个文件夹复制到 U 盘，在任意 Windows 电脑上运行。
          </div>
          <h3>卸载</h3>
          <p>直接删除整个文件夹即可，不会在系统中留下任何残留。</p>
        </section>

        <hr />

        {/* 构建与运行 */}
        <section id="build">
          <h2>构建与运行</h2>
          <h3>环境准备</h3>
          <ul>
            <li>.NET 10 SDK（10.0.302 或更高版本）</li>
            <li>Windows 10 / 11 (64-bit)</li>
            <li>WebView2 运行时（可选，Office 网页预览需要）</li>
          </ul>
          <h3>构建命令</h3>
          <pre><code>{`# 克隆仓库
git clone https://github.com/pengcunfu/See.Net.git
cd See.Net

# 构建
dotnet build See.Net.slnx -c Release

# 运行
dotnet run --project See.Net`}</code></pre>
          <h3>运行测试</h3>
          <pre><code>dotnet test See.Net.Tests</code></pre>
          <h3>Office 网页预览资源</h3>
          <p>首次构建需运行脚本拉取 JS 渲染库：</p>
          <pre><code>scripts/fetch-office-libs.ps1</code></pre>
          <p>该脚本会下载固定版本的 mammoth.js / SheetJS / PPTXjs 并进行 SHA-256 校验。</p>
        </section>

        <hr />

        {/* 数据目录 */}
        <section id="data-directory">
          <h2>数据目录</h2>
          <p>应用数据统一存放在用户文档目录下：</p>
          <pre><code>{`Documents/
└─ FNSoftware/
   └─ See/
      ├─ settings.json    # 设置
      ├─ Backups/         # 保存前自动备份
      └─ Logs/            # 未处理异常日志`}</code></pre>
        </section>

        <hr />

        {/* 空格快速预览 */}
        <section id="preview">
          <h2>空格快速预览</h2>
          <p>See.Net 的核心交互：在 Windows 资源管理器中选中任意文件，按下空格键即可弹出预览浮窗。</p>
          <ul>
            <li>选中文件后按 <kbd>Space</kbd> 打开预览</li>
            <li>再按 <kbd>Space</kbd> 或 <kbd>Esc</kbd> 关闭预览</li>
            <li>按 <kbd>↑</kbd> / <kbd>↓</kbd> 在多选文件间切换</li>
            <li>未选中文件时按空格不响应</li>
            <li>编辑器内获得焦点时，空格不触发预览（避免与输入冲突）</li>
          </ul>
          <p>此外，还可以通过以下方式打开预览：</p>
          <ul>
            <li>托盘菜单「打开文件…」选择文件</li>
            <li>命令行：<code>See.exe &lt;file&gt;</code></li>
          </ul>
        </section>

        <hr />

        {/* 文本 & 代码 */}
        <section id="text-code">
          <h2>文本 & 代码预览</h2>
          <p>支持几乎所有文本和代码文件的预览与编辑。</p>
          <h3>功能</h3>
          <ul>
            <li><strong>语法高亮</strong> — 内置多种语言高亮规则，包括 JSON、TOML、YAML、Log 等</li>
            <li><strong>编码识别</strong> — 自动检测 UTF-8 / UTF-16 / GB18030 等编码，支持手动切换</li>
            <li><strong>编辑模式</strong> — 从只读切换到编辑，支持修改、撤销重做、脏标记</li>
            <li><strong>保存</strong> — <code>Ctrl+S</code> 保存，<code>Ctrl+Shift+S</code> 另存为</li>
            <li><strong>自定义字体</strong> — 支持自定义文本字体和字号</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>100 MB 以内自动加载；超过 100 MB 时提示改用十六进制视图</li>
            <li>大文件仍可通过十六进制视图查看和编辑</li>
          </ul>
        </section>

        <hr />

        {/* 十六进制编辑 */}
        <section id="hex-editor">
          <h2>十六进制编辑器</h2>
          <p>自研 WPF 控件，基于 IScrollInfo 虚拟化滚动，内存占用与文件大小无关。</p>
          <h3>布局</h3>
          <p>经典三栏布局：偏移量（Offset）| 十六进制字节（Hex）| ASCII 区</p>
          <h3>功能</h3>
          <ul>
            <li><strong>字节编辑</strong> — 覆盖 / 插入 / 删除字节</li>
            <li><strong>偏移跳转</strong> — 快速跳转到指定偏移量</li>
            <li><strong>Hex 搜索</strong> — 按十六进制串或 ASCII 文本搜索</li>
            <li><strong>多格式复制</strong> — Hex 字符串 / ASCII 文本 / C 数组</li>
            <li><strong>修改标记</strong> — 已编辑字节高亮显示</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>1 GB 以内文件可流畅编辑</li>
            <li>更大文件仍可查看（虚拟化滚动）</li>
          </ul>
        </section>

        <hr />

        {/* 图片预览 */}
        <section id="image">
          <h2>图片预览</h2>
          <p>常见图片格式直接预览，支持多种查看模式。</p>
          <h3>支持格式</h3>
          <p>PNG、JPG/JPEG、BMP、GIF（静态）、WebP、SVG（按位图渲染）</p>
          <h3>操作</h3>
          <ul>
            <li><strong>适应窗口</strong> — 自动缩放以适应预览区域</li>
            <li><strong>实际大小</strong> — 100% 原始尺寸显示</li>
            <li><strong>自由缩放</strong> — 鼠标滚轮或手势缩放</li>
          </ul>
          <div className="alert alert-info">
            <strong>注意：</strong>图片仅支持预览，编辑功能将在后续版本加入。
          </div>
        </section>

        <hr />

        {/* Office */}
        <section id="office">
          <h2>Office 文档预览</h2>
          <p>支持 Word / Excel / PowerPoint 文档的双引擎预览，一键切换。</p>
          <h3>支持格式</h3>
          <table>
            <thead><tr><th>类型</th><th>格式</th></tr></thead>
            <tbody>
              <tr><td>Word</td><td>.docx, .docm, .rtf, .odt</td></tr>
              <tr><td>Excel</td><td>.xlsx, .xlsm, .xls（仅网页视图）, .ods</td></tr>
              <tr><td>PowerPoint</td><td>.pptx, .pptm, .odp</td></tr>
            </tbody>
          </table>
          <h3>双引擎</h3>
          <h4>结构化视图（默认）</h4>
          <ul>
            <li>DocumentFormat.OpenXml 解析 docx/xlsx/pptx</li>
            <li>自研 RTF 字节级 tokenizer、ODF 读取器</li>
            <li>秒开、离线、可单测</li>
            <li>Word 按标题/段落/表格渲染</li>
            <li>Excel 多工作表 DataGrid（每表上限 1 万行）</li>
            <li>PPT 优先经本机 PowerPoint 导出整页 PNG 预览</li>
          </ul>
          <h4>网页渲染视图</h4>
          <ul>
            <li>WebView2 + 离线内嵌 mammoth / SheetJS / PPTXjs</li>
            <li>接近原样的高保真排版</li>
            <li>预览顶部一键切换</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>预览只读，不进入编辑/保存链路</li>
            <li>旧版 .doc / .ppt（OLE2）两种引擎均不支持，提示卡片 + 十六进制兜底</li>
            <li>PPT/PPTX 超过 8 MB 时禁用网页引擎</li>
            <li>WebView2 运行时缺失时结构化视图不受影响</li>
          </ul>
        </section>

        <hr />

        {/* Markdown */}
        <section id="markdown">
          <h2>Markdown 预览</h2>
          <p>默认渲染视图，基于 Markdig + 自研 GitHub 风格离线 CSS。</p>
          <h3>功能</h3>
          <ul>
            <li>GitHub 风格渲染：表格、任务列表、围栏代码、删除线、自动锚点</li>
            <li>一键切换源码模式</li>
            <li>源码模式下支持编辑 / 保存 / 编码切换</li>
            <li>md 内相对图片按所在目录解析</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>内嵌原始 HTML 被转义显示为文本（防脚本执行）</li>
            <li>渲染输入上限 200 万字符，超出提示切源码模式</li>
            <li>渲染视图依赖 WebView2 运行时（缺失时自动进入源码模式）</li>
          </ul>
        </section>

        <hr />

        {/* 网页预览 */}
        <section id="web">
          <h2>网页预览</h2>
          <p>本地 HTML 文件以 WebView2 按原目录渲染。</p>
          <h3>支持格式</h3>
          <p>.html / .htm / .xhtml</p>
          <h3>功能</h3>
          <ul>
            <li>脚本启用，相对引用（./img、style.css）天然生效</li>
            <li>一键切换只读源码</li>
            <li>顶级导航与新窗口一律转交系统浏览器</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>预览不受信任的 HTML 等同于在浏览器中打开它</li>
            <li>文件所在目录映射进 WebView2 沙箱，映射随预览关闭即销毁</li>
            <li>文件名含 <code>#</code> / <code>?</code> 时渲染不可用，自动切源码模式</li>
          </ul>
        </section>

        <hr />

        {/* 音频 */}
        <section id="audio">
          <h2>音频播放</h2>
          <p>WebView2 自研播放页，支持多种常见音频格式。</p>
          <h3>支持格式</h3>
          <table>
            <thead><tr><th>支持</th><th>不支持</th></tr></thead>
            <tbody>
              <tr>
                <td>mp3, wav, flac, ogg (vorbis/opus), m4a (aac)</td>
                <td>WMA, AIFF, MIDI</td>
              </tr>
            </tbody>
          </table>
          <h3>功能</h3>
          <ul>
            <li>播放 / 暂停</li>
            <li>进度拖动、5 秒快进退</li>
            <li>音量控制、倍速播放、循环播放</li>
            <li>元数据展示（时长 / 大小 / 近似比特率）</li>
          </ul>
          <h3>使用边界</h3>
          <ul>
            <li>编解码能力取决于 WebView2 内核（Chromium）</li>
            <li>运行时缺失时降级为提示卡片（附十六进制入口）</li>
          </ul>
        </section>

        <hr />

        {/* PDF */}
        <section id="pdf">
          <h2>PDF 预览</h2>
          <p>使用 WebView2 / Chromium 内置 PDF 查看器。</p>
          <ul>
            <li>支持缩放与翻页</li>
            <li>只读预览</li>
            <li>运行时缺失时降级为提示卡片</li>
          </ul>
        </section>

        <hr />

        {/* 快捷键 */}
        <section id="shortcuts">
          <h2>快捷键</h2>
          <h3>预览操作</h3>
          <table>
            <thead><tr><th>按键</th><th>功能</th></tr></thead>
            <tbody>
              <tr><td><kbd>Space</kbd></td><td>资源管理器中打开 / 关闭预览</td></tr>
              <tr><td><kbd>Esc</kbd></td><td>关闭预览</td></tr>
              <tr><td><kbd>↑</kbd> / <kbd>↓</kbd></td><td>预览内切换上一个 / 下一个文件</td></tr>
            </tbody>
          </table>
          <h3>十六进制编辑器</h3>
          <table>
            <thead><tr><th>按键</th><th>功能</th></tr></thead>
            <tbody>
              <tr><td><kbd>0</kbd>-<kbd>9</kbd> <kbd>A</kbd>-<kbd>F</kbd></td><td>输入十六进制</td></tr>
              <tr><td><kbd>Tab</kbd></td><td>切换 Hex / ASCII 区</td></tr>
              <tr><td><kbd>Insert</kbd></td><td>切换覆盖 / 插入模式</td></tr>
              <tr><td><kbd>Delete</kbd> / <kbd>Backspace</kbd></td><td>删除字节</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>A</kbd></td><td>全选</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>C</kbd></td><td>复制 Hex 字符串</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd></td><td>复制 ASCII 文本</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>C</kbd></td><td>复制 C 数组</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Home</kbd> / <kbd>End</kbd></td><td>跳到文件头 / 尾</td></tr>
            </tbody>
          </table>
          <h3>文本编辑</h3>
          <table>
            <thead><tr><th>按键</th><th>功能</th></tr></thead>
            <tbody>
              <tr><td><kbd>Ctrl</kbd>+<kbd>S</kbd></td><td>保存</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd></td><td>另存为</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Z</kbd></td><td>撤销</td></tr>
              <tr><td><kbd>Ctrl</kbd>+<kbd>Y</kbd></td><td>重做</td></tr>
            </tbody>
          </table>
        </section>

        <hr />

        {/* 使用边界 */}
        <section id="limitations">
          <h2>使用边界</h2>
          <table>
            <thead><tr><th>类型</th><th>限制</th></tr></thead>
            <tbody>
              <tr><td>十六进制编辑器</td><td>1 GB 以内可流畅编辑；更大文件仍可查看（虚拟化滚动）</td></tr>
              <tr><td>文本预览/编辑</td><td>100 MB 以内自动加载；超过提示改用十六进制视图</td></tr>
              <tr><td>Office 预览</td><td>只读；Excel 每表上限 1 万行；PPTX 网页视图上限 8 MB</td></tr>
              <tr><td>PDF 预览</td><td>依赖 WebView2 运行时；只读</td></tr>
              <tr><td>Markdown</td><td>渲染上限 200 万字符；原始 HTML 被转义</td></tr>
              <tr><td>网页预览</td><td>脚本启用；文件名含 # / ? 时自动切源码</td></tr>
              <tr><td>音频</td><td>WMA / AIFF / MIDI 不支持</td></tr>
              <tr><td>图片</td><td>仅预览，编辑功能后续版本加入</td></tr>
            </tbody>
          </table>
        </section>

        <hr />

        {/* 技术栈 */}
        <section id="tech-stack">
          <h2>技术栈</h2>
          <table>
            <thead><tr><th>方向</th><th>选型</th></tr></thead>
            <tbody>
              <tr><td>框架</td><td>.NET 10（net10.0-windows）</td></tr>
              <tr><td>UI</td><td>WPF + MVVM（CommunityToolkit.Mvvm）</td></tr>
              <tr><td>文本编辑</td><td>AvalonEdit</td></tr>
              <tr><td>十六进制编辑器</td><td>自研控件（IScrollInfo 虚拟化滚动）</td></tr>
              <tr><td>Office 解析</td><td>DocumentFormat.OpenXml 3.5.1 + 自研 RTF / ODF 读取器</td></tr>
              <tr><td>Office 高保真渲染</td><td>WebView2 + mammoth.js 1.12.1 / SheetJS 0.20.3 / PPTXjs 1.21.1</td></tr>
              <tr><td>Markdown 渲染</td><td>Markdig 1.3.2 + 自研 GitHub 风格离线 CSS</td></tr>
              <tr><td>网页 / 音频 / PDF</td><td>WebView2（Chromium 内核）</td></tr>
              <tr><td>测试</td><td>xUnit</td></tr>
              <tr><td>发布</td><td>自包含发布（win-x64）</td></tr>
            </tbody>
          </table>
          <h3>项目结构</h3>
          <pre><code>{`See.Net/
├─ See.Net.slnx
├─ See.Net.Core/           # 核心逻辑
├─ See.Net/                # WPF 主应用
├─ See.Net.Tests/          # xUnit 单元测试
├─ packaging/
│  └─ assets/              # 应用图标资源
└─ scripts/
   └─ generate-assets.ps1`}</code></pre>
        </section>

        <hr />

        {/* 后续规划 */}
        <section id="roadmap">
          <h2>后续规划</h2>
          <ul>
            <li>最近打开与固定、多标签页</li>
            <li>文件内容全文搜索（正则）</li>
            <li>外部修改监控与提示</li>
            <li>深色主题、设置界面</li>
            <li>右键菜单「用 See.Net 打开」</li>
            <li>图片编辑</li>
          </ul>
        </section>

        <hr />

        {/* 常见问题 */}
        <section id="faq">
          <h2>常见问题</h2>
          <h4>自包含版本会占用更多空间吗？</h4>
          <p>大小约 184 MB，包含必要的运行时文件。好处是无需安装 .NET 运行时，解压即用。</p>
          <h4>我可以在多台计算机上使用同一个自包含版本吗？</h4>
          <p>可以，只需将整个文件夹复制到目标计算机即可运行。</p>
          <h4>如何卸载？</h4>
          <p>直接删除整个文件夹即可，不会在系统中留下残留。</p>
          <h4>WebView2 运行时缺失怎么办？</h4>
          <p>
            WebView2 通常已随 Windows 预装。如果缺失，Office 网页渲染视图、Markdown 渲染视图、音频播放、PDF 预览将降级，
            但结构化视图和文本编辑功能不受影响。可以从{' '}
            <a href="https://developer.microsoft.com/en-us/microsoft-edge/webview2/" target="_blank" rel="noopener">Microsoft 官网</a> 下载安装。
          </p>
          <h4>遇到安全软件阻止运行怎么办？</h4>
          <p>Windows Defender 或其他安全软件可能会阻止未知来源的程序。请将 See.exe 添加到安全软件的信任列表中。</p>
          <h4>支持哪些文件格式？</h4>
          <p>
            支持 30+ 种文件格式，包括文本/代码、图片、Office 文档、Markdown、HTML、音频、PDF 等。
            详见 <Link to="/#formats">支持格式</Link> 页面。
          </p>
        </section>
      </main>
    </div>
  )
}
