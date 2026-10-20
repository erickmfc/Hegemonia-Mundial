# Auditoria das capturas de campanha longa

## Escopo e evidências

Análise somente leitura dos CSVs existentes em `GlobalMapPerformance`, gravados
em 10/10/2026. Nenhuma sessão foi reiniciada e nenhum código, cena ou prefab foi
alterado nesta auditoria.

Os cenários do pedido são velocidades da simulação: A = unidades paradas a
100%, B = unidades navegando a 100%, C = unidades paradas a 200% e D = unidades
navegando a 200%. “100/200” não representa quantidade de unidades.

| Caso | Captura usada | Duração | Escala | Situação |
| --- | --- | ---: | ---: | --- |
| A | `A-paradas-100-6navios-retry3-90m-20261010-210028.csv` | 90,5 min; 5.411 amostras | 1,00 | Completa |
| B | `B-movendo-100-6navios-20261010-212648-20261010-225750.csv` | 90,8 min; 5.440 amostras | 1,00 | Completa |
| C | `C-paradas-200-6navios-20261010-232457.csv` | 19,0 min; 1.140 amostras | 2,00 | Incompleta |
| D | — | — | 2,00 | Não há captura identificada |

Há outra execução A de 85,6 minutos, abaixo dos 90 minutos. As duas cópias
`C-paradas-200-6navios-20261010-232457.csv` e
`C-paradas-200-6navios-20261010-232602.csv` têm SHA-256 idêntico; contam como
uma captura, não duas.

## Checkpoints A/B

Cada célula vem da amostra mais próxima do minuto indicado (a captura grava em
intervalos de aproximadamente um segundo). O ponto de 0 min é a primeira
amostra, perto de 1 s, ainda durante a geração inicial dos tiles. `FPS` e
`P95` são os valores daquela amostra, não médias de uma janela de 30 segundos.

| Caso | Minuto | FPS | Frame P95 (ms) | Memória Unity (GB) | Mono (GB) | GameObjects | Unidades / navais | Tiles / árvores |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 0 | 38,7 | 62,4 | 1,91 | 0,96 | 8.926 | 23 / 6 | 5 / 23.128 |
| A | 30 | 46,2 | 24,9 | 1,91 | 1,02 | 8.926 | 23 / 6 | 5 / 23.128 |
| A | 60 | 44,6 | 25,1 | 1,91 | 1,02 | 8.926 | 23 / 6 | 5 / 23.128 |
| A | 90 | 43,4 | 29,5 | 1,91 | 0,99 | 8.926 | 23 / 6 | 5 / 23.128 |
| B | 0 | 31,8 | 60,8 | 1,91 | 1,05 | 8.926 | 23 / 6 | 5 / 23.128 |
| B | 30 | 35,3 | 50,1 | 1,91 | 0,97 | 8.926 | 23 / 6 | 5 / 23.128 |
| B | 60 | 39,7 | 38,3 | 1,91 | 1,02 | 8.926 | 23 / 6 | 5 / 23.128 |
| B | 90 | 40,6 | 37,0 | 1,91 | 1,00 | 8.926 | 23 / 6 | 5 / 23.128 |

## Comportamento durante as execuções completas

As linhas de cada CSV são amostras de aproximadamente um segundo. Os
percentis abaixo descrevem a distribuição desses registros; a contagem de
“intervalos” conta amostras em que o pior frame daquele intervalo passou do
limite, não o número exato de frames individuais.

| Caso | FPS médio | P95 do frame P95 (ms) | Pior frame observado (ms) | Intervalos >100 ms | >250 ms | >1.000 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 42,6 | 56,5 | 2.538,5 | 705 / 5.411 | 242 / 5.411 | 28 / 5.411 |
| B | 40,5 | 66,5 | 3.250,3 | 415 / 5.440 | 95 / 5.440 | 9 / 5.440 |

Nos checkpoints A/B, FPS não cai progressivamente: A vai de 38,7 no primeiro
registro para 43,4 aos 90 min; B vai de 31,8 para 40,6. Memória Unity fica em
1,91 GB nos pontos amostrados, Mono varia sem tendência de crescimento e a
contagem de objetos/unidades/navios permanece constante. Isso não elimina os
hitches longos mostrados pela distribuição completa.

C registrou FPS médio de 34,7, P95 do frame P95 de 74,3 ms e pior frame de
2.046,8 ms durante os 19 minutos disponíveis. A amostra inicial tem 22 unidades
e 6 navios; o contador de objetos varia de 8.580 a 8.954 e sistemas de
partículas de 0 a 15. Não há checkpoints aos 30/60/90 min, então C não prova
estabilidade de campanha longa a 200% nem explica essa variação curta.

## Monitor de processo e limites dos dados

- Os monitores externos não registram amostras com `editor_responding=False`
  nas execuções A retry3, B ou C.
- Em A retry3, RAM livre variou de 1,4 a 3,5 GB e working set máximo foi
  4,39 GB. Em B, RAM livre foi de 1.927 a 5.176 MB e working set máximo foi
  4.420 MB. O monitor C cobre apenas cerca de 19,6 min.
- O monitor externo de B tem 181 amostras de CPU: mediana de 149,31% de um
  núcleo e 12,44% normalizado por todos os núcleos; P95 de 156,08% e 13,01%,
  respectivamente. Essa série não está sincronizada aos checkpoints e não há
  equivalente em A retry3/C para comparação entre cenários.
- `GlobalWorldDiagnostics` já grava FPS, frame médio/P95/pior, Main Thread,
  memória Unity/Mono/driver, GC Alloc e contadores de objetos/unidades. Nos
  CSVs examinados, GC Alloc consta como zero nos intervalos. Render Thread,
  GPU e working set do CSV estão indisponíveis (`-1`); o monitor externo cobre
  working set em A retry3, B e C, mas não substitui CPU/GPU sincronizados em
  todos os casos.
- Os CSVs não identificam qual subsistema causou cada hitch. Também não trazem
  contagens de componentes, filas/listas, inscrições/eventos, scans de radar,
  buscas globais, consultas físicas, recálculos de rota ou custos por
  `Update`/`FixedUpdate`/scheduler/logística. Portanto não provam vazamento nem
  autorizam uma otimização causal.

## Conclusão e pendências

Não há evidência de degradação monotônica de FPS ou crescimento contínuo de
memória/objetos em A/B. Há hitches ocasionais acima de 1 s, mas a captura não
atribui causa. C é curta e D não existe; a comparação 100% versus 200% e o
cenário de movimento a 200% permanecem incompletos. Não foi feita correção de
desempenho nesta rodada.

Como o usuário indicou que este teste já era suficiente por agora, as lacunas
ficam registradas sem iniciar novas sessões longas. Para concluir a matriz
original, ainda faltam C até 90 min e D, com captura de CPU/GPU e marcadores por
subsistema alinhados aos checkpoints.
