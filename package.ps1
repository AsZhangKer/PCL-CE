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