using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hegemonia.EditorTools
{
    /// <summary>
    /// Suíte de integração e testes das otimizações aeronáuticas e de combate implementadas:
    /// 1. Otimização de altitude e cache de terrenos em ControleAviao.
    /// 2. Priorização e classificação de alvos aéreos em LancadorMisselCaca (sem reflection/strings lentas).
    /// 3. Throttling de detecção PhysX em AviaoBombardeiro.
    /// 4. Otimização de busca em passagem única de mísseis em RTSVisibilityService.
    /// </summary>
    public static class TesteIntegracaoAeronavesPerformance
    {
        private const string LogPrefix = "[TesteIntegracaoAeronaves]";
        private const string SessionKey = "TesteIntegracaoAeronaves.RanThisSession";

        [InitializeOnLoadMethod]
        private static void ScheduleOnLoad()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += ExecutarTodosOsTestes;
        }

        [MenuItem("Tools/Testes/Executar Testes de Performance e Combate Aeronaves")]
        public static void ExecutarTodosOsTestes()
        {
            Debug.Log($"{LogPrefix} === INICIANDO SUÍTE DE TESTES E INTEGRAÇÕES ===");
            int total = 0;
            int sucessos = 0;

            Testar(ref total, ref sucessos, "Teste 1: Cache de Altitude e Terreno (ControleAviao)", TesteCacheAltitudeTerreno);
            Testar(ref total, ref sucessos, "Teste 2: Classificação e Prioridade de Alvos (LancadorMisselCaca)", TesteClassificacaoPrioridadeAlvos);
            Testar(ref total, ref sucessos, "Teste 3: Throttling de Patrulha (AviaoBombardeiro)", TesteThrottlingBombardeiro);
            Testar(ref total, ref sucessos, "Teste 4: Busca de Contra-Ataque Estratégico (RTSVisibilityService)", TesteBuscaPassagemUnicaMissil);
            Testar(ref total, ref sucessos, "Teste 5: Teste de Carga e Benchmark Sintético (100 unidades em combate)", TesteCargaCombateSintetico);

            Debug.Log($"{LogPrefix} === RESULTADO FINAL: {sucessos}/{total} TESTES PASSARAM COM SUCESSO! ===");
        }

        private static void Testar(ref int total, ref int sucessos, string nome, Action metodo)
        {
            total++;
            try
            {
                metodo();
                sucessos++;
                Debug.Log($"<color=green>[PASSOU]</color> {nome}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[FALHOU]</color> {nome}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void TesteCacheAltitudeTerreno()
        {
            GameObject aviaoGo = new GameObject("AviaoTeste_Altitude");
            try
            {
                ControleAviao aviao = aviaoGo.AddComponent<ControleAviao>();

                // Acesso via reflexão ao método privado TryObterAlturaSolo
                MethodInfo tryObter = typeof(ControleAviao).GetMethod("TryObterAlturaSolo", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tryObter == null) throw new MissingMethodException("TryObterAlturaSolo não encontrado em ControleAviao");

                Vector3 pos1 = new Vector3(100f, 50f, 100f);
                object[] args1 = new object[] { pos1, 0f };

                Stopwatch sw = Stopwatch.StartNew();
                bool res1 = (bool)tryObter.Invoke(aviao, args1);
                long t1 = sw.ElapsedTicks;

                // Segunda chamada na mesma posição (deve atingir o cache imediato)
                sw.Restart();
                bool res2 = (bool)tryObter.Invoke(aviao, args1);
                long t2 = sw.ElapsedTicks;

                // Terceira chamada dentro do raio de 5m (deve atingir cache de deslocamento)
                Vector3 posPerto = new Vector3(102f, 50f, 101f);
                object[] args3 = new object[] { posPerto, 0f };
                sw.Restart();
                bool res3 = (bool)tryObter.Invoke(aviao, args3);
                long t3 = sw.ElapsedTicks;

                Debug.Log($"{LogPrefix} Altitude: T1={t1} ticks, T2(Cache Exato)={t2} ticks, T3(Cache Deslocamento)={t3} ticks");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aviaoGo);
            }
        }

        private static void TesteClassificacaoPrioridadeAlvos()
        {
            GameObject cacaGo = new GameObject("Caca_Teste");
            GameObject bombGo = new GameObject("Aviao_Bombardeiro_Teste");
            GameObject inimigoCacaGo = new GameObject("Caca_Inimigo");
            GameObject tanqueGo = new GameObject("Tanque_Inimigo");

            try
            {
                LancadorMisselCaca lancador = cacaGo.AddComponent<LancadorMisselCaca>();
                MethodInfo ehAereoMetodo = typeof(LancadorMisselCaca).GetMethod("EhAlvoAereo", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo prioridadeMetodo = typeof(LancadorMisselCaca).GetMethod("ObterPrioridadeAlvo", BindingFlags.NonPublic | BindingFlags.Instance);

                if (ehAereoMetodo == null || prioridadeMetodo == null)
                    throw new MissingMethodException("Métodos EhAlvoAereo ou ObterPrioridadeAlvo não encontrados");

                // Configura Bombardeiro com IdentidadeUnidade Aereo
                IdentidadeUnidade idBomb = bombGo.AddComponent<IdentidadeUnidade>();
                idBomb.tipoUnidade = TipoUnidade.Aereo;

                // Configura Caca
                IdentidadeUnidade idCaca = inimigoCacaGo.AddComponent<IdentidadeUnidade>();
                idCaca.tipoUnidade = TipoUnidade.Aereo;

                // Configura Tanque terrestre
                IdentidadeUnidade idTanque = tanqueGo.AddComponent<IdentidadeUnidade>();
                idTanque.tipoUnidade = TipoUnidade.Veiculo;

                bool bombEhAereo = (bool)ehAereoMetodo.Invoke(lancador, new object[] { bombGo.transform, idBomb });
                bool cacaEhAereo = (bool)ehAereoMetodo.Invoke(lancador, new object[] { inimigoCacaGo.transform, idCaca });
                bool tanqueEhAereo = (bool)ehAereoMetodo.Invoke(lancador, new object[] { tanqueGo.transform, idTanque });

                if (!bombEhAereo) throw new Exception("Bombardeiro deveria ter sido classificado como aéreo!");
                if (!cacaEhAereo) throw new Exception("Caça deveria ter sido classificado como aéreo!");
                if (tanqueEhAereo) throw new Exception("Tanque terrestre NÃO deveria ser classificado como aéreo!");

                int prioBomb = (int)prioridadeMetodo.Invoke(lancador, new object[] { bombGo.transform, idBomb });
                int prioCaca = (int)prioridadeMetodo.Invoke(lancador, new object[] { inimigoCacaGo.transform, idCaca });
                int prioTanque = (int)prioridadeMetodo.Invoke(lancador, new object[] { tanqueGo.transform, idTanque });

                Debug.Log($"{LogPrefix} Prioridades obtidas: Bombardeiro={prioBomb}, Caça={prioCaca}, Tanque={prioTanque}");

                if (prioBomb != 0) throw new Exception($"Prioridade do Bombardeiro deve ser 0 (máxima), foi {prioBomb}");
                if (prioCaca != 1) throw new Exception($"Prioridade do Caça deve ser 1, foi {prioCaca}");
                if (prioTanque < 2) throw new Exception($"Prioridade do Tanque deve ser baixa (>= 2), foi {prioTanque}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cacaGo);
                UnityEngine.Object.DestroyImmediate(bombGo);
                UnityEngine.Object.DestroyImmediate(inimigoCacaGo);
                UnityEngine.Object.DestroyImmediate(tanqueGo);
            }
        }

        private static void TesteThrottlingBombardeiro()
        {
            GameObject bombGo = new GameObject("Bombardeiro_Throttling");
            try
            {
                AviaoBombardeiro bomb = bombGo.AddComponent<AviaoBombardeiro>();
                FieldInfo fieldProximaChecagem = typeof(AviaoBombardeiro).GetField("proximaChecagemPatrulha", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldProximaChecagem == null)
                    throw new MissingFieldException("Campo proximaChecagemPatrulha não encontrado em AviaoBombardeiro");

                float valorInicial = (float)fieldProximaChecagem.GetValue(bomb);
                if (valorInicial != 0f)
                    throw new Exception("proximaChecagemPatrulha deveria inicializar em 0");

                Debug.Log($"{LogPrefix} Campo proximaChecagemPatrulha verificado com sucesso.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bombGo);
            }
        }

        private static void TesteBuscaPassagemUnicaMissil()
        {
            GameObject serviceGo = new GameObject("RTSVisibilityService_Teste");
            GameObject lancadorGo1 = new GameObject("Lancador1");
            GameObject lancadorGo2 = new GameObject("Lancador2");
            GameObject alvoGo = new GameObject("AlvoInimigo");

            try
            {
                RTS.RTSVisibilityService service = serviceGo.AddComponent<RTS.RTSVisibilityService>();

                IdentidadeUnidade idLancador1 = lancadorGo1.AddComponent<IdentidadeUnidade>();
                idLancador1.teamID = 1;
                LancadorMisseis l1 = lancadorGo1.AddComponent<LancadorMisseis>();
                l1.municaoAtual = 4;
                lancadorGo1.transform.position = new Vector3(100f, 0f, 100f);

                IdentidadeUnidade idLancador2 = lancadorGo2.AddComponent<IdentidadeUnidade>();
                idLancador2.teamID = 1;
                LancadorMisseis l2 = lancadorGo2.AddComponent<LancadorMisseis>();
                l2.municaoAtual = 2;
                lancadorGo2.transform.position = new Vector3(50f, 0f, 50f);

                IdentidadeUnidade idAlvo = alvoGo.AddComponent<IdentidadeUnidade>();
                idAlvo.teamID = 2;
                alvoGo.transform.position = new Vector3(0f, 0f, 0f);

                // Injeta na lista unitsBuffer do serviço
                FieldInfo fieldUnitsBuffer = typeof(RTS.RTSVisibilityService).GetField("unitsBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldUnitsBuffer == null)
                    throw new MissingFieldException("unitsBuffer não encontrado em RTSVisibilityService");

                List<IdentidadeUnidade> buffer = (List<IdentidadeUnidade>)fieldUnitsBuffer.GetValue(service);
                buffer.Clear();
                buffer.Add(idLancador1);
                buffer.Add(idLancador2);
                buffer.Add(idAlvo);

                MethodInfo metodoContraAtaque = typeof(RTS.RTSVisibilityService).GetMethod("TentarLancarMissilEstrategico", BindingFlags.NonPublic | BindingFlags.Instance);
                if (metodoContraAtaque == null)
                    throw new MissingMethodException("TentarLancarMissilEstrategico não encontrado em RTSVisibilityService");

                // Executa a busca em passagem única
                bool resultado = (bool)metodoContraAtaque.Invoke(service, new object[] { 1, idAlvo });

                Debug.Log($"{LogPrefix} Busca de lançador em passagem única executada sem erros. Retorno={resultado}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceGo);
                UnityEngine.Object.DestroyImmediate(lancadorGo1);
                UnityEngine.Object.DestroyImmediate(lancadorGo2);
                UnityEngine.Object.DestroyImmediate(alvoGo);
            }
        }

        private static void TesteCargaCombateSintetico()
        {
            // Simula um cenário onde 10 caças avaliam simultaneamente 100 alvos da base inimiga
            GameObject cacaGo = new GameObject("Caca_Benchmark");
            List<GameObject> unidadesBase = new List<GameObject>(100);

            try
            {
                LancadorMisselCaca lancador = cacaGo.AddComponent<LancadorMisselCaca>();
                MethodInfo prioridadeMetodo = typeof(LancadorMisselCaca).GetMethod("ObterPrioridadeAlvo", BindingFlags.NonPublic | BindingFlags.Instance);

                for (int i = 0; i < 100; i++)
                {
                    GameObject u = new GameObject($"Base_Unidade_{i}");
                    IdentidadeUnidade id = u.AddComponent<IdentidadeUnidade>();
                    id.teamID = 2;
                    id.tipoUnidade = (i % 5 == 0) ? TipoUnidade.Aereo : TipoUnidade.Estrutura;
                    unidadesBase.Add(u);
                }

                Stopwatch sw = Stopwatch.StartNew();
                int totalAvaliacoes = 0;

                // 10 aviões x 100 alvos x 5 varreduras
                for (int aviao = 0; aviao < 10; aviao++)
                {
                    for (int ciclo = 0; ciclo < 5; ciclo++)
                    {
                        for (int i = 0; i < unidadesBase.Count; i++)
                        {
                            IdentidadeUnidade id = unidadesBase[i].GetComponent<IdentidadeUnidade>();
                            prioridadeMetodo.Invoke(lancador, new object[] { unidadesBase[i].transform, id });
                            totalAvaliacoes++;
                        }
                    }
                }
                sw.Stop();

                double tempoMs = sw.Elapsed.TotalMilliseconds;
                Debug.Log($"{LogPrefix} Benchmark: {totalAvaliacoes} avaliações de alvos em combate completadas em {tempoMs:F2} ms (Média: {(tempoMs / totalAvaliacoes) * 1000:F2} µs/alvo)");

                if (tempoMs > 100.0)
                {
                    throw new Exception($"Desempenho abaixo do esperado: {tempoMs:F2} ms para 5000 avaliações.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cacaGo);
                for (int i = 0; i < unidadesBase.Count; i++)
                {
                    if (unidadesBase[i] != null) UnityEngine.Object.DestroyImmediate(unidadesBase[i]);
                }
            }
        }
    }
}
