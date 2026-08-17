# See.Net 证书安装助手
# 用于安装测试证书以解决MSIX包签名验证问题

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  See.Net 测试证书安装助手" -ForegroundColor Cyan  
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

 = "D:\Projects\DevTools\See.Net\packaging\See.Net_TestCert.pfx"
 = "See.Net123"

if (-not (Test-Path )) {
    Write-Host "❌ 错误：找不到证书文件" -ForegroundColor Red
    Write-Host "请确保证书文件存在于：" -ForegroundColor Yellow
    Read-Host "按回车键退出"
    exit 1
}

Write-Host "📋 证书信息：" -ForegroundColor Green
Write-Host "文件路径：" -ForegroundColor White
Write-Host "证书密码：" -ForegroundColor Yellow
Write-Host ""

Write-Host "⚠️  注意事项：" -ForegroundColor Yellow
Write-Host "1. 此为测试证书，仅用于开发和测试" -ForegroundColor White
Write-Host "2. 生产环境应使用正式的代码签名证书" -ForegroundColor White
Write-Host "3. 安装后需要重启计算机才能生效" -ForegroundColor White
Write-Host ""

 = Read-Host "是否继续安装？(Y/N)"
if ( -ne "Y" -and  -ne "y") {
    Write-Host "已取消安装" -ForegroundColor Yellow
    Read-Host "按回车键退出"
    exit 0
}

Write-Host ""
Write-Host "🔄 正在安装证书..." -ForegroundColor Green

try {
     = ConvertTo-SecureString -String  -Force -AsPlainText
     = Import-PfxCertificate -FilePath  -CertStoreLocation "Cert:\LocalMachine\Root" -Password 
    
    Write-Host "✅ 证书安装成功！" -ForegroundColor Green
    Write-Host "证书主题：" -ForegroundColor Cyan
    Write-Host "  " .Subject -ForegroundColor White
    Write-Host "证书有效期：" -ForegroundColor Cyan
    Write-Host "  " .NotBefore " 至 " .NotAfter -ForegroundColor White
    Write-Host ""
    Write-Host "📝 下一步操作：" -ForegroundColor Green
    Write-Host "1. 重启计算机" -ForegroundColor White
    Write-Host "2. 重启后可以正常安装MSIX包" -ForegroundColor White
    Write-Host "3. 如果仍有问题，请检查Windows开发者选项设置" -ForegroundColor White
    
} catch {
    Write-Host "❌ 证书安装失败：" -ForegroundColor Red
    Write-Host .Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 可能的解决方案：" -ForegroundColor Yellow
    Write-Host "1. 以管理员身份运行此脚本" -ForegroundColor White
    Write-Host "2. 检查证书文件是否损坏" -ForegroundColor White
    Write-Host "3. 确认密码输入正确" -ForegroundColor White
}

Write-Host ""
Read-Host "按回车键退出"
