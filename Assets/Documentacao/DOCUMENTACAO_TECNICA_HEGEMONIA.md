# Documentação Técnica Completa - Hegemonia Global

## 1. Visão Geral do Projeto
**Hegemonia Global** (ou *Hegemonia Mundial*) é um jogo de estratégia em tempo real (RTS) desenvolvido em Unity. O jogo foca em gerenciamento de recursos, construção de bases, produção de unidades militares e combate tático.

O projeto utiliza uma arquitetura baseada em **Gerenciadores (Managers)** centralizados e componentes modulares para unidades e prédios via `MonoBehaviour`.

---

## 2. Estrutura de Arquivos e Diretórios (Assets/scripts)

A base de código está organizada funcionalmente:

- **Raiz (`Assets/scripts/`)**: Contém os gerentes principais (`GerenteDeJogo`, `GerenciadorRecursos`) e lógicas fundamentais de combate e IA.
- **`Armazens/`**: Sistema de estocagem e inventário militar/recursos.
- **`Editor/`**: Scripts de extensão do Editor da Unity para automação e facilitadores de desenvolvimento.
- **`Menus/`**: Lógica de Interface de Usuário (UI), gerenciamento de menus de construção, comandos e mercado.
- **`Predios/`**: Scripts específicos para comportamentos de edifícios (ex: `UsinaPetroleo`).

---

## 3. Sistemas Principais (Core Systems)

### 3.1. Gerenciamento de Jogo (`GerenteDeJogo.cs`)
É o coração da produção de unidades e controle de fluxo do jogo.
- **Responsabilidades**:
  - Gerenciar a **Fila de Produção** de unidades.
  - Controlar os pontos de *spawn* (nascimento) de unidades (Hangar para veículos, Tenda para soldados).
  - Instanciar unidades no mundo e atribuir suas equipes.
  - **Fallback**: Possui lógica de segurança para criar unidades na frente da câmera caso uma fábrica não seja encontrada.
- **Lógica de Produção**:
  - Utiliza a classe interna `PedidoDeProducao` para rastrear o tempo de construção.
  - Processa a fila no `Update()` decrementando o tempo restante.

### 3.2. Economia e Recursos (`GerenciadorRecursos.cs` & `PainelRecursos.cs`)
Sistema centralizado (Singleton) que gerencia toda a economia.
- **Recursos Gerenciados**:
  - 💰 **Dinheiro**: Moeda principal para compras.
  - ⛽ **Petróleo**: Combustível e manutenção.
  - 🔩 **Aço**: Material de construção pesada.
  - ⚡ **Energia**: Necessária para funcionamento de prédios avançados.
  - 👥 **População**: Limite de unidades (Atual vs Máximo).
- **Funcionalidades**:
  - **Renda Passiva**: Calcula ganhos por segundo (`dinheiroPorSegundo`, etc.) e aplica automaticamente.
  - **Transações**: Método `TentarGastar(...)` verifica saldo e deduz custos atomicamente (tudo ou nada).
  - **Eventos**: Dispara `OnRecursosAtualizados` para que a UI (`PainelRecursos.cs`) se atualize apenas quando necessário, economizando performance.

### 3.3. Unidades, Identidade e Censo
Para gerenciar combate e evitar fogo amigo, cada unidade possui uma "Cédula de Identidade".
- **IdentidadeUnidade**: Armazena o `teamID` (ex: 1 = Jogador) e `nomeDoPais`.
- **CensoImperial.cs**: Sistema estatístico que rastreia a contagem exata de tropas do jogador (Infantaria, Veículos, Naval, Aéreo) em tempo real. Essencial para limitar população e mostrar estatísticas no HUD.

- **Controle de Movimento (`ControleUnidade.cs`)**:
  - Integração com **NavMeshAgent** para pathfinding inteligente.
  - Suporte a movimento direto (Transform) para unidades aéreas.

### 3.4. Combate: Mísseis e Sistemas
- **Armas Táticas (`LancadorMisseis.cs`, `MissilTeleguiado.cs`)**:
  - Usado por veículos de combate para disparar projéteis guiados contra inimigos próximos.
  - Lógica de perseguição (`Homing`).

- **Armas Estratégicas (`MisselICBM.cs`, `SiloNuclear.cs`)**:
  - **Silo**: Prédio que armazena o ICBM.
  - **Míssil ICBM**: Voa em grande altitude (parábola) e causa detonação nuclear em área (`BombaICNU.cs`).

- **Torretas e Defesa (`ControleTorreta.cs`)**:
  - Identificação de alvo via `IdentidadeUnidade`.
  - Sistema de rotação suave para mirar.

