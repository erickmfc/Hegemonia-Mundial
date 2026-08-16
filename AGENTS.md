# Instruções obrigatórias para agentes — Hegemonia Mundial

Antes de fazer qualquer análise de implementação ou edição neste projeto, leia integralmente:

`REGRAS_FIXAS_ARQUITETURA.md`

Essas regras são obrigatórias e fazem parte do contrato de manutenção do jogo. O agente deve preservar todas as funcionalidades existentes, incluindo UI, menus, HUDs, cenas, prefabs, sistemas e integrações. Não deve remover, substituir, reorganizar ou “simplificar” nada fora do pedido atual. Em caso de dúvida, deve preservar o comportamento atual.

## Ritual de entrada obrigatório

Na primeira resposta de cada tarefa, depois de ler as regras, o agente deve apresentar um plano resumido com objetivo, arquivos a consultar, arquivos a alterar/criar, riscos e verificações. Nenhuma edição deve ser feita antes desse plano. Se a execução precisar sair do plano, o agente deve atualizar o plano e explicar a razão antes de continuar.

Use este bloco no início de qualquer prompt enviado a outro agente:

> Preserve todas as funcionalidades existentes, não remova UI, menus ou HUDs, não exclua nada que não esteja explicitamente nesta tarefa. Em caso de dúvida, preserve o comportamento atual. Leia e siga integralmente `REGRAS_FIXAS_ARQUITETURA.md` e apresente um plano resumido de arquivos antes de editar.

Se o agente não puder cumprir este procedimento, ele deve parar antes de editar e informar a limitação.
