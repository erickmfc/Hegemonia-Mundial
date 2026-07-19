using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Gerenciador central do sistema de Ordens de Extração.
/// 
/// SETUP:
///   1. Adicione este componente a um GameObject vazio na cena (ex: "Gerenciadores").
///   2. Arraste o DadosArmazemRecursos e o GerenciadorArmazens nos campos do Inspector.
///   3. Configure o tempo de 1 dia em segundos (segundosPorDia).
///   4. Crie ordens pelo Inspector ou chame AdicionarOrdem() por código.
/// </summary>
public class GerenciadorExtracoes : MonoBehaviour
{
    // Removido o Singleton para permitir que cada Fábrica tenha o seu próprio gerenciador de extração.
    // public static GerenciadorExtracoes Instancia { get; private set; }

    // ============================================================
    // CONFIGURAÇÕES INSPECTOR
    // ============================================================

    [Header("⏱️ Ciclo de Tempo")]
    [Tooltip("Quantos segundos de tempo real equivalem a 1 dia de jogo.\n" +
             "Ex: 60 = cada minuto real é um dia de extração.")]
    public float segundosPorDia = 60f;

    [Tooltip("Se true, o ciclo de dias avança automaticamente com o tempo.\n" +
             "Se false, chame AvançarDia() manualmente (ideal se o jogo já tem um sistema de turnos).")]
    public bool modoAutomatico = true;

    [Header("📋 Ordens de Extração")]
    [Tooltip("Lista de ordens ativas. Adicione aqui ou use AdicionarOrdem() por código.")]
    public List<OrdemExtracao> ordens = new List<OrdemExtracao>();

    [Header("📊 Estado Global (somente leitura)")]
    [SerializeField] private int _diaAtual = 0;
    [SerializeField] private float _tempoAcumuladoDia = 0f;

    // ============================================================
    // EVENTOS
    // ============================================================

    /// <summary>Disparado ao final de cada dia de jogo (após processar todas as ordens)</summary>
    public event Action<int> OnDiaAvancado;

    /// <summary>Disparado quando qualquer ordem muda de estado</summary>
    public event Action<OrdemExtracao> OnEstadoOrdemAlterado;

    /// <summary>Disparado quando um ciclo completo é concluído com produção</summary>
    public event Action<OrdemExtracao, float> OnCicloConcluido;

    // ============================================================
    // PROPRIEDADES
    // ============================================================

    public int DiaAtual => _diaAtual;
    public float TempoAteProximoDia => Mathf.Max(0f, segundosPorDia - _tempoAcumuladoDia);
    public float ProgressoDia => Mathf.Clamp01(_tempoAcumuladoDia / segundosPorDia);

    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    void Awake()
    {
        // Removida a validação de Singleton
    }

    void Start()
    {
        // Inicializa ordens que ainda não têm ID (configuradas via Inspector)
        foreach (var ordem in ordens)
        {
            if (ordem != null && string.IsNullOrEmpty(ordem.ID))
                ordem.Inicializar();
        }

        Debug.Log($"[GerenciadorExtracoes] Iniciado com {ordens.Count} ordem(s). " +
                  $"Modo: {(modoAutomatico ? $"Automático ({segundosPorDia}s/dia)" : "Manual")}");
    }

    void Update()
    {
        if (!modoAutomatico) return;

        _tempoAcumuladoDia += Time.deltaTime;
        if (_tempoAcumuladoDia >= segundosPorDia)
        {
            _tempoAcumuladoDia -= segundosPorDia;
            AvançarDia();
        }
    }

    // ============================================================
    // AVANÇAR DIA — LÓGICA CENTRAL
    // ============================================================

    /// <summary>
    /// Avança 1 dia de jogo: processa todos os ciclos de extração.
    /// Pode ser chamado externamente pelo sistema de turnos do jogo.
    /// </summary>
    public void AvançarDia()
    {
        _diaAtual++;
        Debug.Log($"[GerenciadorExtracoes] ════ DIA {_diaAtual} ════");

        foreach (var ordem in ordens)
        {
            if (ordem == null || ordem.dados == null) continue;
            ProcessarOrdemNoDia(ordem);
        }

        // Tenta retomar automaticamente ordens suspensas por falta de recursos
        TentarRetormarOrdensSuspensas();

        OnDiaAvancado?.Invoke(_diaAtual);
    }

