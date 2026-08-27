import { useState, useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import './Navbar.css'

export default function Navbar() {
  const [scrolled, setScrolled] = useState(false)
  const [open, setOpen] = useState(false)
  const { pathname } = useLocation()

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 20)
    window.addEventListener('scroll', onScroll, { passive: true })
    onScroll()
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  useEffect(() => {
    setOpen(false)
  }, [pathname])

  return (
    <nav className={`navbar${scrolled ? ' scrolled' : ''}`}>
      <div className="container nav-content">
        <Link to="/" className="nav-logo">
          <span className="logo-icon">👁</span>
          <span className="logo-text">See<span className="logo-dot">.</span>Net</span>
        </Link>

        <div className={`nav-links${open ? ' active' : ''}`}>
          <a href="/#features">功能</a>
          <a href="/#formats">格式</a>
          <a href="/#shortcuts">快捷键</a>
          <a href="/#tech">技术</a>
          <Link to="/docs">文档</Link>
          <a href="https://github.com/pengcunfu/See.Net" target="_blank" rel="noopener">GitHub</a>
        </div>

        <a href="/#download" className="btn btn-primary nav-cta">下载</a>

        <button
          className={`nav-toggle${open ? ' active' : ''}`}
          onClick={() => setOpen(!open)}
          aria-label="菜单"
        >
          <span /><span /><span />
        </button>
      </div>
    </nav>
  )
}
