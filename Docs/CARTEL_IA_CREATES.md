# IA do cartel usando Creates manuais

O sistema esta em `Assets/scripts/Cartel/`.

## Gerar todos os Creates

No Unity, abra:

`Hegemonia > Cartel > Gerador de Creates Manuais`

Informe o `CountryId` e clique em **Criar ou atualizar pacote completo**. O gerador cria os Creates na raiz `CartelManualCreates_<pais>`, com nomes padronizados e desativados por seguranca. Posicione cada objeto no mapa e ative somente os pontos validos.

O pacote inclui os candidatos de base, areas, spawns, rotas, encontros costeiros, ilhas, patrulhas, roubos, fugas, estacionamentos, alvos, ataques, entradas, defesa, reforcos, expansao e referencias opcionais de cidade, policia, exercito e estrada.

## Configuracao da cena

1. Crie um GameObject vazio, adicione `CartelAIController` e defina o `CartelTeamId`.
2. Para cada ponto do mapa, crie um GameObject vazio e adicione `CartelManualCreate`.
3. Selecione o `Type` correspondente no Inspector e configure `CountryId`, `LinkId` e `Radius`.
4. Em cada pais, coloque pelo menos um `CartelBaseCreate` e um `CartelBaseAreaCreate`.
5. Para ativar spawns, associe os prefabs em `CartelPrefabSet` e configure os Creates de spawn.
6. Para impedir construcoes erradas, preencha `WaterLayers`, `PlacementBlockerLayers` e coloque referencias opcionais de cidade, policia, exercito e estrada.

## Ligacao entre pontos

`LinkId` deve ser o mesmo nos Creates que pertencem a uma base ou ilha. Por exemplo:

```text
Pais01_BaseCreate_01       LinkId = Pais01_Base_01
Pais01_BaseAreaCreate_01   LinkId = Pais01_Base_01
TerrestreSpawn_01          LinkId = Pais01_Base_01
BaseExit_01                LinkId = Pais01_Base_01
FuelStorage_01             LinkId = Pais01_Base_01
```

Para uma rota, use `CountryId`, `RouteSetId` e `RouteSequence`. Os pontos serao ordenados pelo `RouteSequence`.

## Petroleiros

O cartel so considera um petroleiro quando ele passou dentro de um `OilPlatformExitCreate`, possui carga e esta dentro de um `CartelRobberyAreaCreate`. Estados de carregamento e acoplamento da plataforma sao rejeitados.

## Prefabs

Os prefabs sao opcionais para permitir validar a logica antes de finalizar os modelos. Quando um prefab nao estiver associado, a IA aguarda a unidade correspondente e nao cria uma unidade improvisada fora de um Create.

## Fluxos implementados

- selecao de base por distancia das referencias e acesso costeiro;
- validacao do raio da area da base e bloqueios fisicos;
- spawn terrestre e maritimo em Creates manuais;
- saida, patrulha, area de roubo, fuga e ilha de apoio;
- transferencia costeira e armazenamento numerico na base;
- selecao de alvo terrestre, chegada, posicoes de ataque, retirada e retorno;
- expansao somente para paises com `CartelExpansionCreate` e `CartelBaseCreate`.

O objeto `CartelManualCreate` pode permanecer sem renderer; os gizmos aparecem no Editor para facilitar a montagem do mapa.

## Testes

Os testes EditMode ficam em `Assets/Tests/EditMode/CartelCreateEditModeTests.cs`. Eles verificam ocupacao, registro, criacao de base, escolha do ponto mais distante da cidade e a presenca de todas as categorias de Create.

Os testes principais de execucao ficam em `Assets/Tests/PlayMode/CartelCreatePlayModeTests.cs`. Use o Test Runner com a plataforma **PlayMode** para verificar o registro em runtime, a criacao da base dentro da area e a escolha do candidato mais distante da cidade.
