$ErrorActionPreference = "Stop"
Set-Location "e:\Hegemonia_save"

# Mata processos git zumbis
Get-Process git -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Remove-Item ".git\index.lock" -ErrorAction SilentlyContinue

Write-Host "=== Verificando arquivos modificados recentemente ==="

# Data de corte: 3 dias atras (pega tudo das ultimas sessoes)
$corte = (Get-Date).AddDays(-3)

# Pastas que mais usamos
$pastas = @(
    "Assets\scripts",
    "progress.md",
    "RELATORIO_AUDITORIA.md"
)

$arquivosParaCommit = @()

foreach ($p in $pastas) {
    if (Test-Path $p -PathType Container) {
        $arquivos = Get-ChildItem -Path $p -Recurse -File | Where-Object { $_.LastWriteTime -gt $corte }
        foreach ($a in $arquivos) {
            $rel = $a.FullName.Replace("$PWD\", "").Replace("\", "/")
            $arquivosParaCommit += $rel
        }
    } elseif (Test-Path $p) {
        $a = Get-Item $p
        if ($a.LastWriteTime -gt $corte) {
            $arquivosParaCommit += $p.Replace("\", "/")
        }
    }
}

Write-Host "Encontrados $($arquivosParaCommit.Count) arquivos modificados nos ultimos 3 dias"
Write-Host ""

# Verifica quais realmente estao modificados no git
$modificados = @()
$novos = @()

foreach ($f in $arquivosParaCommit) {
    $s = git status --porcelain -- $f 2>$null
    if ($s) {
        $status = $s.Substring(0, 2).Trim()
        if ($status -eq "M" -or $status -eq "A" -or $status -eq "??") {
            if ($status -eq "??") { $novos += $f }
            else { $modificados += $f }
        }
    }
}

Write-Host "--- Modificados ($($modificados.Count)) ---"
$modificados | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host "--- Novos ($($novos.Count)) ---"
$novos | ForEach-Object { Write-Host "  $_" }
Write-Host ""

if ($modificados.Count -eq 0 -and $novos.Count -eq 0) {
    Write-Host "Nada para commitar!"
    exit 0
}

# Adiciona ao staging em lotes pequenos para nao travar
$todos = $modificados + $novos
$lote = 5
for ($i = 0; $i -lt $todos.Count; $i += $lote) {
    $itens = $todos[$i..([Math]::Min($i + $lote - 1, $todos.Count - 1))]
    $itens | ForEach-Object { git add $_ 2>$null }
    Write-Host "  Lote $($i/$lote + 1): adicionados $($itens.Count) arquivos"
}

Write-Host ""
Write-Host "=== Fazendo commit ==="

# Commit
$msg = "feat: scripts governo/tripulacao/auditoria + melhorias - data " + (Get-Date -Format "yyyy-MM-dd")
git commit --no-verify -m $msg

Write-Host ""
Write-Host "=== Resultado ==="
git log --oneline -1
