# 🔍 Relatório de Auditoria — Hegemonia Global (Alpha 1.6)

**Data da auditoria:** 22/05/2026  
**Versão Unity:** 2022.3 LTS → 6000.2.15f1 (migração em andamento)  
**Escopo:** Revisão completa do projeto — código, documentação, performance, arquitetura

---

## 📊 Resumo Executivo

| Área | Status | Prioridade |
|------|--------|------------|
| Performance / FPS | 🔴 Crítico | Imediata |
| Memory Leak | 🔴 Crítico | Imediata |
| Migração trilha de controle | 🟠 Em andamento | Alta |
| BrainMaster Urbanismo | 🟠 Parcial | Alta |
| Prefabs faltantes/quebrados | 🟡 7+ itens | Média |
| Features planejadas | 🟡 4 sistemas | Média |
| Bugs conhecidos | 🟡 6 categorias | Média |
| Dívida técnica (código duplicado) | 🟠 Significativa | Alta |
| Log spam | 🟡 Moderado | Média |

---

## 1. 🔴 PERFORMANCE CRÍTICA

### 1.1 CPU — Main Thread Acima do Orçamento
Com base nos CSVs de diagnóstico (`Diagnostico fps/desempenho_2026-05-14_21-23-00.csv` e `desempenho_2026-05-13_22-50-33.csv`):

| Métrica | Valor Observado | Alvo (60 FPS) |
|---------|----------------|---------------|
| Main Thread | **16–35ms+** frequente | ≤ 16.67ms |
| IA_BuildDirector | **até 200ms+** (pico) | < 5ms |
| IA_SquadDirector | **até 50ms** | < 5ms |
| IA_DeusaBrain | **até 368ms** (pico!) | < 10ms |
| Coast Scan | **100–388ms** | < 5ms |

**Ação necessária:**
- [`IA_DeusaBrain`](Assets/scripts/IA/) com pico de **368ms** é o maior gargalo individual — precisa de profiling profundo e possivelmente reescrita com Jobs/Burst ou corrotinas distribuídas
- [`IA_BuildDirector`](Assets/scripts/IA/BrainMaster/) precisa ser limitado por frame (orçamento temporal)
- Coast Scan precisa ser assíncrono (já usa corrotina? verificar se está rodando no main thread)
- [`FindObjectsByType`](Assets/scripts/Governo/SistemaGovernoMundial.cs:33) chamado a cada 5s no [`SistemaGovernoMundial`](Assets/scripts/Governo/SistemaGovernoMundial.cs) — substituir por sistema de registro no Awake/OnDestroy

### 1.2 Garbage Collection Excessiva
- GC Gen1 e Gen2 coleções **regulares** durante a sessão
- Memory delta de **-100MB+** em picos (objetos grandes sendo coletados)
- Alocações por frame estão acima do aceitável para 60 FPS

**Ação necessária:**
- Auditar alocações nas corrotinas dos diretores de IA (List, Dictionary criados por frame)
- Substituir `LINQ` em hot paths por iteração manual
- Usar `ArrayPool<T>` / `ObjectPool<T>` para buffers temporários
- Verificar `foreach` em `Dictionary`/`List` que causa boxing de enumerator

### 1.3 Memory Leak (Crescimento Contínuo)
- Memória inicia em ~745MB e cresce para **~900MB+** durante a sessão
- Indica objetos não liberados (event handlers não removidos, listas crescendo sem limite, referências em dicionários estáticos)

**Ação necessária:**
- Usar Unity Memory Profiler para identificar objetos não descartados
- Verificar `OnEnable`/`OnDisable` — handlers de evento assinados em OnEnable precisam ser removidos em OnDisable
- Verificar dicionários estáticos (ex: `s_cacheBarracasPorTeam` no [`SistemaGovernoMundial`](Assets/scripts/Governo/SistemaGovernoMundial.cs))

---

## 2. 🟠 MIGRAÇÃO DA TRILHA DE CONTROLE DE UNIDADES (Em Andamento)

Conforme [`PLANO_MIGRACAO_TRILHA_CONTROLE_UNIDADES.md`](PLANO_MIGRACAO_TRILHA_CONTROLE_UNIDADES.md) (572 linhas de planejamento):

### Sistemas duplicados atuais:
| Sistema Antigo | Sistema Novo/Paralelo | Tipo |
|----------------|----------------------|------|
| [`ControleNavioRealista`](Assets/scripts/) | [`NavegacaoInteligenteNaval`](Assets/scripts/) | Naval |
| Trilha de patrulha antiga | Nova trilha unificada | Patrulha/Seguir |
| [`ControleAviao`](Assets/scripts/ControleAviao.cs) | [`ControleAviaoCaca`](Assets/scripts/ControleAviaoCaca.cs) | Aéreo |

