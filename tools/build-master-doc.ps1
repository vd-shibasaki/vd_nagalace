# docs/01〜14 を連結して NAGARACE_MASTER_REQUIREMENTS.md を再生成する（正本は docs/ 側）
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'NAGARACE_MASTER_REQUIREMENTS.md'
$parts = @("# ながレース — 統合要件定義・開発仕様書", "", "> 自動生成ファイル。直接編集せず docs/ を編集して tools/build-master-doc.ps1 で再生成すること。", "")
Get-ChildItem (Join-Path $root 'docs') -Filter '*.md' | Sort-Object Name | ForEach-Object {
    $parts += [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8).TrimEnd()
    $parts += ""
    $parts += "---"
    $parts += ""
}
[System.IO.File]::WriteAllText($out, ($parts -join "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "generated: $out"
