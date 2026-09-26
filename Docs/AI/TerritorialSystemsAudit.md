# Auditoria dos sistemas territoriais

Data da auditoria: 2026-09-26  
Repositório analisado: `Hegemonia-Mundial-main`  
Objetivo: evoluir a propriedade política mantendo os sistemas de construção, economia, detecção, combate e câmera existentes.

## A. Fronteira anterior

`GerenteDeTerritorio.ObterDonoDoPonto` combina duas regras legadas: uma zona de `GerenciadorExpansaoFronteira` em estado reservado/ocupado tem prioridade; fora dela, o método escolhe o `MarcadorTerritorio` ativo mais próximo dentro do seu raio quadrado em X/Z. Essa é uma área de influência de cidade, não uma fronteira política irregular.

O sistema novo adiciona `DadosMapaTerritorial` com regiões poligonais normalizadas pela cobertura dos Terrains ativos. A consulta pré-indexa os polígonos em uma grade 64 × 36. Dentro de qualquer polígono definido, o resultado político é soberano; zona de expansão e raio de marcador só são consultados quando nenhum polígono cobre o ponto. O legado e suas APIs continuam disponíveis fora da geometria política.

## B. Relação com prefeitura e economia

`MarcadorTerritorio` continua registrando cidades no `GerenciadorDivisaoTerritorial`, que usa marcadores para população, emprego, produção e dados de cidade. Essas funções permanecem inalteradas. A consulta de propriedade feita por construção e IA passa a consultar o polígono político primeiro por meio da API existente de `GerenteDeTerritorio`.

## C. Leitores da fronteira antiga

Chamadores confirmados de `ObterDonoDoPonto` incluem:

- `Construtor.cs` (regras de construção);
- `ControleUnidade.cs` (verificação de território da unidade);
- `IA/BrainMaster/IA_BackendBridge.cs` e `IA/BrainMaster/IA_BuildDirector.cs`;
- `IA/Sovereign/AISovereignBackend.cs`;
- `IA01/IA01NationRuntime.cs` e `IA02/IA02NationRuntime.cs`;
- diagnóstico em `scripts/Editor/BrainMasterNavalDiagnosticsMenu.cs`.

`GerenciadorExpansaoFronteira` e `ZonaFronteiraExpansionavel` continuam responsáveis por reserva e ocupação de parcelas de construção; o novo resolvedor não os remove.

## D. País, equipe e facção

`IdentidadeUnidade.teamID` já é a identidade nacional/equipe usada pelas unidades; ela permanece como fonte da equipe da unidade. `SistemaGovernoMundial`, `DadosPaisGoverno` e `RelacaoPaisGoverno` já armazenam países, postura, tratado e guerra. `RTSVisibilityService` usa esse estado para guerra, detecção e visibilidade.

## E. Dependências do combate

Os filtros automáticos de alvo que exigem guerra aparecem em `ControleNavioRealista`, `ControleTorreta`, `ControleTorretaModular`, `LancadorNaval` e `SistemaDeTiro`. `RTSVisibilityService.TeamsAtWar` também é chamado por radar, visibilidade e seleção de contato. Por isso, sua semântica permanece intacta: as verificações de território/ROE usam uma API contextual separada, e os gates de arma preservam a regra de guerra existente com suporte explícito a cessar-fogo.

## F. Riscos e limites

- O mapa de referência mostra seis regiões continentais distintas, além da ilha central, enquanto o objetivo informa cinco países. A imagem e a cor dos biomas não determinam a associação política. As seis regiões continentais e a ilha foram preservadas como polígonos separados; as regiões continentais começam sem país atribuído e o editor oferece as equipes 1–5 para associação explícita. A ilha central começa neutra, sem dono, e capturável. As linhas rosas alimentam as regiões de águas territoriais; elas não alteram a costa física.
- A geometria é relativa aos limites dos Terrains ativos. A criação ou troca de cena atualiza esses limites sem alterar Terrain, costa, vegetação ou relevo.
- O modo M apresenta a imagem de referência como camada cartográfica e usa a mesma câmera, consulta territorial, entidades, ordens e serviço de visibilidade do controlador existente. A câmera superior segue disponível no botão `Câmera 3D`.
- Território fora dos polígonos desenhados usa fallback legado em terra. Água sem região marítima desenhada é reportada como águas internacionais quando a altura do ponto/terreno fica abaixo do nível do mar.
- Cessar-fogo fecha os gates automáticos de arma que já exigiam guerra; aquisição/detecção continua independente. A violação territorial é evento/contexto e não dispara fogo por si só.

