Original prompt: bota no menu do aeroporto uma funcao para que quando tenha o mesmo modelo de aviao mais de duas unidades, voce possa enviar todos de uma vez ao clicar em patrulha, ai aparece todos ou uma quatidade igual o do drone

- Inspecionado `Assets/scripts/GerenciadorAeroporto.cs`: drone kamikaze ja possui contador em massa, mas aeronaves comuns nao.
- Ajuste em andamento: adicionar patrulha em grupo por mesmo modelo, com quantidade e opcao `Todos` quando houver 3+ unidades disponiveis.
- Concluido: patrulha em grupo por mesmo modelo adicionada ao painel do aeroporto, com contador separado e envio sequencial a partir do patio/hangar.
- Validacao: revisao manual dos fluxos de clique no `GerenciadorAeroporto.cs` concluida. Build automatica nao foi possivel porque o ambiente local esta sem `dotnet` e o MSBuild instalado nao encontra `Microsoft.CSharp.Core.targets`.
- Ajuste adicional concluido: clique de patrulha aerea do aeroporto agora usa o efeito `Marker 5 Circle Loop`, inclusive no fluxo com menu aberto e na patrulha em grupo.
- Correcao adicional: `InvalidCastException` ao instanciar o marker resolvida usando instanciação segura via `UnityEngine.Object`, aceitando prefab serializado como `GameObject` ou `Component`.
- Revisado o `ControleNavioRealista` usado pelo USS Vindicator: a manobra de re quando o destino fica atras do casco agora usa um estado curto com histerese e tempo maximo, evitando ficar alternando entre frente e re a cada frame e reduzindo as travadas na navegacao.
- Ajuste fino no prefab `USS Vindicator`: configurada uma manobra de re mais suave e um pouco mais longa so para ele, reduzindo trancos quando precisa alinhar o casco com o destino vindo de costa.
- Ajuste BrainMaster/B260: bombardeiro agora garante `SistemaDeDanos`, preserva alvo terrestre real ao sair do aeroporto/BrainMaster e retorna ao aeroporto depois de concluir ataques de patrulha da IA.
- Ajuste aviacao IA: sortidas aereas agora preservam uma aeronave no patio, podem sair em grupos pequenos, usam cooldown menor e a producao mira recompor avioes perdidos ate manter uma reserva melhor.
- Ajuste anfibio IA: hovercraft vazio busca soldados/tanques, chama embarque, leva carga para margem inimiga e desembarca para combate terrestre; producao de hover foi limitada para evitar acumulo parado no estaleiro.
- Validacao: revisao estatica concluida nos scripts alterados e balanceamento de chaves OK. Build batch pelo Unity 6000.2.15f1 foi tentado, mas o Unity recusou porque ja existe outra instancia aberta com este projeto.
- Otimizacao menus navios: `TransporteAnfibio`, `NavioTransporteTropas` e `GerenciadorPortaAvioes` tiveram caches de GUI/buffers reutilizaveis, limites de lista padrao e remocao de alocacoes em `OnGUI`, mantendo os botoes e operacoes existentes.
- Validacao: balanceamento de chaves OK nos scripts de menu. Build batch pelo Unity 6000.2.15f1 foi tentado, mas recusado porque ja existe outra instancia aberta com este projeto.
- Revisao menu governo X: `MenuGoverno` agora reaproveita o painel ao reabrir, limita refresh dinamico de eventos, cacheia categorias/subabas, atualiza rodape/dica e evita recriar subabas quando so muda selecao.
- Funcoes do menu governo conectadas: perfil/proposta/crise, sancoes com tipo/duracao, economia, ordem de compra, interior, defesa, ciencia e trabalho agora executam efeitos em `SistemaGovernoMundial`, `SistemaMercadoGlobal`, `GerenciadorRecursos` e dados locais quando necessario.
- Validacao: `Unity.exe -batchmode` foi bloqueado por outra instancia aberta do projeto, mas a compilacao standalone do `Assembly-CSharp.csproj` com o `csc` Mono da Unity 6000.2.15f1 passou sem erros.
