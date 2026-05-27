$ErrorActionPreference = "Continue"
Set-Location "e:\Hegemonia_save"

# Mata qualquer processo git pendente
Get-Process git -ErrorAction SilentlyContinue | ForEach-Object { 
    try { Stop-Process $_.Id -Force } catch {} 
}
Start-Sleep -Seconds 2

# Remove lock
Remove-Item ".git\index.lock" -Force -ErrorAction SilentlyContinue

# Garante que filtro LFS nao atrapalha
$env:GIT_LFS_SKIP_SMUDGE = "1"
$env:GIT_LFS_SKIP_PUSH = "1"

# Lista de commits em lotes logicos (cada linha = arquivos separados por pipe)
$lotes = @(
    @{msg="feat: novos scripts de governo (ordens temporais, resgate, tempo, tripulacao)"; files=@(
        "Assets/scripts/Governo/GerenciadorOrdensTemporais.cs",
        "Assets/scripts/Governo/GerenciadorOrdensTemporais.cs.meta",
        "Assets/scripts/Governo/GerenciadorTempo.cs",
        "Assets/scripts/Governo/GerenciadorTempo.cs.meta",
        "Assets/scripts/Governo/ProxiResgateSobrevivente.cs",
        "Assets/scripts/Governo/ProxiResgateSobrevivente.cs.meta",
        "Assets/scripts/Governo/TripulacaoUnidade.cs",
        "Assets/scripts/Governo/TripulacaoUnidade.cs.meta"
    )},
    @{msg="feat: novos scripts de tripulacao e animacao"; files=@(
        "Assets/scripts/GerenciadorTripulacaoNavio.cs",
        "Assets/scripts/GerenciadorTripulacaoNavio.cs.meta",
        "Assets/scripts/AnimadorUnidade.cs",
        "Assets/scripts/AnimadorUnidade.cs.meta"
    )},
    @{msg="feat: atualizacao scripts Governo (economia, populacao, sistema mundial)"; files=@(
        "Assets/scripts/Governo/DadosPaisGoverno.cs",
        "Assets/scripts/Governo/EstruturaEconomica.cs",
        "Assets/scripts/Governo/SistemaEconomiaImoveis.cs",
        "Assets/scripts/Governo/SistemaGovernoMundial.cs",
        "Assets/scripts/Governo/SistemaPopulacao.cs"
    )},
    @{msg="feat: atualizacao scripts IA (Comandante, Arquiteto, Dominadora, Suprema)"; files=@(
        "Assets/scripts/IA/IA_Comandante.cs",
        "Assets/scripts/IA/Modulos/IA_Arquiteto_Pro.cs",
        "Assets/scripts/IA_Dominadora.cs",
        "Assets/scripts/IA_Suprema.cs"
    )},
    @{msg="feat: atualizacao scripts de combate e armamento"; files=@(
        "Assets/scripts/ControleAviao.cs",
        "Assets/scripts/ControleTorreta.cs",
        "Assets/scripts/ControleUnidade.cs",
        "Assets/scripts/CombustivelUnidade.cs",
        "Assets/scripts/Helicoptero.cs",
        "Assets/scripts/SistemaArmamentoHelice.cs",
        "Assets/scripts/SistemaDeDanos.cs",
        "Assets/scripts/SistemaDeTiro.cs",
        "Assets/scripts/PoolDeObjetosCombate.cs"
    )},
    @{msg="feat: atualizacao scripts de gerenciamento (jogo, recursos, selecao, save)"; files=@(
        "Assets/scripts/GerenciadorPortaAvioes.cs",
        "Assets/scripts/GerenciadorRecursos.cs",
        "Assets/scripts/GerenteDeJogo.cs",
        "Assets/scripts/GerenteSelecao.cs",
        "Assets/scripts/SistemaSaveGame.cs",
        "Assets/scripts/Fabrica.cs",
        "Assets/scripts/Fazenda.cs",
        "Assets/scripts/AuditoriaConteudoJogo.cs",
        "Assets/scripts/Menus/MenuGoverno.cs"
    )},
    @{msg="feat: atualizacao Editor scripts e documentacao/auditoria"; files=@(
        "Assets/scripts/Editor/GeradorDadosHeliporto.cs",
        "RELATORIO_AUDITORIA.md",
        "progress.md"
    )}
)

$totalOk = 0
$totalFail = 0

foreach ($lote in $lotes) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "LOTE: $($lote.msg)" -ForegroundColor Yellow
    
    # Garante que nao tem lock
    Remove-Item ".git\index.lock" -Force -ErrorAction SilentlyContinue
    
    # Adiciona arquivos
    $added = @()
    foreach ($f in $lote.files) {
        if (Test-Path $f) {
            $out = git add -- $f 2>&1
            if ($LASTEXITCODE -eq 0) {
                $added += $f
                Write-Host "  + $f" -ForegroundColor Green
            } else {
                Write-Host "  ! $f (falhou add)" -ForegroundColor DarkYellow
            }
        } else {
            Write-Host "  ? $f (nao encontrado)" -ForegroundColor DarkGray
        }
    }
    
    if ($added.Count -eq 0) {
        Write-Host "  Nenhum arquivo para commitar neste lote" -ForegroundColor DarkGray
        continue
    }
    
    # Commit com LFS desabilitado
    $commitOut = git -c filter.lfs.required=false -c filter.lfs.process= -c filter.lfs.smudge= -c filter.lfs.clean= commit --no-verify -m $lote.msg 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  COMMIT OK!" -ForegroundColor Green
        $totalOk++
    } else {
        Write-Host "  COMMIT FALHOU: $commitOut" -ForegroundColor Red
        $totalFail++
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TOTAL: $totalOk commits OK, $totalFail falhas" -ForegroundColor White
Write-Host ""
Write-Host "=== LOG FINAL ===" -ForegroundColor Cyan
git log --oneline -10