### Status das 6 etapas:
- **Etapa 1 (Preparação):** ✅ Iniciada
- **Etapa 2 (Trilha oficial):** ❌ Não iniciada
- **Etapa 3 (Adaptação unidades):** ❌ Não iniciada
- **Etapa 4 (Desativação legado):** ❌ Não iniciada
- **Etapa 5 (Testes):** ❌ Não iniciada
- **Etapa 6 (Limpeza):** ❌ Não iniciada

**Ação necessária:** Concluir a migração antes de adicionar novas features que dependam de controle de unidades. Cada novo sistema que usa a trilha antiga aumenta a dívida técnica.

---

## 3. 🟠 BRAINMASTER URBANISMO (Parcial)

Conforme [`BRAINMASTER_URBANISMO.md`](BRAINMASTER_URBANISMO.md):

### Módulos planejados:
| Módulo | Status |
|--------|--------|
| [`IA_SemanticMapPlanner`](Assets/scripts/IA/) | ❌ Não implementado |
| [`IA_ZonePlanner`](Assets/scripts/IA/) | ❌ Não implementado |
| [`IA_LotPlanner`](Assets/scripts/IA/) | ❌ Não implementado |
| [`IA_UrbanBuildValidator`](Assets/scripts/IA/) | ❌ Não implementado |
| [`IA_ConstructionPlanner`](Assets/scripts/IA/) | ❌ Não implementado |

### Fases de migração (3 fases):
- **Fase Shadow:** ❌ Não iniciada
- **Fase Hybrid:** ❌ Não iniciada
- **Fase Full:** ❌ Não iniciada

**Ação necessária:** Iniciar implementação seguindo a ordem sugerida no documento: SemanticMapPlanner → ZonePlanner → LotPlanner → UrbanBuildValidator → ConstructionPlanner.

---

## 4. 🟡 PRE FABS FALTANTES / QUEBRADOS

Conforme [`DOCUMENTACAO_PROJETO.md`](DOCUMENTACAO_PROJETO.md) e [`AuditoriaConteudoJogo.cs`](Assets/scripts/AuditoriaConteudoJogo.cs):

| Prefab | Problema |
|--------|----------|
| **USS Dominion** | Referência ausente |
| **USS Wraith** | Referência ausente |
| **USS Mako** | Referência ausente |
| **USS Arrowhead** | Referência ausente |
| **Artilharia** | Referência ausente |
| **Centro de Construção** | Referência ausente |
| **+ outros** | Detectados pelo auditor automático |

O [`AuditoriaConteudoJogo.cs`](Assets/scripts/AuditoriaConteudoJogo.cs) valida em runtime:
- Prefabs nulos
- Preços negativos
- Colliders ausentes
- [`SaveableEntity`](Assets/scripts/) ausente
- [`IdentidadeUnidade`](Assets/scripts/) ausente
- [`SistemaDeDanos`](Assets/scripts/) ausente
- Armas ausentes em categorias militares
- Categorias suspeitas

**Ação necessária:** Rodar o jogo e verificar o console após a cena carregar — o auditor emite warnings/erros. Corrigir cada item listado.

---

## 5. 🟡 BUGS CONHECIDOS

Conforme [`GUIA_CORRECAO_ERROS.md`](GUIA_CORRECAO_ERROS.md):

| # | Erro | Status |
|---|------|--------|
| 1 | **Particle System Duration** — Log de erro no console | ✅ Corrigido |
| 2 | **IA_Arquiteto centroDaBase** — NullReferenceException | ✅ Corrigido |
| 3 | **Missing Prefab Logging** — Logs insuficientes | ⚠️ Melhorado (não validado) |
| 4 | **Missing Scripts** — "The referenced script (Unknown) on this Behaviour is missing!" | ❌ Requer ação manual (script de limpeza disponível) |
| 5 | **Font/Emoji Issues** — Caracteres Unicode não encontrados na fonte | ⚠️ Soluções disponíveis, não aplicadas |
| 6 | **NullReferenceException - SerializedObject** | ⚠️ Não verificado |

### 5.1 Missing Scripts — Ação Urgente
O script de limpeza [`RemoveMissingPrefabs.cs`](Assets/Editor/RemoveMissingPrefabs.cs) existe no projeto. Rodar:
1. Abrir Unity
2. `Tools > Cleanup > Remove Missing Scripts from All Prefabs`

---

## 6. 🟡 FEATURES PLANEJADAS NÃO IMPLEMENTADAS

Conforme [`DOCUMENTACAO_PROJETO.md`](DOCUMENTACAO_PROJETO.md) (seção "Próximos Passos"):

