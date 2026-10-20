$targets = @(".\artifact")

Get-ChildItem -Path . -Recurse -Directory -Include "bin", "obj" | ForEach-Object {
    $targets += $_.FullName
}

foreach ($target in $targets) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "已删除: $target"
    }
}