// Pre-build: clean build artifacts from docs/
// Post-build: copy original docs files back
import { copyFileSync, existsSync, rmSync, readdirSync } from 'fs'
import { resolve, join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const root = resolve(__dirname, '../../')
const docsDir = join(root, 'docs')

// Clean build artifacts (keep original docs files)
const cleanPatterns = ['assets', 'index.html', 'favicon.svg', 'icons.svg']
for (const name of cleanPatterns) {
  const target = join(docsDir, name)
  if (existsSync(target)) {
    rmSync(target, { recursive: true, force: true })
  }
}
console.log('cleaned build artifacts from docs/')
