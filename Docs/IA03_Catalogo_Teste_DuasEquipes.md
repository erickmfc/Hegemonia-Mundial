# Catálogo de teste da IA03 — Valdoria e Karsovia

Este catálogo completa a configuração de teste já ligada à cena `Assets/Scenes/GlobalMapRTS.unity`. Ele preserva os seis assets existentes e acrescenta patrulha terrestre, grupo de porta-aviões, patrulha submarina, invasão anfíbia e visita presidencial. Assim, os nove valores de `IA03TipoMissao` ficam representados, com variantes terrestres, navais e aéreas para patrulha.

## Equipes configuradas

| País | Team ID | Perfil | Equipe-alvo |
|---|---:|---|---:|
| Valdoria | 1 | `PerfilPais_Valdoria` | 3 (Karsovia) |
| Karsovia | 3 | `PerfilPais_Karsovia` | 1 (Valdoria) |

As duas IA03 compartilham os mesmos 11 assets de missão, mantendo o perfil e os Creatys próprios de cada equipe. Os IDs das missões têm prefixo `TEST-`; esta configuração é de teste, não um balanceamento final de campanha.

A cena inicial deixa ambas as IA03 em Paz (`nivelDeConflito: 0`); o ponto `PontoGenerico` por equipe dá um destino compatível ao fluxo genérico sem missão pré-configurada. As missões de crise entram nos níveis N4–N1 conforme os gates atuais.

## Categorias e requisitos da simulação

| Categoria | Faixa configurada | Creaty de início | Composição/requisito |
|---|---|---|---|
| Patrulha aérea, naval e terrestre | N4–N1 para ar/mar; N4–N1 para terra | `PatrulhaAerea`, `PatrulhaNaval`, `PatrulhaTerrestre` | Unidade compatível com o domínio |
| Avanço | N3–N1 | `AvancoN3` | Unidade terrestre conforme a missão existente |
| Ataque limitado | N2–N1 | `ConflitoN2` | Mantém autorização de guerra e alvo observado exigidos pelo código |
| Guerra total | N1 | `GuerraN1` | Mantém autorização diplomática existente |
| Grupo de porta-aviões | N3–N1 | `ZonaPortaAvioes` | Porta-aviões, 2 escoltas e 1 aeronave; grupo deve existir no snapshot da equipe |
| Patrulha submarina | N4–N1 | `PatrulhaSubmarino` | Ao menos um objeto com `ControleSubmarino` |
| Invasão anfíbia | N2–N1 | `EmbarqueTransporte` → `DesembarqueAnfibio` | Transporte de tropas e uma unidade terrestre; exige guerra formal no gate atual |
| Visita presidencial | N4 | `HelipontoDiplomatico` → `RecepcaoPresidencial` | Unidade marcada com `IA03MarcaPresidencial` |
| Defesa de objetivo | N4–N1 | `TensaoN4`, `AvancoN3`, `ConflitoN2`, `GuerraN1` | Ordem defensiva e Creaty compatível com o nível |

Foram adicionados 48 Creatys estáticos à cena: por equipe, um ponto genérico de paz; patrulha terrestre, naval e aérea em N4–N1; patrulha submarina em N4–N1; zona de porta-aviões em N3–N1; um par embarque/desembarque em N2 e outro em N1; e um par heliponto/recepção em N4. As rotas ficam vinculadas por referência direta aos Creatys da mesma equipe e nível. IDs mantêm o padrão exclusivo `IA03-TEST-<PAÍS>-...`.

## Limites importantes

Esta etapa completa o catálogo, os pontos estratégicos e os critérios de seleção/composição que já existem no código. As ordens ainda passam pelo `IA_BrainMaster` e pelo `CommandQueue`; nada aqui contorna a autorização de guerra, reconhecimento de alvos ou diplomacia.

O teste `IA03MissionRouteDestinationEditModeTests` cobre que os dois tipos de ponto final encadeado sejam aceitos sem incluí-los como pontos iniciais, e que pontos genéricos não atravessem essa exceção.

O envio de um grupo para os Creatys de embarque/desembarque não implementa, por si só, carregamento e descarga de tropas. A visita presidencial está configurada como deslocamento entre pontos, não como uma cerimônia com sistema próprio; o grupo de porta-aviões usa as ordens disponíveis, sem criar operações de lançamento/recuperação. Esses fluxos específicos dependem das mecânicas existentes dos respectivos sistemas e não são introduzidos por este catálogo. A cena contém pontos de teste colocados perto das âncoras manuais atuais; sua aderência a terreno, costa e navegação precisa ser confirmada no Unity.

## Validação pendente

Foi feita somente validação estática dos IDs, serialização e referências de assets. O Unity não foi aberto e nenhum teste foi executado nesta rodada. O teste acima foi adicionado, mas permanece sem execução. A validação autorizada deverá confirmar importação dos assets, registro dos Creatys, seleção em N4/N3/N2/N1, composição real de unidades e passagem pelas rotas encadeadas.
