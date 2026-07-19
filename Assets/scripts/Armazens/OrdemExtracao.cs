using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ENUMS DO SISTEMA DE EXTRAÇÃO
// ============================================================

/// <summary>
/// Tipos de minérios extraíveis. Mapeiam para campos específicos no DadosArmazemRecursos.
/// </summary>
public enum TipoRecursoExtracao
{
    Ferro,
    Cobre,
    Bauxita,
    Titanio,
    Uranio
}

/// <summary>
/// Estado atual de uma ordem de extração.
/// </summary>
public enum EstadoOrdem
{
    /// <summary>Bloqueada por autorização pendente ou pré-requisito não atendido</summary>
    Bloqueada,
    /// <summary>Na fila, aguardando início do ciclo</summary>
    Aguardando,
    /// <summary>Extração em andamento normalmente</summary>
    Ativa,
    /// <summary>Pausada manualmente pelo jogador</summary>
    Pausada,
    /// <summary>Suspensa automaticamente por falta de energia</summary>
    SemEnergia,
    /// <summary>Suspensa automaticamente por falta de dinheiro</summary>
    SemVerba,
    /// <summary>Ciclo em conclusão, entrega pendente</summary>
    ConcluindoCiclo
}

/// <summary>
/// Configura como a ordem decide quando parar.
/// </summary>
public enum ModoExtracao
{
    /// <summary>Ciclos infinitos — reinicia automaticamente ao concluir</summary>
    Continua,
    /// <summary>Para quando acumular X toneladas no total desta ordem</summary>
    PorQuantidade,
    /// <summary>Para após N ciclos/dias concluídos</summary>
    PorDias,
    /// <summary>Para quando o estoque do minério no armazém atingir X toneladas</summary>
    PorEstoqueAlvo
}

// ============================================================
// REGISTRO DE HISTÓRICO
// ============================================================

/// <summary>
/// Registro imutável de um ciclo de extração concluído.
/// </summary>
[System.Serializable]
public struct RegistroExtracao
{
    [Tooltip("Dia de jogo em que o ciclo foi concluído")]
    public int dia;

    [Tooltip("Nome do recurso extraído")]
    public string recurso;

    [Tooltip("Quantidade produzida em toneladas")]
    public float quantidadeProduzida;

    [Tooltip("Custo de dinheiro cobrado neste ciclo")]
    public int custoDinheiro;

    [Tooltip("Custo de energia cobrado neste ciclo")]
    public int custoEnergia;

    [Tooltip("Observação do ciclo (NORMAL, SEM ENERGIA, SEM VERBA, etc.)")]
    public string observacao;

    public RegistroExtracao(int dia, string recurso, float qtd, int dinheiro, int energia, string obs)
    {
        this.dia = dia;
        this.recurso = recurso;
        this.quantidadeProduzida = qtd;
        this.custoDinheiro = dinheiro;
        this.custoEnergia = energia;
        this.observacao = obs;
    }
}

// ============================================================
// ORDEM DE EXTRAÇÃO
// ============================================================

/// <summary>
/// Representa uma ordem de extração mineral na fila da fábrica.
/// </summary>
[System.Serializable]
public class OrdemExtracao
{
    [Header("🪨 Configuração")]
    [Tooltip("Dados do tipo de minério extraído (arraste o ScriptableObject)")]
    public DadosTipoMinerio dados;

    [Header("⚙️ Modo de Operação")]
    public ModoExtracao modo = ModoExtracao.Continua;

    [Header("🎯 Metas (usadas conforme o Modo)")]
    [Tooltip("PorQuantidade: total de toneladas a extrair nesta ordem")]
    public float quantidadeMeta = 10000f;

    [Tooltip("PorDias: número de ciclos a completar")]
    public int diasMeta = 3;

    [Tooltip("PorEstoqueAlvo: parar quando armazém atingir esse total do minério (ex: 50.000 t)")]
    public float estoqueAlvo = 50000f;

    // ---- Estado Interno ----

    [Header("📊 Estado Atual (somente leitura)")]
    [SerializeField] private EstadoOrdem _estado = EstadoOrdem.Aguardando;
    [SerializeField] private float _totalProduzidoNestaOrdem = 0f;
    [SerializeField] private int _ciclosConcluidos = 0;
    [SerializeField] private int _diasRestantesNoCicloAtual = 0;
    [SerializeField] private List<RegistroExtracao> _historico = new List<RegistroExtracao>();

    // ---- Propriedades Públicas ----

    public EstadoOrdem Estado => _estado;
    public float TotalProduzido => _totalProduzidoNestaOrdem;
    public int CiclosConcluidos => _ciclosConcluidos;
    public int DiasRestantesNoCiclo => _diasRestantesNoCicloAtual;
    public IReadOnlyList<RegistroExtracao> Historico => _historico;

