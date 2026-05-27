$rootPath = $PSScriptRoot
$assetsPath = Join-Path $rootPath "Assets"

# Função para decodificar caracteres escapados como \xE1 ou \u00e1
function Decode-EscapedChars($str) {
    if ([string]::IsNullOrEmpty($str)) { return "" }
    # Trata escapes de unicode de 4 digitos (\u00e1)
    $str = [System.Text.RegularExpressions.Regex]::Replace($str, '\\u([0-9a-fA-F]{4})', {
        param($m)
        [char][int]("0x" + $m.Groups[1].Value)
    })
    # Trata escapes hexadecimais de 2 digitos (\xE1)
    $str = [System.Text.RegularExpressions.Regex]::Replace($str, '\\x([0-9a-fA-F]{2})', {
        param($m)
        [char][int]("0x" + $m.Groups[1].Value)
    })
    return $str
}

Write-Host "Mapeando GUIDs..."
$guidToPath = @{}
Get-ChildItem -Path $assetsPath -Recurse -Filter "*.meta" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding utf8
    if ($content -match "guid:\s*([a-fA-F0-9]+)") {
        $guid = $Matches[1]
        $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5) # Remove .meta
        $guidToPath[$guid] = $assetPath
    }
}

Write-Host ("Buscando fichas de constru" + [char]0xE7 + [char]0xE3 + "o...")
$dadosConstrucaoGuid = "01adaf1ed705a7f4aa6f385d271a343f"
$fichas = @()

