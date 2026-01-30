# Guia de Correção de Erros - Unity

## 📋 Resumo dos Problemas

Este guia aborda todos os erros encontrados no console do Unity:

1. ✅ **Particle System Duration Error** - CORRIGIDO
2. ✅ **IA_Arquiteto centroDaBase Error** - CORRIGIDO  
3. ✅ **Missing Prefab Logging** - MELHORADO
4. ⚠️ **Missing Scripts** - REQUER AÇÃO MANUAL
5. ⚠️ **Font/Emoji Issues** - REQUER CONFIGURAÇÃO
6. ⚠️ **NullReferenceException (SerializedObject)** - REQUER VERIFICAÇÃO

---

## ✅ Correções Já Aplicadas

### 1. Particle System Duration Error
**Arquivo:** `SistemaDeDanos.cs`

**Problema:** Tentativa de modificar `duration` enquanto o sistema de partículas estava tocando.

**Solução:** O sistema agora para completamente antes de modificar as configurações, depois reinicia:
```csharp
if(ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
var main = ps.main;
main.loop = true;
main.duration = 1.0f;
ps.Play();
```

### 2. IA_Arquiteto centroDaBase Error
**Arquivo:** `IA_Arquiteto.cs`

**Problema:** Variável `centroDaBase` não atribuída no Inspector.

**Solução:** Adicionada validação automática que tenta encontrar o centro da base automaticamente:
- Primeiro tenta usar `comandante.basePrincipal`
- Depois tenta `comandante.transform`
- Por último usa `transform` do próprio objeto

### 3. Missing Prefab Logging
**Arquivo:** `IA_Arquiteto.cs`

**Solução:** Adicionado método `ListarPrefabsDisponiveis()` que mostra todos os prefabs disponíveis no catálogo quando um não é encontrado. Isso facilita a identificação de erros de nome.

---

## ⚠️ Correções Que Requerem Ação Manual

### 4. Missing Scripts (The referenced script (Unknown) on this Behaviour is missing!)

**Causa:** Algum GameObject na sua cena tem referências a scripts que foram deletados ou renomeados.

**Como Corrigir:**

#### Opção A - Localizar e Limpar Manualmente:
1. No Unity, vá para **Edit → Project Settings → Editor**
2. Habilite **Debug Mode** no inspetor (canto superior direito, três pontinhos)
3. Procure por GameObjects com componentes "Missing" (aparecerão como "None (Script)")
4. Remova esses componentes clicando no ícone de engrenagem → Remove Component

#### Opção B - Script de Limpeza Automática:
Crie um arquivo `Editor/CleanMissingScripts.cs`:

```csharp
using UnityEngine;
using UnityEditor;

public class CleanMissingScripts : MonoBehaviour
{
    [MenuItem("Tools/Limpar Scripts Ausentes")]
    static void LimparScriptsAusentes()
    {
        GameObject[] objs = FindObjectsOfType<GameObject>();
        int contagem = 0;
        
        foreach (GameObject obj in objs)
        {
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            if (count > 0)
            {
                contagem += count;
                Debug.Log($"Removidos {count} scripts ausentes de: {obj.name}");
            }
        }
        
        Debug.Log($"Total: {contagem} scripts ausentes removidos.");
    }
}
```

Depois use: **Tools → Limpar Scripts Ausentes** no menu do Unity.

---

### 5. Font/Emoji Issues (Unicode characters not found in font)

**Problema:** O font **LiberationSans SDF** não suporta emojis. Os seguintes caracteres estão causando erro:
- 💰 (U+1F4B0) - Money Bag
- ⛽ (U+26FD) - Fuel Pump
- 🔧 (U+1F529) - Wrench
- ⚡ (U+26A1) - Lightning
- 📦 (U+1F4E6) - Package
- 👥 (U+1F465) - People
- ⚔️ (U+2694) - Crossed Swords

**Soluções:**

#### Opção A - Substituir Emojis por Símbolos ASCII:
Encontre os textos que usam emojis (provavelmente em `DadosConstrucao` ou `MenuConstrucao`) e substitua:
- 💰 → "$"
- ⛽ → "GAS"
- 🔧 → "TOOL"
- ⚡ → "PWR"
- 📦 → "BOX"
- 👥 → "PPL"
- ⚔️ → "ATK"

