# Operações aéreas V2 do porta-aviões

## Diagnóstico do legado

O desaparecimento é causado por `GerenciadorPortaAvioes.ArmazenarAviaoNoHangarInterno`, na linha que executa `av.gameObject.SetActive(false)` depois de colocar o avião como filho do navio. O fluxo de retorno reativa o objeto em `RotinaElevadorSequencial` e escolhe uma vaga externa, enquanto a referência `vagaRetorno` já foi limpa. Por isso o avião parece reaparecer ou voltar a uma posição antiga.

Há disputa de autoridade: `ControleAviao.SequenciaDeVooEPouso` segue a rota, faz `SetParent` e chama `EstacionarAeronaveNoConves`; o gerenciador inicia outra rotina de estacionamento/elevador e também escreve parentesco, posição, rotação e ativação. O menu IMGUI do gerenciador chama diretamente `MandarParaOHangar`/`AcionarElevadorParaCima`. O V2 deixa o menu somente como adaptador de comandos e interrompe as coroutines do `ControleAviao` ao assumir o token.

## Instalação no porta-aviões de teste

O prefab `Assets/Prefabs/Navios_Guerra/Porta avioes/Uss Enterprise.prefab` já contém essa configuração inicial: `OperacoesAereasV2`, layout, adaptador de menu, vagas externas/internas, catapulta, elevador, pontos de serviço, grupos `Voo`/`Decolagem` e `Colisores`. O campo `usarSistemaOperacoesV2` está ativado somente nele.

1. Crie um filho `OperacoesAereasV2` no porta-aviões de teste.
2. Adicione `LayoutConvesPortaAvioesV2` e `GerenciadorOperacoesPortaAvioesV2` nesse filho; marque `usarSistemaOperacoesV2` somente nesse teste.
3. Adicione `AdaptadorMenuPortaAvioesV2` ao objeto do menu e aponte para o gerenciador V2. O menu legado continua disponível no porta-aviões sem V2.
4. No layout, use o botão **Criar estrutura padrão** e ajuste manualmente cada ponto no espaço local do navio.
5. Em cada filho direto de `VagasExternas` e `VagasInternas`, mantenha `VagaPortaAvioesV2`, um `id` único e o tamanho permitido.
6. Em cada elevador, configure `Plataforma`, `Posicao_Conves`, `Posicao_Baixa` e `Fila`; `ElevadorPortaAvioesV2` é criado automaticamente pelo layout.
7. No Inspector, execute **Validar layout**. Os gizmos usam azul para pouso, branco para taxi, verde para vagas externas, vermelho para catapultas, roxo para elevadores, laranja para hangar e ciano para serviços; as setas seguem `Transform.forward`.
8. No USS Enterprise, `Colisores` possui `BoxCollider_Conves` sólido e volumes trigger para `PistaPouso`, `Taxi`, `Elevador_01` e `Catapulta_01`. Ajuste os tamanhos no Inspector se a escala do modelo for alterada.
9. `Decolagem` contém `Fila`, `Alinhamento`, `Liberacao`, `Subida_Inicial` e `Saida_Voo`; `Voo` contém `Circuito_01`, `Afastamento_01`, `Subida_Inicial` e `Ponto_Missao`. Esses pontos são locais ao porta-aviões.

Todos os pontos são filhos do navio e devem ser posicionados com `localPosition`/`localRotation`, para acompanhar movimento e rotação do porta-aviões.

## Fluxo V2

`Pouso -> Frenagem -> Taxi -> EstacionadoNoConves -> Reabastecendo -> ProntoNoConves -> ElevadorDescendo -> ArmazenadoNoHangar -> ElevadorSubindo -> Taxi -> Catapulta -> Lancamento -> EmMissao`.

O pouso não desativa o avião. O `SetActive(false)` V2 só é executado após a plataforma atingir `Posicao_Baixa`, e apenas quando `interiorHangarModelado` está desmarcado. O retorno ativa a mesma instância no elevador inferior, sobe a plataforma e só depois percorre taxi até uma nova vaga; nunca usa a vaga externa antiga como reaparecimento.

## Estado atual da validação

Foram criados testes EditMode para transições, identidade, reserva exclusiva e visibilidade, além de um teste PlayMode de identidade. A compilação independente dos dois assemblies de teste passou. O prefab foi reimportado e aberto no Unity sem os erros de hierarquia; a mensagem vermelha antiga permanece no Console da sessão. A execução automática completa do Test Runner não foi concluída porque o painel existente ficou oculto na sessão aberta; portanto a partida real e o ciclo PlayMode completo ainda exigem execução manual pelo painel do Unity.