| Feature | Descrição | Prioridade |
|---------|-----------|------------|
| **Invasão Anfíbia** | Sistema de desembarque de tropas de navios para terra | Alta |
| **Clima Dinâmico** | Sistema de clima que afeta gameplay (visibilidade, movimento, operações aéreas) | Média |
| **Diplomacia Avançada** | Negociações, tratados, acordos comerciais, embargos | Média |
| **Telemetria** | Sistema de coleta de dados de gameplay para balanceamento | Baixa |

### 6.1 Integração IA BrainMaster
Conforme [`INTEGRACAO_IA_BRAINMASTER.md`](INTEGRACAO_IA_BRAINMASTER.md), as 3 fases (Shadow → Hybrid → Full) estão documentadas mas **nenhuma foi concluída**. O rollback seguro também está planejado.

---

## 7. 🟠 DÍVIDA TÉCNICA — CÓDIGO DUPLICADO

### 7.1 Controle Naval
- [`ControleNavioRealista`](Assets/scripts/) (antigo) vs [`NavegacaoInteligenteNaval`](Assets/scripts/) (novo) — ambos ativos
- [`ControleSubmarino`](Assets/scripts/) — terceira variação
- [`GerenciadorPortaAvioes`](Assets/scripts/GerenciadorPortaAvioes.cs) — 1753 linhas, herda de [`GerenciadorAeroporto`](Assets/scripts/)

### 7.2 Controle Aéreo
- [`ControleAviao`](Assets/scripts/ControleAviao.cs) (principal)
- [`ControleAviaoCaca`](Assets/scripts/ControleAviaoCaca.cs) (variante)
- Lógica de pouso/decolagem duplicada entre [`GerenciadorAeroporto`](Assets/scripts/) e [`GerenciadorPortaAvioes`](Assets/scripts/GerenciadorPortaAvioes.cs)

### 7.3 Sistemas de IA Paralelos
- [`IA_BrainMaster`](Assets/scripts/IA/BrainMaster/IA_BrainMaster.cs) (novo, modular) — em integração parcial
- Sistema DEUSA AI (legado) — ainda ativo com picos de **368ms**
- Sistema IA3 (legado) — ainda ativo
- Sistema NovaIA (legado) — ainda ativo

### 7.4 FindObjectsByType em Hot Paths
O [`SistemaGovernoMundial`](Assets/scripts/Governo/SistemaGovernoMundial.cs) chama `FindObjectsByType<GerenciadorQuartel>` e `FindObjectsByType<Fabrica>` a cada 5 segundos (linhas 33-34). Isso é extremamente caro em cenas grandes.

---

## 8. 📏 ARQUIVOS COM TAMANHO EXCESSIVO

| Arquivo | Linhas | Risco |
|---------|--------|-------|
| [`GerenciadorPortaAvioes.cs`](Assets/scripts/GerenciadorPortaAvioes.cs) | **1753** | Alto — classe faz tudo (UI, navegação, hangar, elevador, patrulha, debug) |
| [`SistemaGovernoMundial.cs`](Assets/scripts/Governo/SistemaGovernoMundial.cs) | **1293** | Alto — múltiplas responsabilidades |
| [`MenuGoverno.cs`](Assets/scripts/Menus/MenuGoverno.cs) | **3050** | Crítico — maior arquivo do projeto, UI + lógica de governo |
| [`Fazenda.cs`](Assets/scripts/Fazenda.cs) | **626** | Médio — IMGUI + lógica de produção |
| [`SistemaEconomiaImoveis.cs`](Assets/scripts/Governo/SistemaEconomiaImoveis.cs) | **479** | Médio |

---

## 9. ⚠️ LOG SPAM NO CONSOLE

Itens identificados nos CSVs de diagnóstico:

| Log | Impacto |
|-----|---------|
| **"Petroleo Pier time 2"** repetido excessivamente | Spam de log causa overhead de string e GC |
| **"OrdemTravada"** — "Executor naval sem progresso" | Indica bug em pathfinding naval |
| **"OrdemTravada"** — "NavMeshAgent sem caminho/com caminho parcial" | Bug em pathfinding terrestre |
| **"Executor naval sem progresso"** | Navios presos em ordens impossíveis |

**Ação necessária:**
- Adicionar throttling nos logs (máximo 1 por segundo por unidade)
- Investigar por que navios estão recebendo ordens para destinos inalcançáveis
- Remover logs de debug em produção ou usar `Debug.Log` condicional com `#if UNITY_EDITOR`

---

## 10. 📋 CHECKLIST DE AÇÕES PRIORITÁRIAS

