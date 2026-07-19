using UnityEngine;
using System;

/// <summary>
/// Gerenciador central de armazéns que conecta os ScriptableObjects ao HUD
/// e prepara para futura integração com mercado internacional
/// </summary>
public class GerenciadorArmazens : MonoBehaviour
{
    public static GerenciadorArmazens Instancia { get; private set; }

    [Header("📦 Referências aos Armazéns")]
    [Tooltip("Arraste aqui o ScriptableObject do Armazém de Recursos")]
    public DadosArmazemRecursos armazemRecursos;
    
    [Tooltip("Arraste aqui o ScriptableObject do Armazém Militar")]
    public DadosArmazemMilitar armazemMilitar;

    [Header("🔗 Conexão com Produção")]
    [Tooltip("A cada X segundos, transfere produção para armazéns")]
    public float intervaloTransferencia = 5f;
    
    private float tempoAcumulado = 0f;

    // Eventos para notificar quando armazéns mudarem
    public event Action OnArmazensAtualizados;
    public event Action<string> OnArmazemCheio; // Notifica quando um armazém está cheio

    void Awake()
    {
        // Singleton
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Validação
        if (armazemRecursos == null)
        {
            Debug.LogError("❌ GerenciadorArmazens: DadosArmazemRecursos não foi atribuído!");
        }
        
        if (armazemMilitar == null)
        {
            Debug.LogError("❌ GerenciadorArmazens: DadosArmazemMilitar não foi atribuído!");
        }
    }

    void Update()
    {
        // Transfere produção do GerenciadorRecursos para os armazéns periodicamente
        tempoAcumulado += Time.deltaTime;
        
        if (tempoAcumulado >= intervaloTransferencia)
        {
            TransferirProducaoParaArmazens();
            tempoAcumulado = 0f;
        }
    }