    /// <summary>ID único gerado no momento da criação da ordem</summary>
    public string ID { get; private set; }

    /// <summary>
    /// Inicializa a ordem com um ID único e configura o estado inicial.
    /// </summary>
    public void Inicializar()
    {
        ID = System.Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

        if (dados == null)
        {
            Debug.LogError("[OrdemExtracao] Tentativa de inicializar ordem sem DadosTipoMinerio!");
            _estado = EstadoOrdem.Bloqueada;
            return;
        }

        _estado = dados.exigeAutorizacao ? EstadoOrdem.Bloqueada : EstadoOrdem.Aguardando;
        _diasRestantesNoCicloAtual = dados.duracaoEmDias;
        _totalProduzidoNestaOrdem = 0f;
        _ciclosConcluidos = 0;
        _historico = new List<RegistroExtracao>();
    }

    /// <summary>
    /// Muda o estado da ordem com log de depuração.
    /// </summary>
    public void MudarEstado(EstadoOrdem novoEstado)
    {
        if (_estado == novoEstado) return;
        Debug.Log($"[Extração {ID}] {dados?.nomeRecurso ?? "?"}: {_estado} → {novoEstado}");
        _estado = novoEstado;
    }

    /// <summary>
    /// Registra um ciclo concluído no histórico e atualiza acumuladores.
    /// </summary>
    public void RegistrarCiclo(int diaJogo, float producao, int dinheiro, int energia, string obs)
    {
        _totalProduzidoNestaOrdem += producao;
        _ciclosConcluidos++;
        _historico.Add(new RegistroExtracao(diaJogo, dados?.nomeRecurso ?? "?", producao, dinheiro, energia, obs));
    }

    /// <summary>
    /// Avança 1 dia no contador do ciclo atual. Retorna true quando o ciclo termina.
    /// </summary>
    public bool AvancarDiaNoCiclo()
    {
        if (_diasRestantesNoCicloAtual > 0)
            _diasRestantesNoCicloAtual--;

        return _diasRestantesNoCicloAtual <= 0;
    }

    /// <summary>
    /// Reinicia o contador de dias do ciclo atual (para ordens contínuas).
    /// </summary>
    public void ResetarCiclo()
    {
        _diasRestantesNoCicloAtual = dados != null ? dados.duracaoEmDias : 1;
    }

    /// <summary>
    /// Pausa manualmente a ordem (apenas se estiver Ativa ou Aguardando).
    /// </summary>
    public void Pausar()
    {
        if (_estado == EstadoOrdem.Ativa || _estado == EstadoOrdem.Aguardando)
            MudarEstado(EstadoOrdem.Pausada);
    }

    /// <summary>
    /// Retoma a ordem (apenas se estiver Pausada).
    /// </summary>
    public void Retomar()
    {
        if (_estado == EstadoOrdem.Pausada)
            MudarEstado(EstadoOrdem.Aguardando);
    }

    /// <summary>
    /// Concede autorização a uma ordem Bloqueada, movendo-a para Aguardando.
    /// </summary>
    public bool ConcederAutorizacao()
    {
        if (_estado != EstadoOrdem.Bloqueada) return false;
        MudarEstado(EstadoOrdem.Aguardando);
        return true;
    }

    /// <summary>
    /// Retorna o texto de próxima entrega formatado para UI.
    /// </summary>
    public string ProximaEntregaFormatada()
    {
        switch (_estado)
        {
            case EstadoOrdem.Bloqueada:
                return dados != null && !string.IsNullOrEmpty(dados.descricaoRestricao)
                    ? dados.descricaoRestricao
                    : "Exige autorização";
            case EstadoOrdem.Pausada:
            case EstadoOrdem.SemEnergia:
            case EstadoOrdem.SemVerba:
                return "—";
            default:
                return _diasRestantesNoCicloAtual <= 1 ? "1 dia" : $"{_diasRestantesNoCicloAtual} dias";
        }
    }

    /// <summary>
    /// Verifica se a ordem atingiu sua condição de parada com base no modo.
    /// </summary>
    public bool VerificarCondicaoParada(float estoqueAtualNoArmazem)
    {
        switch (modo)
        {
            case ModoExtracao.PorQuantidade:
                return _totalProduzidoNestaOrdem >= quantidadeMeta;

            case ModoExtracao.PorDias:
                return _ciclosConcluidos >= diasMeta;

            case ModoExtracao.PorEstoqueAlvo:
                return estoqueAtualNoArmazem >= estoqueAlvo;

            default: // Continua
                return false;
        }
    }
}
