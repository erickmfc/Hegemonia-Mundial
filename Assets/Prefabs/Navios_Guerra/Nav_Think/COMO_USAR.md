# Como Adicionar o Navio Think ao Jogo

⚠️ **IMPORTANTE**: O prefab automático foi removido devido a GUIDs inválidos. Use os métodos abaixo.

O Navio Think já foi configurado com scripts de movimento realista. Siga um dos métodos abaixo para adicioná-lo ao jogo:

## Método 1: Usar o Script de Spawn (RECOMENDADO - Mais Fácil)

1. **Adicionar o Spawner à Cena:**
   - Na sua cena do Unity, crie um GameObject vazio (GameObject → Create Empty)
   - Renomeie para "NavioThinkSpawner"
   - Adicione o componente `NavioThinkSpawner` (script em Assets/scripts/)

2. **Configurar o Spawn:**
   - No Inspector, defina a `Posição Spawn` onde você quer que o navio apareça
   - Opcionalmente, arraste um modelo 3D de navio para `Modelo Visual` (se não usar, será criado um cubo simples)

3. **Fazer o Spawn:**
   - Entre em Play Mode
   - No Inspector, clique no botão `Spawn Navio Think()` (se você expor como método público)
   - OU chame via script: `GetComponent<NavioThinkSpawner>().SpawnNavioThink();`

## Método 2: Criar Prefab Manualmente no Unity

1. **Criar GameObject:**
   - Hierarchy → Create Empty
   - Renomeie para "NavioThink"

2. **Adicionar Componentes:**
   - Adicione `NavMeshAgent` (Navigation → NavMeshAgent)
     - Radius: 2
     - Speed: 10
     - Update Position: DESMARCADO
     - Update Rotation: DESMARCADO
   
   - Adicione `Rigidbody` (Physics → Rigidbody)
     - Mass: 1000
     - Use Gravity: DESMARCADO
     - Is Kinematic: MARCADO
     - Constraints: Freeze Position Y, Freeze Rotation X, Freeze Rotation Z
   
   - Adicione `IdentidadeNaval` (script)
     - Nome do Navio: "Navio Think"
     - Categoria: Medio
   
   - Adicione `ControleNavioRealista` (script)
     - Use configurações padrão
   
   - Adicione `NavioThink` (script)
     - Script que você criou em Assets/scripts/
   
   - Adicione `ControleUnidade` (script)
     - Necessário para seleção e movimento
   
   - Adicione `IdentidadeUnidade` (script)
     - Team ID: 1
     - Nome do País: "Player"

3. **Adicionar Modelo Visual:**
   - Crie um filho (GameObject → 3D Object → Cube)
   - Ajuste escala para x=2, y=1, z=8 (formato de navio)
   - Arraste este filho para o campo `Modelo 3D` no componente `ControleNavioRealista`

4. **Salvar como Prefab:**
   - Arraste o GameObject "NavioThink" da Hierarchy para a pasta `Assets/Prefabs/Navios_Guerra/Nav_Think/`

## Método 3: Duplicar Navio Existente (Mais Rápido)

1. **Duplique um Navio:**
   - Na Project view, encontre um navio existente (ex: Navio Wall ou Liberty Prime)
   - Duplique (Ctrl+D)
   - Renomeie a cópia para "NavioThink"

2. **Modificar Scripts:**
   - Selecione o prefab
   - Remova scripts específicos daquele navio (ex: NavioLiberty se duplicou o Liberty)
   - Adicione o script `NavioThink`
   - Configure `IdentidadeNaval` com nome "Navio Think" e categoria "Medio"

3. **Ajustar Escala:**
   - Ajuste a escala do modelo visual se necessário

## Testar o Movimento

Após criar o navio por qualquer método:

1. Coloque o navio em uma cena com água
2. Certifique-se de que há NavMesh na área (bake o NavMesh se necessário)
3. Entre em Play Mode
4. Clique no navio para selecioná-lo (deve aparecer círculo de seleção)
5. Clique com o botão direito na água para mandá-lo se mover
6. O navio deve:
   - Acelerar suavemente (não instantaneamente)
   - Curvar realisticamente (raio largo)
   - Ter efeitos visuais (inclinação nas curvas)
   - Parar suavemente ao chegar

## Componentes do Navio Think

- **NavioThink.cs**: Configuração automática do navio
- **ControleNavioRealista.cs**: Movimento físico realista (aceleração, curvas, inércia)
- **IdentidadeNaval.cs**: Identidade para sistema de piers e docas
- **ControleUnidade.cs**: Seleção e ordens de movimento
- **NavMeshAgent**: Navegação pelo mapa
- **Rigidbody**: Física (configurado como kinematic para movimento controlado)

## Solução de Problemas

- **Navio não se move**: Verifique se há NavMesh e se o NavMeshAgent está configurado
- **Navio afunda**: Ajuste `Offset Altura Agua` no ControleNavioRealista
- **Navio gira errado**: Desmarque "Update Rotation" no NavMeshAgent
- **Não consegue selecionar**: Verifique se ControleUnidade está adicionado
- **Sem movimento fluido**: ControleNavioRealista deve estar presente e ativado
