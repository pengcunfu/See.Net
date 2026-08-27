import { useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import './Home.css'

/* ── SVG Icons ── */
const IconDownload = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
    <polyline points="7 10 12 15 17 10" />
    <line x1="12" y1="15" x2="12" y2="3" />
  </svg>
)

const IconArrowDown = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="M12 5v14M5 12l7 7 7-7" />
  </svg>
)

const IconMonitor = () => (
  <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
    <line x1="8" y1="21" x2="16" y2="21" />
    <line x1="12" y1="17" x2="12" y2="21" />
  </svg>
)

const IconFile = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
    <polyline points="14 2 14 8 20 8" />
    <line x1="16" y1="13" x2="8" y2="13" />
    <line x1="16" y1="17" x2="8" y2="17" />
    <polyline points="10 9 9 9 8 9" />
  </svg>
)

const IconImage = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
    <circle cx="8.5" cy="8.5" r="1.5" />
    <polyline points="21 15 16 10 5 21" />
  </svg>
)

const IconOffice = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
    <polyline points="14 2 14 8 20 8" />
    <path d="M8 13h2" /><path d="M8 17h2" />
    <path d="M14 13h2" /><path d="M14 17h2" />
  </svg>
)

const IconEdit = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M12 20h9" />
    <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
  </svg>
)

const IconAudio = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
    <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
    <path d="M19.07 4.93a10 10 0 0 1 0 14.14" />
  </svg>
)

const IconBook = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
    <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
  </svg>
)

const IconGlobe = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <circle cx="12" cy="12" r="10" />
    <line x1="2" y1="12" x2="22" y2="12" />
    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
  </svg>
)

const IconPDF = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
    <polyline points="14 2 14 8 20 8" />
    <line x1="12" y1="18" x2="12" y2="12" />
    <line x1="9" y1="15" x2="15" y2="15" />
  </svg>
)

const IconShield = () => (
  <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </svg>
)

const IconBox = () => (
  <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
    <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
    <line x1="12" y1="22.08" x2="12" y2="12" />
  </svg>
)

const IconTerminal = () => (
  <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <polyline points="4 17 10 11 4 5" />
    <line x1="12" y1="19" x2="20" y2="19" />
  </svg>
)

const IconCheck = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    <polyline points="20 6 9 17 4 12" />
  </svg>
)

const IconCode = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <polyline points="16 18 22 12 16 6" />
    <polyline points="8 6 2 12 8 18" />
  </svg>
)

const IconFolderFile = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
    <polyline points="14 2 14 8 20 8" />
  </svg>
)

/* ── Data ── */
const features = [
  { icon: <IconFile />, title: '文本 & 代码', desc: '语法高亮、编码识别（UTF-8/UTF-16/GB18030 等）、编辑模式、撤销重做、保存。支持自定义字体字号。' },
  { icon: <IconImage />, title: '图片预览', desc: 'PNG、JPG、BMP、GIF、WebP、SVG 等常见格式直接预览，支持适应窗口、实际大小、自由缩放。' },
  { icon: <IconOffice />, title: 'Office 文档', desc: 'docx/xlsx/pptx 双引擎预览：结构化视图秒开，网页渲染视图高保真排版。支持 RTF、ODF 格式。' },
  { icon: <IconEdit />, title: '十六进制编辑', desc: '自研控件，三栏布局（偏移/Hex/ASCII），支持字节编辑、偏移跳转、Hex 搜索、多格式复制。' },
  { icon: <IconAudio />, title: '音频播放', desc: 'mp3/wav/flac/ogg/m4a 等格式播放，支持进度拖动、5 秒快进退、音量、倍速、循环。' },
  { icon: <IconBook />, title: 'Markdown', desc: 'GitHub 风格渲染，支持表格、任务列表、围栏代码、删除线。一键切换源码模式，编辑/保存/编码切换全套可用。' },
  { icon: <IconGlobe />, title: '网页预览', desc: '本地 HTML 以 WebView2 按原目录渲染，脚本启用，相对引用天然生效。一键切换只读源码。' },
  { icon: <IconPDF />, title: 'PDF 预览', desc: 'WebView2/Chromium 内置 PDF 查看器，支持缩放与翻页。运行时缺失时降级为提示卡片。' },
]