    /// <summary>
    /// Transfere a produção por segundo do GerenciadorRecursos para os armazéns
    /// </summary>
    void TransferirProducaoParaArmazens()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null || armazemRecursos == null) return;

        // Calcula quanto foi produzido no intervalo
        float multiplicador =intervaloTransferencia;
        
        // Transfere petróleo
        if (recursos.petroleoPorSegundo > 0)
        {
            int quantidade = Mathf.RoundToInt(recursos.petroleoPorSegundo * multiplicador);
            if (!armazemRecursos.AdicionarRecurso(TipoRecurso.Petroleo, quantidade))
            {
                OnArmazemCheio?.Invoke("Petróleo");
            }
        }
        
        // Transfere aço/metal
        if (recursos.acoPorSegundo > 0)
        {
            int quantidade = Mathf.RoundToInt(recursos.acoPorSegundo * multiplicador);
            if (!armazemRecursos.AdicionarRecurso(TipoRecurso.Metal, quantidade))
            {
                OnArmazemCheio?.Invoke("Metal");
            }
        }
        
        // Transfere energia (baterias)
        if (recursos.energiaPorSegundo > 0)
        {
            int quantidade = Mathf.RoundToInt(recursos.energiaPorSegundo * multiplicador);
            if (!armazemRecursos.AdicionarRecurso(TipoRecurso.Energia, quantidade))
            {
                OnArmazemCheio?.Invoke("Energia");
            }
        }

        OnArmazensAtualizados?.Invoke();
    }

    // ==================== MÉTODOS PÚBLICOS PARA RECURSOS ====================

    /// <summary>
    /// Adiciona recursos ao armazém (ex: compra do mercado internacional)
    /// </summary>
    public bool AdicionarRecursoCivil(TipoRecurso tipo, int quantidade)
    {
        if (armazemRecursos == null) return false;
        
        bool sucesso = armazemRecursos.AdicionarRecurso(tipo, quantidade);
        if (sucesso)
        {
            OnArmazensAtualizados?.Invoke();
            Debug.Log($"✅ Adicionado {quantidade} de {tipo} ao armazém");
        }
        return sucesso;
    }

    /// <summary>
    /// Remove recursos do armazém (ex: venda no mercado internacional)
    /// </summary>
    public bool RemoverRecursoCivil(TipoRecurso tipo, int quantidade)
    {
        if (armazemRecursos == null) return false;
        
        bool sucesso = armazemRecursos.RemoverRecurso(tipo, quantidade);
        if (sucesso)
        {
            OnArmazensAtualizados?.Invoke();
            Debug.Log($"✅ Removido {quantidade} de {tipo} do armazém");
        }
        return sucesso;
    }

    /// <summary>
    /// Consulta quantidade disponível de um recurso civil
    /// </summary>
    public int ConsultarRecursoCivil(TipoRecurso tipo)
    {
        if (armazemRecursos == null) return 0;
        return armazemRecursos.ConsultarRecurso(tipo);
    }

    public void NotificarAtualizacaoManual()
    {
        OnArmazensAtualizados?.Invoke();
    }

    // ==================== MÉTODOS PÚBLICOS PARA RECURSOS MILITARES ====================

    /// <summary>
    /// Adiciona recursos militares ao armazém
    /// </summary>
    public bool AdicionarRecursoMilitar(TipoRecursoMilitar tipo, int quantidade)
    {
        if (armazemMilitar == null) return false;
        
        bool sucesso = armazemMilitar.AdicionarRecursoMilitar(tipo, quantidade);
        if (sucesso)
        {
            OnArmazensAtualizados?.Invoke();
            Debug.Log($"✅ Adicionado {quantidade} de {tipo} ao armazém militar");
        }
        return sucesso;
    }

    /// <summary>
    /// Remove recursos militares do armazém (ex: equipar tropas)
    /// </summary>
    public bool RemoverRecursoMilitar(TipoRecursoMilitar tipo, int quantidade)
    {
        if (armazemMilitar == null) return false;
        
        bool sucesso = armazemMilitar.RemoverRecursoMilitar(tipo, quantidade);
        if (sucesso)
        {
            OnArmazensAtualizados?.Invoke();
            Debug.Log($"✅ Removido {quantidade} de {tipo} do armazém militar");
        }
        return sucesso;
    }

    /// <summary>
    /// Consulta quantidade disponível de um recurso militar
    /// </summary>
    public int ConsultarRecursoMilitar(TipoRecursoMilitar tipo)
    {
        if (armazemMilitar == null) return 0;
        return armazemMilitar.ConsultarRecursoMilitar(tipo);
    }

    // ==================== MÉTODOS PARA MERCADO INTERNACIONAL (FUTURO) ====================

    /// <summary>
    /// Prepara dados para exportação (mercado internacional)
    /// Retorna array com [tipo, quantidade, preco]
    /// </summary>
    public RecursoParaVenda[] ObterRecursosDisponiveisParaVenda()
    {
        // Implementar lógica de quais recursos podem ser vendidos
        // Por enquanto, retorna vazio - implementar quando criar o mercado
        return new RecursoParaVenda[0];
    }

    /// <summary>
    /// Executa uma transação de compra/venda com outro país
    /// </summary>
    public bool ExecutarTransacaoInternacional(TipoRecurso recurso, int quantidade, int preco, bool ehCompra)
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return false;

        if (ehCompra)
        {
            // Comprando recurso
            if (recursos.TentarGastar(custoDinheiro: preco))
            {
                AdicionarRecursoCivil(recurso, quantidade);
                Debug.Log($"🌍 Compra internacional: {quantidade} {recurso} por ${preco}");
                return true;
            }
        }
        else
        {
            // Vendendo recurso
            if (RemoverRecursoCivil(recurso, quantidade))
            {
                recursos.AdicionarRecursos(addDinheiro: preco);
                Debug.Log($"🌍 Venda internacional: {quantidade} {recurso} por ${preco}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Retorna relatório completo dos armazéns (para debug ou menu)
    /// </summary>
    public string ObterRelatorioCompleto()
    {
        string relatorio = "=== RELATÓRIO DE ARMAZÉNS ===\n\n";
        
        if (armazemRecursos != null)
        {
            relatorio += "📦 ARMAZÉM DE RECURSOS:\n";
            relatorio += $"Ocupação: {armazemRecursos.PercentualOcupacao():F1}%\n";
            relatorio += $"🌾 Alimentos: {armazemRecursos.alimentos}/{armazemRecursos.alimentosMaximo}\n";
            relatorio += $"💧 Água: {armazemRecursos.agua}/{armazemRecursos.aguaMaximo}\n";
            relatorio += $"⛽ Petróleo: {armazemRecursos.petroleo}/{armazemRecursos.petroleoMaximo}\n";
            relatorio += $"💎 Minerais: {armazemRecursos.minerais}/{armazemRecursos.mineraisMaximo}\n";
            relatorio += $"🔩 Metal: {armazemRecursos.metal}/{armazemRecursos.metalMaximo}\n";
            relatorio += $"⚡ Energia: {armazemRecursos.energia}/{armazemRecursos.energiaMaximo}\n\n";
        }
        
        if (armazemMilitar != null)
        {
            relatorio += "🎖️ ARMAZÉM MILITAR:\n";
            relatorio += $"Ocupação: {armazemMilitar.PercentualOcupacao():F1}%\n";
            relatorio += $"🔫 Munição Leve: {armazemMilitar.municaoLeve}/{armazemMilitar.municaoLeveMaximo}\n";
            relatorio += $"💣 Munição Pesada: {armazemMilitar.municaoPesada}/{armazemMilitar.municaoPesadaMaximo}\n";
            relatorio += $"🚀 Mísseis: {armazemMilitar.misseis}/{armazemMilitar.misseisMaximo}\n";
            relatorio += $"💥 Explosivos: {armazemMilitar.explosivos}/{armazemMilitar.explosivosMaximo}\n";
            relatorio += $"🎖️ Equipamento: {armazemMilitar.equipamento}/{armazemMilitar.equipamentoMaximo}\n";
            relatorio += $"🛡️ Blindagem: {armazemMilitar.blindagem}/{armazemMilitar.blindagemMaximo}\n";
        }
        
        return relatorio;
    }
}

/// <summary>
/// Estrutura para venda de recursos (mercado internacional futuro)
/// </summary>
[System.Serializable]
public struct RecursoParaVenda
{
    public TipoRecurso tipo;
    public int quantidadeDisponivel;
    public int precoUnitario;
}