### 3.5. Construção (`Construtor.cs` & `MenuConstrucao.cs`)
O jogador pode colocar edifícios no mapa.
- **Construtor**: Gerencia o objeto "fantasma" (preview) do prédio seguindo o mouse.
- **Validação**: Verifica se o local é válido (terreno plano, sem colisão) e se há recursos.
- **DadosConstrucao**: ScriptableObjects ou classes de dados que definem preço e tempo de cada prédio.

### 3.6. Suporte Aéreo (Helicópteros)
O jogo possui sistemas dedicados para suporte aéreo avançado.
- **Helicópteros (`Helicoptero.cs`, `Heliporto.cs`)**:
  - Podem decolar, pousar e patrulhar.
  - Consomem combustível/recursos para operar.
  - `GerenciadorHelicopteros.cs` mantém registro global da frota aérea.

---

## 4. Detalhamento Técnico das Classes e Funções

### `GerenciadorRecursos.cs`
- `public static GerenciadorRecursos Instancia`: Acesso global.
- `bool TentarGastar(int dinheiro, int petroleo, ...)`: Tenta realizar uma compra. Retorna `true` se sucesso.
- `void AdicionarRecursos(...)`: Injeta recursos (ex: recompensas).
- `bool AdicionarPopulacao(int qtd)`: Verifica limite populacional antes de criar unidade.

### `GerenteDeJogo.cs`
- `List<PedidoDeProducao> filaProducao`: Lista de unidades sendo treinadas.
- `void ComprarUnidade(GameObject prefab, int preco, int qtd)`: Inicia o processo de fabricação.
- `void FinalizarProducao(PedidoDeProducao pedido)`: Instancia a unidade no mundo e configura seu `TeamID`.

### `IdentidadeUnidade.cs`
- `int teamID`: O identificador numérico da equipe.
- `string nomeDoPais`: Texto descritivo para UI.

### `Construtor.cs`
- `void IniciarConstrucao(GameObject prefabPredio)`: Começa o modo de posicionamento.
- `void ConfirmarConstrucao()`: Finaliza a colocação e desconta recursos.

### `ControleTorreta.cs`
- `void ProcurarAlvo()`: Busca via `Physics.OverlapSphere`, filtra por `teamID` diferente.
- `void Atirar()`: Instancia projetil ou aplica dano via Raycast.

### `MisselICBM.cs`
- A lógica de voo é dividida em estados (Deitando, Subindo, Caindo).
- `void Detonar()`: Cria efeito visual e aplica `ExplosionForce` e dano em área.

### `CriadorHUDRecursos.cs`
- Script responsável por gerar a UI do topo da tela dinamicamente.
- Cria os contadores de Dinheiro, Petróleo, Aço verticalmente no canto superior esquerdo.

### `GerenteMercadoUI.cs`
- Gera interface de compra dinamicamente via código (`CriarLinhaItem`), sem necessidade de prefab de UI complexo.
- Mantém lista de `ItensMercado` e calcula total do carrinho antes de finalizar a compra.
- Integra-se com o `Construtor` para iniciar a colocação de prédios comprados.

---

## 5. Fluxos de Trabalho Comuns

### Como criar uma nova Unidade
1. Criar o Prefab da unidade (modelo + colisor).
2. Adicionar `IdentidadeUnidade` (TeamID padrao).
3. Adicionar `ControleUnidade` (Movimento e NavMeshAgent).
4. Registrar no Menu de Compra (`MenuConstrucao` ou similar) para chamar `GerenteDeJogo.ComprarUnidade`.

### Como criar um novo Prédio
1. Criar o Prefab.
2. Adicionar script funcional (ex: `GeradorRecursos` para usinas ou `Heliporto` para militar).
3. Configurar no `Construtor` e adicionar botão no `MenuConstrucao`.

### O Sistema de Armazéns
O jogo conta com um sistema logístico onde recursos não são apenas números abstratos, mas podem ter representação e limites baseados em prédios "Armazéns".
- `GerenciadorArmazens.cs`: Centraliza a lógica de capacidade total vs usada.
- **Tipos**: `GalpaoRecursos` (commodities) e `GalpaoMilitar` (munição/equipamentos).

---

## 6. Observações para Desenvolvimento Futuro
- **Singleton Pattern**: Muito utilizado (`Instancia`). Facilita acesso mas requer cuidado com ordem de inicialização (`Awake`).
- **Navegação**: O jogo mistura NavMesh (terrestres) com Transform direto (aéreos). Atenção ao criar unidades híbridas.
- **Performance**: O `GerenciadorRecursos` usa eventos para evitar checagens constantes (`polling`) na UI, o que é uma boa prática mantida.

***
*Documentação gerada automaticamente pela assistência de IA com base na análise do código fonte atual.*
