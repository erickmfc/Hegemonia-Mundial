using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum CamadaSimulacao
    {
        Visual,
        Critica,
        Operacional,
        Estrategica,
        Adormecida
    }

    public enum PerfilDesempenhoSimulacao
    {
        Economico,
        Balanceado,
        AltoDesempenho,
        Automatico
    }

    public readonly struct RegistroTarefaSimulacao
    {
        public readonly string Id;
        public readonly int Dono;
        public readonly CamadaSimulacao Camada;
        public readonly float Frequencia;
        public readonly float PrazoMaximo;
        public readonly float UltimaExecucao;
        public readonly float ProximaExecucao;
        public readonly float CustoRecenteMs;
        public readonly bool Aguardando;
        public readonly bool DespertarSolicitado;

        public RegistroTarefaSimulacao(string id, int dono, CamadaSimulacao camada, float frequencia,
            float prazoMaximo, float ultimaExecucao, float proximaExecucao, float custoRecenteMs,
            bool aguardando, bool despertarSolicitado)
        {
            Id = id;
            Dono = dono;
            Camada = camada;
            Frequencia = frequencia;
            PrazoMaximo = prazoMaximo;
            UltimaExecucao = ultimaExecucao;
            ProximaExecucao = proximaExecucao;
            CustoRecenteMs = custoRecenteMs;
            Aguardando = aguardando;
            DespertarSolicitado = despertarSolicitado;
        }
    }

    public readonly struct SnapshotSimulacao
    {
        public readonly int TarefasRegistradas;
        public readonly int TarefasExecutadas;
        public readonly int TarefasAguardando;
        public readonly int DespertaresImediatos;
        public readonly int DespertaresAgrupados;
        public readonly int DespertaresDescartados;
        public readonly int EstouroOrcamento;
        public readonly int Starvation;
        public readonly float OrcamentoMs;
        public readonly float TempoTotalMs;
        public readonly float TempoCriticoMs;
        public readonly float MaiorEspera;
        public readonly float MediaCpuMs;

        public SnapshotSimulacao(int tarefasRegistradas, int tarefasExecutadas, int tarefasAguardando,
            int despertaresImediatos, int despertaresAgrupados, int despertaresDescartados,
            int estouroOrcamento, int starvation, float orcamentoMs, float tempoTotalMs,
            float tempoCriticoMs, float maiorEspera, float mediaCpuMs)
        {
            TarefasRegistradas = tarefasRegistradas;
            TarefasExecutadas = tarefasExecutadas;
            TarefasAguardando = tarefasAguardando;
            DespertaresImediatos = despertaresImediatos;
            DespertaresAgrupados = despertaresAgrupados;
            DespertaresDescartados = despertaresDescartados;
            EstouroOrcamento = estouroOrcamento;
            Starvation = starvation;
            OrcamentoMs = orcamentoMs;
            TempoTotalMs = tempoTotalMs;
            TempoCriticoMs = tempoCriticoMs;
            MaiorEspera = maiorEspera;
            MediaCpuMs = mediaCpuMs;
        }
    }

    /// <summary>
    /// Agendador global de tarefas de simulacao. Ele distribui callbacks ja
    /// existentes; nao decide estrategia, nao movimenta unidades e nao possui
    /// estado de ordens. ControleOrdemMovimento continua sendo a autoridade
    /// unica sobre ordens.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    public sealed class OrquestradorGlobalSimulacao : MonoBehaviour
    {
        private sealed class Tarefa
        {
            public string Id;
            public int Dono;
            public CamadaSimulacao Camada;
            public float Frequencia;
            public float PrazoMaximo;
            public float UltimaExecucao = -1f;
            public float ProximaExecucao;
            public float CustoRecenteMs;
            public float MaiorEspera;
            public bool Aguardando;
            public bool DespertarSolicitado;
            public bool Executando;
            public bool Ativo = true;
            public UnityEngine.Object Proprietario;
            public Func<float, bool> Callback;
        }

        private readonly Dictionary<string, Tarefa> tarefas = new Dictionary<string, Tarefa>(256, StringComparer.Ordinal);
        private readonly List<Tarefa>[] filas =
        {
            new List<Tarefa>(32), new List<Tarefa>(64), new List<Tarefa>(128),
            new List<Tarefa>(64), new List<Tarefa>(128)
        };
        private readonly Stopwatch cronometro = new Stopwatch();
        private float mediaCpuMs;
        private float tempoDesdePerfil;
        private float pressaoAcumulada;
        private float recuperacaoAcumulada;
        private int indiceRoundRobin;
        private float ultimaPausaOuCarga;
        private int tarefasExecutadas;
        private int tarefasAguardando;
        private int despertaresImediatos;
        private int despertaresAgrupados;
        private int despertaresDescartados;
        private int estourosOrcamento;
        private int starvation;
        private float tempoTotalMs;
        private float tempoCriticoMs;
        private float maiorEspera;
        private bool pausado;

        public static OrquestradorGlobalSimulacao Instancia { get; private set; }

        [Header("Nucleo")]
        [SerializeField] private bool habilitarNucleo = true;
        [SerializeField, Min(0.25f)] private float orcamentoBalanceadoMs = 3.5f;
        [SerializeField, Min(0f)] private float reservaCriticaMs = 0.6f;
        [SerializeField, Min(0.1f)] private float limiteFuroCriticoMs = 0.75f;
        [SerializeField, Min(1)] private int maxTarefasPorFrame = 64;
#pragma warning disable CS0414
        [SerializeField, Min(0)] private int maxTicksAtrasados = 1;
#pragma warning restore CS0414

        [Header("Perfis")]
        [SerializeField] private PerfilDesempenhoSimulacao perfil = PerfilDesempenhoSimulacao.Balanceado;
        [SerializeField, Min(0.1f)] private float intervaloTrocaPerfil = 4f;
        [SerializeField, Min(0.1f)] private float histereseMs = 1.5f;
        [SerializeField, Min(0.1f)] private float mediaCpuJanela = 1.2f;

        [Header("Feature flags independentes")]
        [SerializeField] private bool habilitarAeronaves = true;
        [SerializeField] private bool habilitarTerrestresLogistica = true;
        [SerializeField] private bool habilitarNavais = true;
        [SerializeField] private bool habilitarEstrategica = true;
        [SerializeField] private bool habilitarPierEventos = true;
        [SerializeField] private bool habilitarDormencia = true;
        [SerializeField] private bool habilitarPooling = true;
        [SerializeField] private bool habilitarLimpezaSegura = true;
        [SerializeField] private bool habilitarPerfilAutomatico = true;

        public float OrcamentoAtualMs => ResolverOrcamento();
        public PerfilDesempenhoSimulacao PerfilAtual => perfil;
        public int TarefasRegistradas => tarefas.Count;
        public bool HabilitarAeronaves => habilitarAeronaves;
        public bool HabilitarTerrestresLogistica => habilitarTerrestresLogistica;
        public bool HabilitarNavais => habilitarNavais;
        public bool HabilitarEstrategica => habilitarEstrategica;
        public bool HabilitarPierEventos => habilitarPierEventos;
        public bool HabilitarDormencia => habilitarDormencia;
        public bool HabilitarPooling => habilitarPooling;
        public bool HabilitarLimpezaSegura => habilitarLimpezaSegura;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instancia != null) return;
            GameObject objeto = new GameObject("[OrquestradorGlobalSimulacao]");
            DontDestroyOnLoad(objeto);
            objeto.AddComponent<OrquestradorGlobalSimulacao>();
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
            DontDestroyOnLoad(gameObject);
            ultimaPausaOuCarga = Time.unscaledTime;
        }

        private void OnEnable()
        {
            OrquestradorGlobalOrdens.DespertarSolicitado += AoDespertarOrdem;
        }

        private void OnDisable()
        {
            OrquestradorGlobalOrdens.DespertarSolicitado -= AoDespertarOrdem;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        private void AoDespertarOrdem(string id)
        {
            // A ordem permanece sob ControleOrdemMovimento; este evento
            // somente acorda uma tarefa que tenha sido registrada pelo executor.
            SolicitarTickImediato("ordem/" + id);
        }

        private void Update()
        {
            if (!habilitarNucleo || pausado) return;
            float agora = Time.unscaledTime;
            AtualizarPerfilAutomatico(agora);
            ExecutarCamada(CamadaSimulacao.Visual, agora, ResolverOrcamento() * 0.15f, false);
            ExecutarCamada(CamadaSimulacao.Critica, agora, reservaCriticaMs, true);
            float restante = Mathf.Max(0f, ResolverOrcamento() - tempoTotalMs);
            ExecutarCamada(CamadaSimulacao.Operacional, agora, restante * 0.50f, false);
            ExecutarCamada(CamadaSimulacao.Estrategica, agora, restante * 0.35f, false);
            ExecutarCamada(CamadaSimulacao.Adormecida, agora, restante * 0.15f, false);
            PublicarMetricas();
        }

        public bool Registrar(string id, int dono, CamadaSimulacao camada, float frequencia,
            float prazoMaximo, Func<float, bool> callback, float agora = -1f)
        {
            if (string.IsNullOrWhiteSpace(id) || callback == null) return false;
            float now = agora >= 0f ? agora : Time.unscaledTime;
            frequencia = Mathf.Max(0f, frequencia);
            prazoMaximo = Mathf.Max(frequencia, prazoMaximo);
            if (tarefas.TryGetValue(id, out Tarefa existente))
            {
                if (existente.Dono != dono || existente.Camada != camada || existente.Callback != callback)
                    return false;
                existente.Frequencia = frequencia;
                existente.PrazoMaximo = prazoMaximo;
                return true;
            }
            Tarefa tarefa = new Tarefa
            {
                Id = id, Dono = dono, Camada = camada, Frequencia = frequencia,
                PrazoMaximo = prazoMaximo, ProximaExecucao = now, Callback = callback
            };
            tarefas.Add(id, tarefa);
            filas[(int)camada].Add(tarefa);
            return true;
        }

        public bool RegistrarComProprietario(string id, int dono, CamadaSimulacao camada,
            float frequencia, float prazoMaximo, UnityEngine.Object proprietario,
            Func<float, bool> callback, float agora = -1f)
        {
            if (!Registrar(id, dono, camada, frequencia, prazoMaximo, callback, agora)) return false;
            tarefas[id].Proprietario = proprietario;
            return true;
        }

        public bool Remover(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !tarefas.TryGetValue(id, out Tarefa tarefa)) return false;
            tarefa.Ativo = false;
            tarefas.Remove(id);
            filas[(int)tarefa.Camada].Remove(tarefa);
            return true;
        }

        public bool SolicitarTickImediato(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !tarefas.TryGetValue(id, out Tarefa tarefa) || !tarefa.Ativo)
            {
                despertaresDescartados++;
                return false;
            }
            if (tarefa.DespertarSolicitado)
            {
                despertaresAgrupados++;
                return true;
            }
            tarefa.DespertarSolicitado = true;
            tarefa.ProximaExecucao = Mathf.Min(tarefa.ProximaExecucao, Time.unscaledTime);
            despertaresImediatos++;
            return true;
        }

        public void DefinirPausado(bool valor)
        {
            if (pausado == valor) return;
            pausado = valor;
            if (!pausado)
            {
                ultimaPausaOuCarga = Time.unscaledTime;
                foreach (Tarefa tarefa in tarefas.Values)
                {
                    tarefa.ProximaExecucao = Mathf.Max(tarefa.ProximaExecucao, ultimaPausaOuCarga);
                    tarefa.DespertarSolicitado = false;
                }
            }
        }

        public bool TentarObter(string id, out RegistroTarefaSimulacao registro)
        {
            if (tarefas.TryGetValue(id, out Tarefa tarefa))
            {
                registro = CriarRegistro(tarefa);
                return true;
            }
            registro = default;
            return false;
        }

        public SnapshotSimulacao ObterSnapshot()
        {
            return new SnapshotSimulacao(tarefas.Count, tarefasExecutadas, tarefasAguardando,
                despertaresImediatos, despertaresAgrupados, despertaresDescartados, estourosOrcamento,
                starvation, ResolverOrcamento(), tempoTotalMs, tempoCriticoMs, maiorEspera, mediaCpuMs);
        }

        public void LimparTarefasParaTeste()
        {
            tarefas.Clear();
            for (int i = 0; i < filas.Length; i++) filas[i].Clear();
        }

        public void ExecutarAgoraParaTeste(float agora)
        {
            if (!habilitarNucleo) return;
            ExecutarCamada(CamadaSimulacao.Visual, agora, ResolverOrcamento(), false);
            ExecutarCamada(CamadaSimulacao.Critica, agora, ResolverOrcamento(), true);
            ExecutarCamada(CamadaSimulacao.Operacional, agora, ResolverOrcamento(), false);
            ExecutarCamada(CamadaSimulacao.Estrategica, agora, ResolverOrcamento(), false);
            ExecutarCamada(CamadaSimulacao.Adormecida, agora, ResolverOrcamento(), false);
        }

        private void ExecutarCamada(CamadaSimulacao camada, float agora, float orcamento, bool critica)
        {
            if (orcamento <= 0f) return;
            List<Tarefa> fila = filas[(int)camada];
            if (fila.Count == 0) return;
            cronometro.Restart();
            float permitido = Mathf.Max(0.05f, orcamento);
            int executadas = 0;
            int inicio = Mathf.Abs(indiceRoundRobin++) % fila.Count;
            for (int offset = 0; offset < fila.Count && executadas < maxTarefasPorFrame; offset++)
            {
                Tarefa tarefa = fila[(inicio + offset) % fila.Count];
                if (!tarefa.Ativo) continue;
                if (!ReferenceEquals(tarefa.Proprietario, null) && tarefa.Proprietario == null)
                {
                    Remover(tarefa.Id);
                    continue;
                }
                if (tarefa.Executando || agora < tarefa.ProximaExecucao) continue;
                float espera = Mathf.Max(0f, agora - tarefa.ProximaExecucao);
                tarefa.Aguardando = espera > tarefa.PrazoMaximo;
                if (tarefa.Aguardando) starvation++;
                tarefa.MaiorEspera = Mathf.Max(tarefa.MaiorEspera, espera);
                maiorEspera = Mathf.Max(maiorEspera, espera);
                tarefa.Executando = true;
                tarefa.DespertarSolicitado = false;
                long inicioMedicao = Stopwatch.GetTimestamp();
                bool manter = true;
                try { manter = tarefa.Callback(agora); }
                catch (Exception excecao)
                {
                    manter = true;
                    DiagnosticoDesempenhoJogo.RegistrarExcecao("OrquestradorGlobalSimulacao." + tarefa.Id, excecao);
                }
                float custo = (float)((Stopwatch.GetTimestamp() - inicioMedicao) * 1000.0 / Stopwatch.Frequency);
                tarefa.CustoRecenteMs = custo;
                tarefa.UltimaExecucao = agora;
                tarefa.ProximaExecucao = agora + ResolverIntervalo(tarefa);
                tarefa.Executando = false;
                tarefa.Aguardando = false;
                if (!manter) Remover(tarefa.Id);
                executadas++;
                tarefasExecutadas++;
                if (critica) tempoCriticoMs += custo;
                if (cronometro.Elapsed.TotalMilliseconds > permitido)
                {
                    estourosOrcamento++;
                    if (!critica || cronometro.Elapsed.TotalMilliseconds > permitido + limiteFuroCriticoMs) break;
                }
            }
            float decorrido = (float)cronometro.Elapsed.TotalMilliseconds;
            tempoTotalMs += decorrido;
            if (decorrido > permitido && executadas == 0) tarefasAguardando++;
        }

        private float ResolverIntervalo(Tarefa tarefa)
        {
            if (tarefa.Frequencia <= 0f) return 0f;
            float multiplicador = perfil == PerfilDesempenhoSimulacao.Economico ? 1.65f
                : perfil == PerfilDesempenhoSimulacao.AltoDesempenho ? 0.78f : 1f;
            if (mediaCpuMs > ResolverOrcamento() * 2.25f && tarefa.Camada >= CamadaSimulacao.Estrategica) multiplicador *= 1.35f;
            return Mathf.Max(0.01f, tarefa.Frequencia * multiplicador);
        }

        private float ResolverOrcamento()
        {
            switch (perfil)
            {
                case PerfilDesempenhoSimulacao.Economico: return 2f;
                case PerfilDesempenhoSimulacao.AltoDesempenho: return 5f;
                default: return orcamentoBalanceadoMs;
            }
        }

        private void AtualizarPerfilAutomatico(float agora)
        {
            float frameMs = Mathf.Clamp(Time.unscaledDeltaTime * 1000f, 0f, 250f);
            mediaCpuMs = mediaCpuMs <= 0f ? frameMs : Mathf.Lerp(mediaCpuMs, frameMs, Time.unscaledDeltaTime / Mathf.Max(0.1f, mediaCpuJanela));
            if (perfil != PerfilDesempenhoSimulacao.Automatico || !habilitarPerfilAutomatico) return;
            tempoDesdePerfil += Time.unscaledDeltaTime;
            if (tempoDesdePerfil < intervaloTrocaPerfil) return;
            float alvo = orcamentoBalanceadoMs;
            if (mediaCpuMs > alvo + histereseMs) { pressaoAcumulada += tempoDesdePerfil; recuperacaoAcumulada = 0f; }
            else if (mediaCpuMs < alvo - histereseMs) { recuperacaoAcumulada += tempoDesdePerfil; pressaoAcumulada = 0f; }
            else { pressaoAcumulada = 0f; recuperacaoAcumulada = 0f; }
            if (pressaoAcumulada >= intervaloTrocaPerfil && perfil != PerfilDesempenhoSimulacao.Economico)
            {
                perfil = PerfilDesempenhoSimulacao.Economico;
                pressaoAcumulada = 0f;
            }
            else if (recuperacaoAcumulada >= intervaloTrocaPerfil && perfil == PerfilDesempenhoSimulacao.Economico)
            {
                perfil = PerfilDesempenhoSimulacao.Balanceado;
                recuperacaoAcumulada = 0f;
            }
            tempoDesdePerfil = 0f;
        }

        private RegistroTarefaSimulacao CriarRegistro(Tarefa tarefa)
        {
            return new RegistroTarefaSimulacao(tarefa.Id, tarefa.Dono, tarefa.Camada, tarefa.Frequencia,
                tarefa.PrazoMaximo, tarefa.UltimaExecucao, tarefa.ProximaExecucao, tarefa.CustoRecenteMs,
                tarefa.Aguardando, tarefa.DespertarSolicitado);
        }

        private void PublicarMetricas()
        {
            tarefasAguardando = 0;
            foreach (Tarefa tarefa in tarefas.Values) if (tarefa.Aguardando) tarefasAguardando++;
            DiagnosticoDesempenhoJogo.DefinirContadorMetrica("sim_scheduler_tasks", tarefas.Count);
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("sim_scheduler_frame_ms", tempoTotalMs);
            tempoTotalMs = 0f;
        }
    }
}
