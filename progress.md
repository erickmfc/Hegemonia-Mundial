Original prompt: bota no menu do aeroporto uma funcao para que quando tenha o mesmo modelo de aviao mais de duas unidades, voce possa enviar todos de uma vez ao clicar em patrulha, ai aparece todos ou uma quatidade igual o do drone

- Inspecionado `Assets/scripts/GerenciadorAeroporto.cs`: drone kamikaze ja possui contador em massa, mas aeronaves comuns nao.
- Ajuste em andamento: adicionar patrulha em grupo por mesmo modelo, com quantidade e opcao `Todos` quando houver 3+ unidades disponiveis.
- Concluido: patrulha em grupo por mesmo modelo adicionada ao painel do aeroporto, com contador separado e envio sequencial a partir do patio/hangar.
- Validacao: revisao manual dos fluxos de clique no `GerenciadorAeroporto.cs` concluida. Build automatica nao foi possivel porque o ambiente local esta sem `dotnet` e o MSBuild instalado nao encontra `Microsoft.CSharp.Core.targets`.
- Ajuste adicional concluido: clique de patrulha aerea do aeroporto agora usa o efeito `Marker 5 Circle Loop`, inclusive no fluxo com menu aberto e na patrulha em grupo.
- Correcao adicional: `InvalidCastException` ao instanciar o marker resolvida usando instanciação segura via `UnityEngine.Object`, aceitando prefab serializado como `GameObject` ou `Component`.
- Revisado o `ControleNavioRealista` usado pelo USS Vindicator: a manobra de re quando o destino fica atras do casco agora usa um estado curto com histerese e tempo maximo, evitando ficar alternando entre frente e re a cada frame e reduzindo as travadas na navegacao.
- Ajuste fino no prefab `USS Vindicator`: configurada uma manobra de re mais suave e um pouco mais longa so para ele, reduzindo trancos quando precisa alinhar o casco com o destino vindo de costa.