## G. Plano mínimo de migração executado

1. Manter a API pública e os marcadores de território atuais.
2. Adicionar dados ScriptableObject para polígonos de terra, águas territoriais, neutralidade, captura e dono.
3. Consultar primeiro a camada política; aplicar o legado apenas fora dos polígonos.
4. Reutilizar as relações existentes para passagem e cessar-fogo; persistir passagem na relação existente e donos capturados no save existente.
5. Reutilizar o MapaGeralController para o mapa 2D/cartográfico, com câmera superior de debug e camadas de dados já disponíveis.
6. Adicionar o editor visual aos dados políticos sem editar Terrain ou cenas.
7. Testar consulta, precedência, águas, captura, persistência e regressões autorizadas pelo objetivo.

## Arquivos e papéis da arquitetura nova

- `Assets/scripts/Mapa/TerritorioPolitico.cs`: tipos serializáveis de regiões e consultas territoriais.
- `Assets/scripts/Mapa/DadosMapaTerritorial.cs`: ScriptableObject com polígonos, classificação land/sea e índice espacial; arquivo com o mesmo nome da classe para serialização/importação correta no Unity.
- `Assets/scripts/GerenteDeTerritorio.cs`: ponte compatível; polígonos soberanos, eventos de captura e fallback legado.
- `Assets/scripts/Governo/ContextoTerritorialDiplomatico.cs`: passagem, contexto de violação, decisões ROE e eventos de mudança territorial.
- `Assets/scripts/Governo/DadosPaisGoverno.cs`: flags de passagem por meio e cessar-fogo na relação serializada existente.
- `Assets/scripts/MapaGeralController.cs`: apresentação cartográfica M e camadas, mantendo os controles e a câmera existentes.
- `Assets/scripts/SistemaSaveGame.cs`: persistência de donos capturados no save já existente.
- `Assets/Editor/TerritoryMapEditorWindow.cs`: edição de geometria/associação no mapa de referência.

## Resultados de validação

- Unity 6000.2.15f1 compilou as assemblies de jogo e de EditMode sem erros de território/diplomacia. O asset `MapaTerritorialInicial` carrega com o script correto após `DadosMapaTerritorial` passar a ter arquivo próprio.
- Suíte EditMode em 2026-09-26: 126/130 passaram. Todos os 10 testes territoriais passaram, incluindo soberania do polígono contra propriedade legada, áreas sem país associado, persistência de captura e carregamento do asset real. As quatro falhas restantes também falhavam no relatório-base de 2026-09-23: cobertura de quartel e produção IA02 em `cena19).unity`, mais dois testes de rota/patrulha aérea.
- Suíte PlayMode executada com `-nographics`: 15/54 passaram; as 39 falhas foram dominadas por `RenderTexture.Create failed`, então essa rodada não serve como veredito de regressão de gameplay. Uma tentativa gráfica encerrou antes do início do Test Runner e não gerou XML. O relatório-base gráfico de 2026-09-23 tinha 31/42 aprovados e falhas preexistentes de cenas, rotas navais e campanha.
- O helper de testes de despacho de mísseis agora passa explicitamente o nono argumento opcional ao invocar o inicializador por reflexão; os três casos passaram na rodada EditMode final.

## Limites pendentes

- Associar as seis regiões continentais a Países 1–5 continua uma escolha de edição. Nenhuma associação foi inferida da cor, do bioma ou do legado.
- As falhas preexistentes das cenas/campanha e a validação PlayMode gráfica permanecem pendentes; nenhum sistema físico foi alterado para contornar esses resultados.