const formatGroups = [
  {
    title: '文本 & 代码',
    icon: <IconFolderFile />,
    tags: ['.txt', '.cs', '.py', '.js', '.ts', '.html', '.css', '.json', '.xml', '.yaml', '.toml', '.md', '.log', '.sql', '.sh', '.bat', '.ini', '.cfg', '更多...'],
  },
  {
    title: '图片',
    icon: <IconImage />,
    tags: ['.png', '.jpg', '.jpeg', '.bmp', '.gif', '.webp', '.svg'],
  },
  {
    title: 'Office',
    icon: <IconOffice />,
    tags: ['.docx', '.xlsx', '.pptx', '.docm', '.xlsm', '.pptm', '.rtf', '.odt', '.ods', '.odp', '.xls'],
  },
  {
    title: '媒体 & 其他',
    icon: <IconAudio />,
    tags: ['.mp3', '.wav', '.flac', '.ogg', '.m4a', '.aac', '.pdf', '.html', '.htm', '任意二进制'],
  },
]

const previewShortcuts = [
  { keys: ['Space'], desc: '打开 / 关闭预览' },
  { keys: ['Esc'], desc: '关闭预览' },
  { keys: ['↑', '↓'], desc: '切换上一个 / 下一个文件' },
]

const hexShortcuts = [
  { keys: ['0-9', 'A-F'], desc: '输入十六进制' },
  { keys: ['Tab'], desc: '切换 Hex / ASCII 区' },
  { keys: ['Insert'], desc: '切换覆盖 / 插入模式' },
  { keys: ['Ctrl', 'C'], desc: '复制 Hex 字符串' },
  { keys: ['Ctrl', 'Shift', 'C'], desc: '复制 ASCII 文本' },
  { keys: ['Ctrl', 'Home/End'], desc: '跳到文件头 / 尾' },
]

const techStack = [
  { name: '.NET 10', desc: '最新运行时' },
  { name: 'WPF', desc: 'Windows 原生 UI' },
  { name: 'MVVM', desc: 'CommunityToolkit' },
  { name: 'AvalonEdit', desc: '文本编辑器' },
  { name: 'WebView2', desc: 'Chromium 渲染' },
  { name: 'Open XML', desc: 'Office 解析' },
  { name: 'Markdig', desc: 'Markdown 引擎' },
  { name: 'xUnit', desc: '单元测试' },
]

/* ── Intersection Observer Hook ── */
function useAnimateIn() {
  const ref = useRef(null)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const obs = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          el.classList.add('animate-in')
          obs.unobserve(el)
        }
      },
      { threshold: 0.1, rootMargin: '0px 0px -40px 0px' }
    )
    obs.observe(el)
    return () => obs.disconnect()
  }, [])
  return ref
}

function AnimatedSection({ children, className = '', ...props }) {
  const ref = useAnimateIn()
  return (
    <div ref={ref} className={className} {...props}>
      {children}
    </div>
  )
}

/* ── Preview Mockup ── */
function PreviewMockup() {
  const lines = [
    { ln: 1, content: <><span className="mk-kw">using</span> System;</> },
    { ln: 2, content: null },
    { ln: 3, content: <><span className="mk-kw">namespace</span> <span className="mk-ty">Demo</span></> },
    { ln: 4, content: '{' },
    { ln: 5, content: <>  <span className="mk-kw">public class</span> <span className="mk-ty">Program</span></> },
    { ln: 6, content: '  {' },
    { ln: 7, content: <>    <span className="mk-kw">static void</span> <span className="mk-fn">Main</span>()</> },
    { ln: 8, content: '    {' },
    { ln: 9, content: <>      Console.<span className="mk-fn">WriteLine</span>(<span className="mk-st">"Hello!"</span>);</> },
    { ln: 10, content: '    }' },
    { ln: 11, content: '  }' },
    { ln: 12, content: '}' },
  ]

  return (
    <div className="preview-mockup">
      <div className="mockup-titlebar">
        <span className="mockup-dot red" />
        <span className="mockup-dot yellow" />
        <span className="mockup-dot green" />
        <span className="mockup-title">preview.cs — See.Net</span>
      </div>
      <div className="mockup-body">
        {lines.map(l => (
          <div className="mockup-line" key={l.ln}>
            <span className="mk-ln">{l.ln}</span>
            {l.content}
          </div>
        ))}
      </div>
    </div>
  )
}

