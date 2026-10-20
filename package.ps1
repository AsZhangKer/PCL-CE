param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Output = ".\artifact"
)

if (Test-Path -LiteralPath ".env.local") {
    Invoke-Expression -Command (Get-Content -LiteralPath ".env.local" -Raw)
}

dotnet build-server shutdown 2>&1 | Out-Null
Remove-Item -Recurse -Force "PCL.Core\obj" -ErrorAction SilentlyContinue

dotnet publish "Plain Craft Launcher 2\Plain Craft Launcher 2.csproj" `
    -p:Configuration=$Configuration `
    -p:Platform=$Platform `
    -p:DeleteExistingFiles=true `
    -o $Output `
    --no-self-contained

# 程序集名必须保持 "Plain Craft Launcher 2"（XAML 里的 pack URI 靠它定位图片/图标资源），
# 因此只在发布后把 exe 改名为对外使用的 "PCL Ker.exe"。
$publishedExe = Join-Path $Output "Plain Craft Launcher 2.exe"
$targetExe = Join-Path $Output "PCL Ker.exe"
if (Test-Path -LiteralPath $publishedExe) {
    if (Test-Path -LiteralPath $targetExe) {
        Remove-Item -LiteralPath $targetExe -Force
    }
    Rename-Item -LiteralPath $publishedExe -NewName "PCL Ker.exe"
    Write-Host "已重命名输出: $targetExe"
}