    /// <summary>
    /// Processa uma única ordem para o dia atual.
    /// </summary>
    void ProcessarOrdemNoDia(OrdemExtracao ordem)
    {
        // Estados que não processam
        switch (ordem.Estado)
        {
            case EstadoOrdem.Bloqueada:
            case EstadoOrdem.Pausada:
                return;
            case EstadoOrdem.SemEnergia:
            case EstadoOrdem.SemVerba:
                // Tentamos retomar (verificado em TentarRetormarOrdensSuspensas)
                return;
        }

        // Ativa ordens que estão aguardando
        if (ordem.Estado == EstadoOrdem.Aguardando)
            ordem.MudarEstado(EstadoOrdem.Ativa);

        // Verifica recursos ANTES de avançar o dia no ciclo
        bool temDinheiro = GerenciadorRecursos.Instancia != null &&
                           GerenciadorRecursos.Instancia.dinheiro >= ordem.dados.custoDinheiro;
        bool temEnergia  = GerenciadorRecursos.Instancia != null &&
                           GerenciadorRecursos.Instancia.energia >= ordem.dados.custoEnergia;

        if (!temEnergia)
        {
            ordem.MudarEstado(EstadoOrdem.SemEnergia);
            OnEstadoOrdemAlterado?.Invoke(ordem);
            Debug.LogWarning($"[Extração] {ordem.dados.nomeRecurso} suspensa: SEM ENERGIA " +
                             $"(precisa {ordem.dados.custoEnergia}, tem {GerenciadorRecursos.Instancia?.energia ?? 0})");
            return;
        }

        if (!temDinheiro)
        {
            ordem.MudarEstado(EstadoOrdem.SemVerba);
            OnEstadoOrdemAlterado?.Invoke(ordem);
            Debug.LogWarning($"[Extração] {ordem.dados.nomeRecurso} suspensa: SEM VERBA " +
                             $"(precisa ${ordem.dados.custoDinheiro}, tem ${GerenciadorRecursos.Instancia?.dinheiro ?? 0})");
            return;
        }

        // Avança o contador de dias do ciclo
        bool cicloCompleto = ordem.AvancarDiaNoCiclo();

        if (!cicloCompleto)
        {
            // Ciclo ainda em andamento — cobra energia do dia mas não produz ainda
            GerenciadorRecursos.Instancia.TentarGastar(custoEnergia: ordem.dados.custoEnergia);
            Debug.Log($"[Extração] {ordem.dados.nomeRecurso}: {ordem.DiasRestantesNoCiclo} dia(s) restante(s) no ciclo.");
            return;
        }

        // ── CICLO COMPLETO: cobra, produz e deposita ──
        ordem.MudarEstado(EstadoOrdem.ConcluindoCiclo);

        // Cobra o custo do ciclo (dinheiro + energia)
        bool cobrou = GerenciadorRecursos.Instancia.TentarGastar(
            custoDinheiro: ordem.dados.custoDinheiro,
            custoEnergia:  ordem.dados.custoEnergia);

        if (!cobrou)
        {
            // Em caso de falha na cobrança (race condition), suspende
            ordem.MudarEstado(EstadoOrdem.SemVerba);
            OnEstadoOrdemAlterado?.Invoke(ordem);
            return;
        }

        // Gera produção aleatória dentro do intervalo configurado
        float producao = ordem.dados.GerarProducaoAleatoria();

        // Deposita no armazém de recursos
        DepositarNoArmazem(ordem.dados.tipoExtracao, Mathf.RoundToInt(producao));

        // Registra no histórico da ordem
        ordem.RegistrarCiclo(_diaAtual, producao, ordem.dados.custoDinheiro, ordem.dados.custoEnergia, "NORMAL");

        Debug.Log($"[Extração] ✅ {ordem.dados.nomeRecurso}: {producao:N0} t produzidas | " +
                  $"Custo: ${ordem.dados.custoDinheiro} + ⚡{ordem.dados.custoEnergia}");

        OnCicloConcluido?.Invoke(ordem, producao);

        // Consulta estoque atual para verificar condição de parada
        float estoqueAtual = ConsultarEstoqueArmazem(ordem.dados.tipoExtracao);

        if (ordem.VerificarCondicaoParada(estoqueAtual))
        {
            // Condição de parada atingida
            ordem.MudarEstado(EstadoOrdem.Pausada);
            OnEstadoOrdemAlterado?.Invoke(ordem);
            Debug.Log($"[Extração] 🏁 {ordem.dados.nomeRecurso}: meta atingida → ordem concluída.");
        }
        else if (ordem.modo == ModoExtracao.Continua)
        {
            // Reinicia ciclo automaticamente
            ordem.ResetarCiclo();
            ordem.MudarEstado(EstadoOrdem.Ativa);
        }
        else
        {
            // Outros modos: reinicia ciclo e continua
            ordem.ResetarCiclo();
            ordem.MudarEstado(EstadoOrdem.Ativa);
        }
    }