/* ── Page ── */
export default function Home() {
  return (
    <>
      {/* Hero */}
      <header className="hero" id="hero">
        <div className="hero-bg">
          <div className="hero-gradient" />
          <div className="hero-grid" />
        </div>
        <div className="container hero-content">
          <div className="hero-badge">.NET 10 · WPF · 开源免费</div>
          <h1 className="hero-title">
            按一下空格<br />
            <span className="gradient-text">秒开任意文件</span>
          </h1>
          <p className="hero-desc">
            See.Net 是一款 Windows 桌面文件预览与编辑工具，<br className="hide-mobile" />
            在资源管理器中选中文件按空格即可快速预览，支持代码、Office、图片、音视频等数十种格式。
          </p>
          <div className="hero-actions">
            <a href="#download" className="btn btn-primary btn-lg">
              <IconDownload /> 免费下载
            </a>
            <Link to="/docs" className="btn btn-outline btn-lg">查看文档</Link>
          </div>
          <div className="hero-stats">
            <div className="stat">
              <span className="stat-value">30+</span>
              <span className="stat-label">支持格式</span>
            </div>
            <div className="stat">
              <span className="stat-value">&lt;1s</span>
              <span className="stat-label">预览速度</span>
            </div>
            <div className="stat">
              <span className="stat-value">1GB</span>
              <span className="stat-label">可编辑上限</span>
            </div>
            <div className="stat">
              <span className="stat-value">MIT</span>
              <span className="stat-label">开源协议</span>
            </div>
          </div>
        </div>
        <div className="hero-scroll-hint">
          <IconArrowDown />
        </div>
      </header>

      {/* Features */}
      <section className="section" id="features">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">核心功能</h2>
            <p className="section-desc">像 macOS Quick Look 一样，在 Windows 上按空格预览文件</p>
          </div>

          {/* 主功能 */}
          <AnimatedSection className="feature-hero">
            <div className="feature-hero-content">
              <div className="feature-icon-lg"><IconMonitor /></div>
              <h3>空格快速预览</h3>
              <p>
                在资源管理器中选中任意文件，按下空格键即可弹出预览浮窗。
                再按空格或 Esc 关闭，方向键 ↑↓ 切换多选文件。
                无需启动完整应用，真正零等待的文件查看体验。
              </p>
            </div>
            <div className="feature-hero-visual">
              <PreviewMockup />
            </div>
          </AnimatedSection>

          {/* 功能网格 */}
          <div className="features-grid">
            {features.map((f, i) => (
              <AnimatedSection className="feature-card" key={i}>
                <div className="feature-icon">{f.icon}</div>
                <h4>{f.title}</h4>
                <p>{f.desc}</p>
              </AnimatedSection>
            ))}
          </div>
        </div>
      </section>

      {/* Safety */}
      <section className="section section-alt" id="safety">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">安全可靠</h2>
            <p className="section-desc">编辑不丢数据，大文件不卡顿</p>
          </div>
          <div className="safety-grid">
            <AnimatedSection className="safety-card">
              <div className="safety-icon"><IconShield /></div>
              <h4>原子保存</h4>
              <p>保存走临时文件 + 原子替换，保存前自动备份到用户文档目录，崩溃也不损坏原文件。</p>
            </AnimatedSection>
            <AnimatedSection className="safety-card">
              <div className="safety-icon"><IconBox /></div>
              <h4>大文件支持</h4>
              <p>十六进制视图窗口化读取，内存占用与文件大小无关。1 GB 以内文件可流畅编辑。</p>
            </AnimatedSection>
            <AnimatedSection className="safety-card">
              <div className="safety-icon"><IconTerminal /></div>
              <h4>命令行集成</h4>
              <p>支持 <code>See.exe &lt;file&gt;</code> 命令行打开，系统托盘常驻，随 Windows 启动可选。</p>
            </AnimatedSection>
          </div>
        </div>
      </section>

      {/* Formats */}
      <section className="section" id="formats">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">支持的格式</h2>
            <p className="section-desc">覆盖日常开发与办公场景的主流文件类型</p>
          </div>
          <div className="formats-grid">
            {formatGroups.map((g, i) => (
              <AnimatedSection className="format-group" key={i}>
                <h4>{g.icon}{g.title}</h4>
                <div className="format-tags">
                  {g.tags.map(t => <span className="tag" key={t}>{t}</span>)}
                </div>
              </AnimatedSection>
            ))}
          </div>
        </div>
      </section>

      {/* Shortcuts */}
      <section className="section section-alt" id="shortcuts">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">快捷键</h2>
            <p className="section-desc">键盘操作，高效预览</p>
          </div>
          <div className="shortcuts-grid">
            <AnimatedSection className="shortcut-group">
              <h4>预览操作</h4>
              <div className="shortcut-list">
                {previewShortcuts.map((s, i) => (
                  <div className="shortcut-item" key={i}>
                    {s.keys.map(k => <kbd key={k}>{k}</kbd>)}
                    <span>{s.desc}</span>
                  </div>
                ))}
              </div>
            </AnimatedSection>
            <AnimatedSection className="shortcut-group">
              <h4>十六进制编辑器</h4>
              <div className="shortcut-list">
                {hexShortcuts.map((s, i) => (
                  <div className="shortcut-item" key={i}>
                    {s.keys.map(k => <kbd key={k}>{k}</kbd>)}
                    <span>{s.desc}</span>
                  </div>
                ))}
              </div>
            </AnimatedSection>
          </div>
        </div>
      </section>

      {/* Tech */}
      <section className="section" id="tech">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">技术栈</h2>
            <p className="section-desc">现代 .NET 生态，性能与体验兼得</p>
          </div>
          <div className="tech-grid">
            {techStack.map((t, i) => (
              <AnimatedSection className="tech-card" key={i}>
                <div className="tech-name">{t.name}</div>
                <div className="tech-desc">{t.desc}</div>
              </AnimatedSection>
            ))}
          </div>
        </div>
      </section>

      {/* Download */}
      <section className="section section-alt" id="download">
        <div className="container">
          <div className="section-header">
            <h2 className="section-title">下载安装</h2>
            <p className="section-desc">开箱即用，无需安装运行时</p>
          </div>
          <div className="download-cards">
            <AnimatedSection className="download-card download-card-primary">
              <div className="download-card-header">
                <IconDownload />
                <h3>自包含版本</h3>
                <p>解压即用，无需 .NET 运行时</p>
              </div>
              <ul className="download-features">
                <li><IconCheck /> Windows 10 / 11 (64-bit)</li>
                <li><IconCheck /> 约 184 MB，含全部运行时</li>
                <li><IconCheck /> 解压后双击 See.exe 即可运行</li>
                <li><IconCheck /> 删除文件夹即完成卸载</li>
              </ul>
              <a href="https://github.com/pengcunfu/See.Net/releases" className="btn btn-primary btn-block" target="_blank" rel="noopener">
                前往 GitHub Releases 下载
              </a>
            </AnimatedSection>

            <AnimatedSection className="download-card">
              <div className="download-card-header">
                <IconCode />
                <h3>从源码构建</h3>
                <p>开发者自编译</p>
              </div>
              <div className="code-block">
                <pre><code>{`# 克隆仓库
git clone https://github.com/pengcunfu/See.Net.git
cd See.Net

# 构建
dotnet build See.Net.slnx -c Release

# 运行
dotnet run --project See.Net`}</code></pre>
              </div>
            </AnimatedSection>
          </div>

          <AnimatedSection className="system-requirements">
            <h4>系统要求</h4>
            <div className="req-grid">
              <div className="req-item">
                <span className="req-label">操作系统</span>
                <span className="req-value">Windows 10 或更高版本 (64-bit)</span>
              </div>
              <div className="req-item">
                <span className="req-label">内存</span>
                <span className="req-value">4 GB 或更多</span>
              </div>
              <div className="req-item">
                <span className="req-label">磁盘空间</span>
                <span className="req-value">500 MB 可用空间</span>
              </div>
              <div className="req-item">
                <span className="req-label">WebView2</span>
                <span className="req-value">通常已预装（Office/PDF/音频预览需要）</span>
              </div>
            </div>
          </AnimatedSection>
        </div>
      </section>
    </>
  )
}
