# Instruções para Configurar o NavioThink no Unity

O arquivo `NavioThink.cs` já foi criado em `Assets/scripts/NavioThink.cs`. Para completar a configuração do navio no jogo:

## Passo 1: Criar o Prefab no Unity Editor

1. Abra o Unity
2. Na janela Project, navegue até `Assets/Prefabs/Navios_Guerra/`
3. Crie uma nova pasta chamada `Nav_Think`
4. Clique com o botão direito na pasta `Nav_Think` → Create → Empty
5. Renomeie o GameObject para "NavioThink"

## Passo 2: Adicionar os Componentes Necessários

No GameObject "NavioThink", adicione os seguintes componentes:

### Componentes Obrigatórios:
1. **NavMeshAgent** (Navigation → NavMeshAgent)
   - Radius: 2
   - Speed: 10
   - Acceleration: 9999
   - Angular Speed: 120
   - Stopping Distance: 5
   - Update Position: DESMARCADO
   - Update Rotation: DESMARCADO

2. **Rigidbody** (Physics → Rigidbody)
   - Mass: 1000
   - Use Gravity: DESMARCADO
   - Is Kinematic: MARCADO
   - Constraints: Freeze Position Y, Freeze Rotation X, Freeze Rotation Z

3. **IdentidadeNaval** (Script)
   - Nome do Navio: "Navio Think"
   - Categoria Navio: Medio

4. **ControleNavioRealista** (Script)
   - Use as configurações padrão (já estão otimizadas para movimento realista)

5. **NavioThink** (Script)
   - Este é o script que você criou
   - Configure os efeitos visuais se desejar

6. **ControleUnidade** (Script)
   - Necessário para seleção e movimento via clique direito

## Passo 3: Adicionar Modelo Visual

Você tem duas opções:

### Opção A: Usar um modelo existente
1. Duplique um navio existente (ex: Navio Wall ou Liberty Prime)
2. Remova os scripts específicos daquele navio
3. Adicione o script `NavioThink`
4. Ajuste a escala conforme necessário

### Opção B: Criar do zero
1. Adicione um filho ao NavioThink (GameObject → 3D Object → Cube)
2. Ajuste a escala para parecer um navio (ex: x=2, y=1, z=8)
3. Adicione um Material para dar cor
4. Este será o modelo visual do navio

## Passo 4: Adicionar Efeitos Visuais (Opcional)

Para adicionar efeitos de água:
1. Adicione um TrailRenderer ao modelo visual (Effects → Trail)
2. Configure para parecer rastro na água
3. Adicione ParticleSystem para fumaça da chaminé se desejar

## Passo 5: Salvar como Prefab

1. Arraste o GameObject "NavioThink" da Hierarchy para a pasta `Assets/Prefabs/Navios_Guerra/Nav_Think/`
2. Salve o projeto

## Passo 6: Testar no Jogo

1. Coloque o NavioThink em uma cena com água
2. Certifique-se de que há NavMesh na área
3. Entre em Play mode
4. Clique no navio para selecioná-lo
5. Clique com o botão direito na água para mandá-lo se mover
6. O navio deve se mover suavemente com física realista

## Troubleshooting

- **Navio não se move**: Verifique se o NavMeshAgent está configurado corretamente e se há NavMesh na área
- **Navio afunda**: Ajuste o `offsetAlturaAgua` no componente ControleNavioRealista
- **Navio gira errado**: Verifique se `Update Rotation` está desmarcado no NavMeshAgent
- **Sem seleção**: Verifique se o componente ControleUnidade está adicionado

## Notas Importantes

- O script `NavioThink` configura automaticamente o Rigidbody e NavMeshAgent no Awake()
- O movimento é controlado por `ControleNavioRealista` que simula física naval realista
- A identidade do navio é gerenciada por `IdentidadeNaval`
- Para produção, use um modelo 3D real de navio em vez de um Cube
