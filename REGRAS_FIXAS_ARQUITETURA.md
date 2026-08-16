# Regras Fixas de Arquitetura — Hegemonia Mundial

Este arquivo é a fonte única das regras de preservação e escopo para qualquer IA, agente ou pessoa que alterar este projeto. Leia e siga integralmente antes de analisar, planejar ou editar qualquer arquivo.

## Bloco obrigatório para o início de todo prompt

> Preserve todas as funcionalidades existentes. Não remova UI, menus, HUDs, cenas, sistemas, scripts ou integrações existentes. Não exclua nem substitua nada que não esteja explicitamente nesta tarefa. Em caso de dúvida, preserve o comportamento atual. Antes de editar, leia integralmente `REGRAS_FIXAS_ARQUITETURA.md` e apresente um plano resumido dos arquivos que serão consultados e alterados. Aguarde confirmação quando a mudança sair do escopo solicitado, envolver exclusão, ou puder quebrar comportamento existente.

## Procedimento obrigatório antes de qualquer edição

1. Ler este arquivo por completo.
2. Inspecionar o estado atual dos arquivos e as dependências do fluxo afetado.
3. Apresentar um plano curto contendo:
   - objetivo da mudança;
   - arquivos que serão consultados;
   - arquivos que serão alterados ou criados;
   - riscos de regressão e como serão verificados.
4. Não editar arquivos fora do plano sem atualizar o plano e explicar o motivo.
5. Depois da edição, verificar compilação, referências quebradas, comportamento afetado e preservar alterações pré-existentes de outras tarefas.

## Regras de preservação

- Não remover funcionalidades, componentes, campos serializados, eventos, menus, HUDs, botões, cenas, prefabs ou assets para “simplificar” uma implementação.
- Não reescrever um sistema inteiro quando uma alteração localizada resolver a tarefa.
- Não alterar nomes, tipos, caminhos, tags, layers, referências do Inspector ou contratos públicos sem justificar explicitamente a necessidade.
- Não substituir uma implementação funcional por placeholder, mock, stub ou comportamento vazio.
- Não corrigir ou reorganizar código não relacionado só porque foi encontrado durante a tarefa.
- Não apagar arquivos, cenas, prefabs ou assets sem autorização explícita nesta tarefa e sem registrar o impacto.
- Não desfazer mudanças já presentes no diretório de trabalho. Trate-as como pertencentes ao usuário.
- Em caso de dúvida sobre intenção, escolha a opção que mantém o comportamento atual.

## Limites de escopo

- O escopo da tarefa é definido pelo pedido atual, não por oportunidades de “limpeza” encontradas no caminho.
- Uma correção só pode tocar arquivos adicionais quando houver dependência técnica comprovada; registre essa dependência no plano.
- Mudanças de arquitetura, migrações, exclusões, renomeações em massa e alterações de cena exigem confirmação explícita antes da execução.
- Prefira mudanças pequenas, reversíveis e compatíveis com o conteúdo existente.

## Organização conhecida do projeto

- `Assets/scripts/`: código de gameplay e sistemas C#.
- `Assets/scripts/IA/`: BrainMaster e diretores da IA estratégica.
- `Assets/scripts/IA01/`: controladores e sistemas da nação IA01.
- `Assets/scripts/UI/` e `Assets/scripts/Menus/`: UI Toolkit, menus e HUDs.
- `Assets/Prefabs/`: prefabs e configurações serializadas de unidades, prédios e menus.
- `Assets/Scenes/`: cenas jogáveis e cenas de menu.
- `Assets/Resources/`: dados e recursos carregados por nome em runtime.
- `Assets/Tests/`: testes automatizados.
- `Docs/` e documentação na raiz: contratos funcionais, arquitetura e procedimentos do projeto.

## Regras Unity

- Preservar referências serializadas e compatibilidade com o Inspector.
- Não mover ou renomear scripts, prefabs, cenas ou assets sem atualizar todos os consumidores e verificar os `.meta` correspondentes.
- Não editar arquivos gerados em `Library/`, `Temp/` ou `Logs/` como parte de uma implementação.
- Ao alterar gameplay, considerar tanto o jogador quanto as IAs, além de save/load, UI e cenas que usam o sistema.
- Ao alterar UI, preservar menus existentes, navegação, callbacks, IDs, textos funcionais e estados de visibilidade.
- Ao alterar IA, preservar o fluxo de ordens, agendamento, fallback da IA antiga e compatibilidade com partidas existentes.

## Critério de conclusão

Uma tarefa só está concluída quando a mudança solicitada funciona, as funcionalidades existentes continuam presentes, os arquivos tocados estão justificados e as verificações relevantes foram executadas ou tiveram sua impossibilidade registrada.

## Registro mínimo ao finalizar

Informe:

- o que foi alterado;
- quais arquivos foram alterados/criados;
- como as regras de preservação foram atendidas;
- quais verificações foram executadas e seus resultados;
- qualquer risco ou limitação que permaneça.
