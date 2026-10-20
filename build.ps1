param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

# Load local secrets (ClientID / API key, etc.)
# The Source Generator reads process environment variables at compile time, so .env.local
# must be imported before dotnet build. The build server is shut down first, otherwise
# the resident compiler keeps its old environment and the injection silently fails.
if (Test-Path -LiteralPath ".env.local") {
    Invoke-Expression -Command (Get-Content -LiteralPath ".env.local" -Raw)
}
dotnet build-server shutdown 2>&1 | Out-Null

dotnet build PCL2-CE.sln -c $Configuration -p:Platform=$Platform