### 🔴 Imediato (Esta Semana)
- [ ] **Profiler no IA_DeusaBrain** — pico de 368ms é inaceitável, causa frame drops massivos
- [ ] **Corrigir memory leak** — usar Unity Memory Profiler, verificar crescimento de 745→900MB
- [ ] **Remover Missing Scripts** de todos os prefabs (script [`RemoveMissingPrefabs.cs`](Assets/Editor/RemoveMissingPrefabs.cs))
- [ ] **Throttling de logs** — "Petroleo Pier time 2", "OrdemTravada", "Executor naval sem progresso"
- [ ] **Substituir FindObjectsByType** no [`SistemaGovernoMundial`](Assets/scripts/Governo/SistemaGovernoMundial.cs) por sistema de registro

### 🟠 Alta (Próximas 2 Semanas)
- [ ] **Iniciar Etapa 2** da migração da trilha de controle ([`PLANO_MIGRACAO_TRILHA_CONTROLE_UNIDADES.md`](PLANO_MIGRACAO_TRILHA_CONTROLE_UNIDADES.md))
- [ ] **Iniciar Fase Shadow** do BrainMaster Urbanismo ([`BRAINMASTER_URBANISMO.md`](BRAINMASTER_URBANISMO.md))
- [ ] **Corrigir prefabs faltantes** (USS Dominion, USS Wraith, USS Mako, USS Arrowhead, Artilharia, Centro de Construção)
- [ ] **Resolver font/emoji** — aplicar uma das soluções do [`GUIA_CORRECAO_ERROS.md`](GUIA_CORRECAO_ERROS.md)
- [ ] **Ordens navais travadas** — adicionar timeout e cancelamento automático para ordens impossíveis

### 🟡 Média (Próximo Mês)
- [ ] **Refatorar [`MenuGoverno.cs`](Assets/scripts/Menus/MenuGoverno.cs)** — 3050 linhas é insustentável; dividir em partial classes ou componentes separados
- [ ] **Refatorar [`GerenciadorPortaAvioes.cs`](Assets/scripts/GerenciadorPortaAvioes.cs)** — separar UI, navegação, hangar em módulos
- [ ] **Implementar Invasão Anfíbia** (próxima feature)
- [ ] **Otimizar GC** — eliminar alocações por frame nos diretores de IA
- [ ] **Reduzir LINQ** em hot paths
- [ ] **Implementar Fase Shadow** da integração BrainMaster ([`INTEGRACAO_IA_BRAINMASTER.md`](INTEGRACAO_IA_BRAINMASTER.md))

### 🟢 Baixa (Futuro)
- [ ] **Clima Dinâmico**
- [ ] **Diplomacia Avançada**
- [ ] **Telemetria**
- [ ] **Unificar sistemas de IA** (DEUSA + IA3 + NovaIA → BrainMaster)

---

## 11. PONTOS POSITIVOS

Apesar dos problemas, o projeto demonstra maturidade em várias áreas:

1. **Sistema de auditoria automática** ([`AuditoriaConteudoJogo.cs`](Assets/scripts/AuditoriaConteudoJogo.cs)) — excelente prática de QA
2. **Documentação extensa** — 12+ documentos Markdown cobrindo arquitetura, migrações, checklists
3. **Sistema de diagnóstico FPS** — CSVs detalhados permitem análise post-mortem de performance
4. **BrainMaster** — arquitetura modular de IA bem planejada com diretores especializados
5. **Sistema de governo/economia** — [`MenuGoverno`](Assets/scripts/Menus/MenuGoverno.cs), [`SistemaGovernoMundial`](Assets/scripts/Governo/SistemaGovernoMundial.cs), [`SistemaEconomiaImoveis`](Assets/scripts/Governo/SistemaEconomiaImoveis.cs) formam base sólida para gameplay estratégico
6. **Torretas modulares** — sistema flexível documentado em [`GUIA_TORRETAS_MODULARES.md`](GUIA_TORRETAS_MODULARES.md)
7. **Navegação naval com marcha à ré** — sistema sofisticado documentado com diagramas
8. **Plano de migração com rollback** — preocupação com segurança e compatibilidade reversa

---

## 12. CONCLUSÃO

O projeto **Hegemonia Global** está em Alpha 1.6 com uma base sólida de sistemas, mas enfrenta **problemas críticos de performance** que precisam ser resolvidos antes de avançar para novas features. Os principais gargalos são:

1. **CPU** — IA consome múltiplas vezes o orçamento de frame (368ms quando o limite é 16ms)
2. **Memória** — Vazamento contínuo de ~155MB por sessão
3. **Dívida técnica** — Múltiplos sistemas de controle de unidade, IA, e pathfinding coexistem

A prioridade imediata deve ser **estabilizar a performance** (profiling + correção de memory leak + otimização de IA) antes de continuar as migrações planejadas e implementar novas features.

---

*Relatório gerado automaticamente com base na análise de 30+ arquivos do projeto.*