    /// <summary>
    /// Verifica ordens suspensas (SemEnergia/SemVerba) e tenta retomá-las se recursos voltaram.
    /// </summary>
    void TentarRetormarOrdensSuspensas()
    {
        foreach (var ordem in ordens)
        {
            if (ordem == null || ordem.dados == null) continue;

            bool suspensa = ordem.Estado == EstadoOrdem.SemEnergia ||
                            ordem.Estado == EstadoOrdem.SemVerba;
            if (!suspensa) continue;

            bool temDinheiro = GerenciadorRecursos.Instancia != null &&
                               GerenciadorRecursos.Instancia.dinheiro >= ordem.dados.custoDinheiro;
            bool temEnergia  = GerenciadorRecursos.Instancia != null &&
                               GerenciadorRecursos.Instancia.energia >= ordem.dados.custoEnergia;

            if (temDinheiro && temEnergia)
            {
                Debug.Log($"[Extração] 🔄 {ordem.dados.nomeRecurso}: recursos restaurados — retomando.");
                ordem.MudarEstado(EstadoOrdem.Ativa);
                OnEstadoOrdemAlterado?.Invoke(ordem);
            }
        }
    }

    // ============================================================
    // DEPÓSITO NO ARMAZÉM
    // ============================================================

    /// <summary>
    /// Deposita o minério produzido no campo correspondente do DadosArmazemRecursos.
    /// </summary>
    void DepositarNoArmazem(TipoRecursoExtracao tipo, int quantidade)
    {
        if (GerenciadorArmazens.Instancia == null || GerenciadorArmazens.Instancia.armazemRecursos == null)
        {
            Debug.LogWarning("[GerenciadorExtracoes] GerenciadorArmazens ou armazemRecursos não encontrado.");
            return;
        }

        var armazem = GerenciadorArmazens.Instancia.armazemRecursos;
        armazem.AdicionarMinerio(tipo, quantidade);

        Debug.Log($"[Extração] 📦 Depositado: {quantidade:N0} t de {tipo} no armazém.");
    }

    /// <summary>
    /// Consulta o estoque atual de um minério no armazém.
    /// </summary>
    float ConsultarEstoqueArmazem(TipoRecursoExtracao tipo)
    {
        if (GerenciadorArmazens.Instancia == null || GerenciadorArmazens.Instancia.armazemRecursos == null)
            return 0f;

        return GerenciadorArmazens.Instancia.armazemRecursos.ConsultarMinerio(tipo);
    }

    // ============================================================
    // API PÚBLICA — GERÊNCIA DE ORDENS
    // ============================================================

