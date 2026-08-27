import { Link } from 'react-router-dom'
import './Footer.css'

export default function Footer() {
  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-content">
          <div className="footer-brand">
            <span className="footer-logo-icon">👁</span>
            <span className="footer-logo-text">See<span className="logo-dot">.</span>Net</span>
            <p className="footer-tagline">Windows 文件快速预览工具</p>
          </div>
          <div className="footer-links">
            <div className="footer-group">
              <h5>项目</h5>
              <a href="https://github.com/pengcunfu/See.Net" target="_blank" rel="noopener">GitHub</a>
              <a href="https://github.com/pengcunfu/See.Net/releases" target="_blank" rel="noopener">下载</a>
              <a href="https://github.com/pengcunfu/See.Net/issues" target="_blank" rel="noopener">反馈</a>
            </div>
            <div className="footer-group">
              <h5>文档</h5>
              <Link to="/docs">使用指南</Link>
              <Link to="/docs#installation">安装说明</Link>
              <Link to="/docs#shortcuts">快捷键</Link>
            </div>
          </div>
        </div>
        <div className="footer-bottom">
          <p>&copy; 2026 See.Net. Released under the MIT License.</p>
        </div>
      </div>
    </footer>
  )
}
