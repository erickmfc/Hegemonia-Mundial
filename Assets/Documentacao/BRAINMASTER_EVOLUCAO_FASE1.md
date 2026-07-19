# BrainMaster - Evolucao Fase 1

## Diagnostico

O runtime atual e coordenado por `IA_BrainMaster`, com `IA_Context` compartilhando `IA_WorldState`, analise de mapa e ameacas, scheduler, backend bridge e diretores de construcao, producao, economia, mercado, diplomacia, logistica, guerra, defesa, ar e marinha. A fila central ja era o ponto correto de integracao, mas ainda nao tinha um ciclo completo de adiamento e arbitragem nacional.

O arquivo de referencia `GDU_BrainMaster_Completo.txt` nao esta presente no checkout analisado. O pedido anexado e a documentacao `INTEGRACAO_IA_BRAINMASTER.md` foram usados como contrato funcional.

## Fase 1 implementada

- `IA_CommandQueue.Requeue` preserva comandos que nao puderam executar por quota.
- `IA_BrainMaster.ProcessCommandQueue` limita comandos inspecionados por frame, evitando drenar uma fila inteira de comandos bloqueados.
- O coordenador global ordena os `TeamId` registrados antes de calcular slots pesados.
- `IA_NationalIntentBoard` oferece publicacao, deduplicacao e expiracao de intencoes.
- `IA_StrategyArbiter` seleciona uma intencao viavel por utilidade, custo, risco, urgencia e estilo.
- `IA_Context` expoe o board e o arbitro para os diretores existentes.
- Construcoes e producoes aceitas pelo backend ficam em `AwaitingConfirmation` ate o `WorldState` observar o objeto novo; timeout aplica retry exponencial limitado a tres tentativas.
- `IA_BuildDirector` e `IA_ProductionDirector` agora publicam intencoes. `IA_IntentCommandRouter` seleciona uma intencao viavel por ciclo e a encaminha para `IA_CommandQueue`.

## Comportamento anterior e novo

Antes, uma ordem bloqueada por quota era marcada como falha e desaparecia. Agora ela volta para a fila com atraso curto e continua sujeita a deduplicacao. Antes, uma resposta positiva do backend concluia construcao e producao imediatamente; agora o status so vira `Success` apos observacao no `WorldState`. Antes, o limite do loop contava apenas execucoes bem-sucedidas; agora tambem limita inspecoes. A arbitragem foi adicionada como contrato, mas ainda nao altera os diretores legados automaticamente.

## Riscos e dependencias

Combate, diplomacia, mercado, aviao e marinha ainda publicam comandos diretamente. A proxima etapa deve migrar esses dominios de forma gradual, mantendo o caminho de recuperacao emergencial direto para evitar bloqueio durante colapso de base.

## Como testar

1. Abrir uma cena com duas ou mais IAs e confirmar que os slots pesados permanecem estaveis mesmo se a ordem de `Awake` mudar.
2. Encher a fila com ordens de combate acima da quota e confirmar que elas permanecem `Queued`, sem desaparecer e sem gerar pico de CPU.
3. Publicar duas intencoes com o mesmo `DedupKey` e confirmar que apenas a primeira entra no board.
4. Executar o projeto em `ShadowReadOnly` e verificar que nenhuma intencao ou comando produz efeito no backend.

## Proxima fase

Adicionar confirmacao de resultado por comando, codigos de falha e retry com backoff; depois migrar construcao e producao para `IA_NationalIntentBoard`.