    /// <summary>
    /// Adiciona e inicializa uma nova ordem de extração.
    /// </summary>
    public OrdemExtracao AdicionarOrdem(OrdemExtracao novaOrdem)
    {
        if (novaOrdem == null)
        {
            Debug.LogError("[GerenciadorExtracoes] Tentativa de adicionar ordem nula.");
            return null;
        }

        novaOrdem.Inicializar();
        ordens.Add(novaOrdem);
        Debug.Log($"[Extração] ➕ Ordem adicionada: {novaOrdem.dados?.nomeRecurso ?? "?"} [{novaOrdem.ID}] — Modo: {novaOrdem.modo}");
        return novaOrdem;
    }

    /// <summary>
    /// Cria e adiciona uma ordem com configurações básicas.
    /// </summary>
    public OrdemExtracao CriarOrdem(DadosTipoMinerio dados, ModoExtracao modo = ModoExtracao.Continua,
                                    float meta = 0f, int diasMeta = 1, float estoqueAlvo = 0f)
    {
        var ordem = new OrdemExtracao
        {
            dados = dados,
            modo = modo,
            quantidadeMeta = meta,
            diasMeta = diasMeta,
            estoqueAlvo = estoqueAlvo
        };
        return AdicionarOrdem(ordem);
    }

    /// <summary>
    /// Pausa manualmente uma ordem pelo índice na lista.
    /// </summary>
    public void PausarOrdem(int indice)
    {
        if (!IndiceValido(indice)) return;
        ordens[indice].Pausar();
        OnEstadoOrdemAlterado?.Invoke(ordens[indice]);
    }

    /// <summary>
    /// Retoma uma ordem pausada manualmente pelo índice.
    /// </summary>
    public void RetomarOrdem(int indice)
    {
        if (!IndiceValido(indice)) return;
        ordens[indice].Retomar();
        OnEstadoOrdemAlterado?.Invoke(ordens[indice]);
    }

    /// <summary>
    /// Cancela e remove uma ordem da lista.
    /// </summary>
    public void CancelarOrdem(int indice)
    {
        if (!IndiceValido(indice)) return;
        Debug.Log($"[Extração] ❌ Ordem cancelada: {ordens[indice].dados?.nomeRecurso ?? "?"} [{ordens[indice].ID}]");
        ordens.RemoveAt(indice);
    }

    /// <summary>
    /// Concede autorização a uma ordem bloqueada.
    /// </summary>
    public bool AutorizarOrdem(int indice)
    {
        if (!IndiceValido(indice)) return false;
        bool ok = ordens[indice].ConcederAutorizacao();
        if (ok) OnEstadoOrdemAlterado?.Invoke(ordens[indice]);
        return ok;
    }

    // ============================================================
    // CONSULTA / RELATÓRIO
    // ============================================================

    /// <summary>
    /// Retorna um relatório textual de todas as ordens (para debug ou UI).
    /// </summary>
    public string ObterRelatorioOrdens()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== ORDENS DE EXTRAÇÃO — DIA {_diaAtual} ===");
        sb.AppendLine($"{"RECURSO",-15} {"ESTADO",-18} {"PRODUÇÃO EST.",-20} {"PRÓX. ENTREGA",-15}");
        sb.AppendLine(new string('─', 70));

        foreach (var o in ordens)
        {
            if (o == null || o.dados == null) continue;
            sb.AppendLine($"{o.dados.nomeRecurso,-15} {o.Estado,-18} {o.dados.ProducaoEstimadaFormatada(),-20} {o.ProximaEntregaFormatada(),-15}");
        }

        return sb.ToString();
    }

    // ============================================================
    // HELPERS PRIVADOS
    // ============================================================

    bool IndiceValido(int indice)
    {
        if (indice < 0 || indice >= ordens.Count)
        {
            Debug.LogWarning($"[GerenciadorExtracoes] Índice inválido: {indice}");
            return false;
        }
        return true;
    }

    // ============================================================
    // GIZMOS DE DEBUG (exibe progresso do dia no Editor)
    // ============================================================

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.BeginGUI();
        GUI.Label(new Rect(10, Screen.height - 60, 300, 20),
            $"Extração — Dia {_diaAtual} | Próximo dia: {TempoAteProximoDia:F0}s");
        UnityEditor.Handles.EndGUI();
    }
#endif
}