#### Opção B - Importar Font com Suporte a Emojis:
1. Baixe um font que suporte emojis (ex: Noto Color Emoji, Segoe UI Emoji)
2. Importe para o Unity
3. Crie um novo **TextMeshPro Font Asset**:
   - Window → TextMeshPro → Font Asset Creator
   - Selecione o font importado
   - Em "Character Set", selecione "Unicode Range (Hex)" ou "Custom Characters"
   - Adicione os códigos dos emojis que você usa
   - Clique em "Generate Font Atlas"
4. No objeto de texto que usa emojis, troque o font para o novo

#### Opção C - Usar Imagens no Lugar de Emojis:
Use `<sprite>` tags do TextMeshPro com sprite sheets.

---

### 6. NullReferenceException - SerializedObject

**Problema:** Erro no Editor do Unity relacionado a `SerializedObject.get_isEditingMultipleObjects()`.

**Causa Provável:** 
- GameObject selecionado no inspector foi destruído
- SerializedObject corrompido
- Bug no Editor do Unity

**Como Corrigir:**

1. **Feche e reabra o Unity** - Muitas vezes resolve
2. **Delete a pasta Library/** do projeto (Unity vai regenerar)
3. **Verifique se há GameObjects sendo destruídos enquanto selecionados**
4. Se persistir, pode ser um bug do Unity. Considere atualizar para versão mais recente.

---

## 🔍 Como Verificar o Catálogo de Prefabs

Para verificar quais prefabs estão sendo carregados no `MenuConstrucao`:

1. Rode o jogo
2. Observe o console - deve aparecer:
   ```
   [MenuConstrucao] Auto-carregadas X fichas de construção.
   ```

3. Se aparecer um warning sobre "Refinaria" não encontrada, o console agora vai listar TODOS os prefabs disponíveis

4. Verifique se existe um **DadosConstrucao** (ScriptableObject) configurado com:
   - Nome contendo "refin" (case insensitive)
   - Prefab válido atribuído

---

## 📝 Ações Recomendadas (Em Ordem de Prioridade)

### 1. URGENTE - Limpar Scripts Ausentes
Use o script de limpeza acima ou localize manualmente os GameObjects com scripts missing.

### 2. IMPORTANTE - Configurar centroDaBase no Inspector
Mesmo com a correção automática, é melhor atribuir manualmente:
- Encontre o GameObject com script `IA_Arquiteto`
- No Inspector, arraste o GameObject que representa o centro da base para o campo `centroDaBase`

### 3. RECOMENDADO - Resolver Emojis
Escolha uma das opções (A, B ou C) e implemente para evitar warnings no console.

### 4. OPCIONAL - Criar Prefab de Refinaria
Se ainda não existe, crie um **ScriptableObject** `DadosConstrucao` para a Refinaria:
- Botão direito no Project → Create → DadosConstrucao
- Configure:
  - nomeItem: "Refinaria" (ou algo contendo "refin")
  - categoria: Recurso ou Economia
  - prefabDaUnidade: Arraste o prefab da Refinaria
  - preco, icone, etc.

---

## 🎯 Resultado Esperado

Após aplicar todas as correções:
- ✅ Sem erros de Particle System
- ✅ IA consegue construir base inicial
- ✅ Sem scripts ausentes
- ✅ Sem warnings de font (se resolvido)
- ✅ Console limpo e funcional

---

## 📞 Troubleshooting

**Se o erro de "Refinaria" não encontrada persistir:**
1. Verifique que o arquivo `DadosConstrucao` está dentro de uma pasta `Resources/` ou em qualquer lugar do projeto
2. Confirme que o campo `prefabDaUnidade` não está vazio
3. Use o método de contexto no MenuConstrucao: Botão direito no script → "Atualizar Catálogo Agora"

**Se ainda houver NullReferenceException no Editor:**
- Delete `Library/StateCache/`
- Reimporte o projeto (Assets → Reimport All)
- Verifique updates do Unity