Get-ChildItem -Path $assetsPath -Recurse -Filter "*.asset" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding utf8
    if ($content -contains $dadosConstrucaoGuid -or $content.Contains($dadosConstrucaoGuid)) {
        $nomeItem = ""
        $descricao = ""
        $preco = 0
        $categoria = -1
        $prefabName = "Nenhum"
        
        # Parse nomeItem
        if ($content -match "nomeItem:\s*['`"]?(.*?)['`"]?\s*(?:\r?\n|$)") {
            $nomeItem = Decode-EscapedChars $Matches[1].Trim()
        }
        
        # Parse descricao (handle multiline/quoted/simple)
        if ($content -match "descricao:\s*\|-\r?\n\s+(.*)") {
            # Multiline
            $lines = $content -split "\r?\n"
            $descLines = @()
            $inDesc = $false
            foreach ($line in $lines) {
                if ($line -match "descricao:\s*\|-") {
                    $inDesc = $true
                    continue
                }
                if ($inDesc) {
                    if ($line -match "^\s+(.*)") {
                        $descLines += $Matches[1]
                    } else {
                        break
                    }
                }
            }
            $descricao = Decode-EscapedChars ($descLines -join " ")
        } elseif ($content -match "descricao:\s*'([^']*)'") {
            $descricao = Decode-EscapedChars $Matches[1].Trim()
        } elseif ($content -match "descricao:\s*`"([^`"]*)`"") {
            $descricao = Decode-EscapedChars $Matches[1].Trim()
        } elseif ($content -match "descricao:\s*(.*?)\s*(?:\r?\n|$)") {
            $descricao = Decode-EscapedChars $Matches[1].Trim()
        }
        
        # Parse preco
        if ($content -match "preco:\s*(\d+)") {
            $preco = [int]$Matches[1]
        }
        
        # Parse categoria
        if ($content -match "categoria:\s*(\d+)") {
            $categoria = [int]$Matches[1]
        }
        
        # Parse prefabDaUnidade
        if ($content -match "prefabDaUnidade:\s*\{\s*fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+)") {
            $prefabGuid = $Matches[1]
            if ($guidToPath.ContainsKey($prefabGuid)) {
                $prefabName = Split-Path $guidToPath[$prefabGuid] -Leaf
            }
        }

        if ([string]::IsNullOrWhiteSpace($nomeItem)) {
            $nomeItem = $_.BaseName
        }

        # Try to resolve health and speed from prefab if available
        $vida = "Desconhecido"
        $velocidade = "Desconhecido"
        if ($prefabName -ne "Nenhum" -and $guidToPath.ContainsKey($prefabGuid)) {
            $prefabPath = $guidToPath[$prefabGuid]
            if (Test-Path $prefabPath) {
                $prefabContent = Get-Content $prefabPath -Raw -Encoding utf8
                
                # Check for vidaMaxima in SistemaDeDanos
                if ($prefabContent -match "vidaMaxima:\s*([0-9.]+)") {
                    $vida = $Matches[1]
                }
                
                # Check for speed/velocidade
                if ($prefabContent -match "speed:\s*([0-9.]+)") {
                    $speedRaw = [float]$Matches[1]
                    $velocidade = "$($speedRaw * 3.6) km/h (NavMeshAgent)"
                }
                elseif ($prefabContent -match "velocidadeMaxima:\s*([0-9.]+)") {
                    $speedRaw = [float]$Matches[1]
                    $velocidade = "$($speedRaw * 3.6) km/h (ControleNavio/Aviao)"
                }
                elseif ($prefabContent -match "velocidadeMaximaVoo:\s*([0-9.]+)") {
                    $speedRaw = [float]$Matches[1]
                    $velocidade = "$($speedRaw * 3.6) km/h (Voo)"
                }
                elseif ($prefabContent -match "velocidadeCruzeiro:\s*([0-9.]+)") {
                    $speedRaw = [float]$Matches[1]
                    $velocidade = "$($speedRaw * 3.6) km/h (Cruzeiro)"
                }
            }
        }

        $fichas += [PSCustomObject]@{
            Nome = $nomeItem
            Descricao = $descricao
            Preco = $preco
            CategoriaId = $categoria
            Prefab = $prefabName
            Vida = $vida
            Velocidade = $velocidade
            File = $_.FullName
        }
    }
}

Write-Host "Total fichas encontradas: $($fichas.Count)"

$categorias = @(
    ("Ex" + [char]0xE9 + "rcito (Terrestre)"),
    "Marinha (Naval)",
    ("Aeron" + [char]0xE1 + "utica (A" + [char]0xE9 + "reo)"),
    "Tecnologia",
    "Infraestrutura",
    "Energia",
    "Urbana"
)

$outputFile = Join-Path $rootPath "unidades_disponiveis.txt"
$report = @()
$report += "==============================================================================="
$report += ("             UNIDADES E ESTRUTURAS DISPON" + [char]0xCD + "VEIS - HEGEMONIA MUNDIAL             ")
$report += "==============================================================================="
$report += ("Data de Gera" + [char]0xE7 + [char]0xE3 + "o: $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')")
$report += "Total de itens catalogados: $($fichas.Count)"
$report += ""

$grouped = $fichas | Group-Object CategoriaId | Sort-Object Name

foreach ($group in $grouped) {
    $catId = [int]$group.Name
    $catName = "Outros / Sem Categoria"
    if ($catId -ge 0 -and $catId -lt $categorias.Count) {
        $catName = $categorias[$catId]
    }
    
    $report += "-------------------------------------------------------------------------------"
    $report += " CATEGORIA: $catName ($($group.Count) itens)"
    $report += "-------------------------------------------------------------------------------"
    
    foreach ($item in ($group.Group | Sort-Object Nome)) {
        $report += "Nome:        $($item.Nome)"
        $report += ("Pre" + [char]0xE7 + "o:       $ $($item.Preco)")
        $report += ("Descri" + [char]0xE7 + [char]0xE3 + "o:   $($item.Descricao)")
        $report += "Vida/Blind:  $($item.Vida)"
        $report += "Velocidade:  $($item.Velocidade)"
        $report += "Prefab 3D:   $($item.Prefab)"
        $report += "Arquivo:     $($item.File.Substring($rootPath.Length + 1))"
        $report += ""
    }
}

# Escreve o arquivo explicitamente como UTF-8
[System.IO.File]::WriteAllLines($outputFile, $report, [System.Text.Encoding]::UTF8)
Write-Host ("Relat" + [char]0xF3 + "rio gerado em: $outputFile")
