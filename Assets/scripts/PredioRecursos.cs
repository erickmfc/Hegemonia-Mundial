using UnityEngine;

/// <summary>
/// Componente que pode ser adicionado a qualquer prédio para modificar
/// os ganhos de recursos por segundo automaticamente.
/// Ao ser construído, aumenta os ganhos. Ao ser destruído, remove os ganhos.
/// </summary>
public class PredioRecursos : MonoBehaviour
{
    [Header("💰 Produção de Recursos por Segundo")]
    [Tooltip("Quanto de dinheiro este prédio gera por segundo")]
    public float producaoDinheiro = 0f;
    
    [Tooltip("Quanto de petróleo este prédio gera por segundo")]
    public float producaoPetroleo = 0f;
    
    [Tooltip("Quanto de aço este prédio gera por segundo")]
    public float producaoAco = 0f;
    
    [Tooltip("Quanto de energia este prédio gera por segundo")]
    public float producaoEnergia = 0f;

    [Header("⚙️ Configurações")]
    [Tooltip("Ativar produção automaticamente ao criar o prédio?")]
    public bool ativarAoCriar = true;
    
    [Tooltip("Delay em segundos antes de começar a produzir (tempo de construção)")]
    public float delayInicial = 0f;

    [Header("📊 Status")]
    [Tooltip("Prédio está produzindo atualmente?")]
    public bool estaProduzindo = false;

    [Header("🎨 Visual (Opcional)")]
    [Tooltip("Partículas ou efeito visual quando está produzindo")]
    public GameObject efeitoProducao;

    private bool jaRegistrado = false;
    private float tempoDecorrido = 0f;

    protected virtual void Start()
    {
        if (ativarAoCriar)
        {
            if (delayInicial > 0)
            {
                Invoke(nameof(AtivarProducao), delayInicial);
            }
            else
            {
                AtivarProducao();
            }
        }
    }

    protected virtual void Update()
    {
        // Atualiza efeito visual
        if (efeitoProducao != null)
        {
            efeitoProducao.SetActive(estaProduzindo);
        }

        // Debug visual
        if (estaProduzindo)
        {
            tempoDecorrido += Time.deltaTime;
        }
    }

    /// <summary>
    /// Ativa a produção de recursos deste prédio
    /// </summary>
    public void AtivarProducao()
    {
        if (jaRegistrado)
        {
            Debug.LogWarning($"[{gameObject.name}] Produção já estava ativa!");
            return;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            recursos.ModificarGanhos(
                multDinheiro: producaoDinheiro,
                multPetroleo: producaoPetroleo,
                multAco: producaoAco,
                multEnergia: producaoEnergia
            );

            jaRegistrado = true;
            estaProduzindo = true;

            Debug.Log($"[OK] [{gameObject.name}] Producao ativada! " +
                      $"$+{producaoDinheiro}/s | P+{producaoPetroleo}/s | A+{producaoAco}/s | E+{producaoEnergia}/s");
        }
        else
        {
            Debug.LogError($"❌ [{gameObject.name}] GerenciadorRecursos não encontrado! Não é possível ativar produção.");
        }
    }

    /// <summary>
    /// Desativa temporariamente a produção (ex: prédio danificado)
    /// </summary>
    public void DesativarProducao()
    {
        if (!jaRegistrado)
        {
            Debug.LogWarning($"[{gameObject.name}] Produção já estava inativa!");
            return;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            // Remove os ganhos (usa valores negativos)
            recursos.ModificarGanhos(
                multDinheiro: -producaoDinheiro,
                multPetroleo: -producaoPetroleo,
                multAco: -producaoAco,
                multEnergia: -producaoEnergia
            );

            jaRegistrado = false;
            estaProduzindo = false;

            Debug.Log($"[PAUSA] [{gameObject.name}] Producao desativada!");
        }
    }

    /// <summary>
    /// Aumenta a produção (upgrade)
    /// </summary>
    public void AumentarProducao(float multiplicador)
    {
        if (!jaRegistrado) return;

        // Remove produção atual
        DesativarProducao();

        // Aumenta valores
        producaoDinheiro *= multiplicador;
        producaoPetroleo *= multiplicador;
        producaoAco *= multiplicador;
        producaoEnergia *= multiplicador;

        // Reativa com novos valores
        AtivarProducao();

        Debug.Log($"[UP] [{gameObject.name}] Producao aumentada {multiplicador}x!");
    }

    protected virtual void OnDestroy()
    {
        // Quando o prédio é destruído, remove a produção
        if (jaRegistrado)
        {
            DesativarProducao();
            Debug.Log($"[DES] [{gameObject.name}] Predio destruido. Producao removida.");
        }
    }

    // Desenha informações de produção no Editor
    protected virtual void OnDrawGizmosSelected()
    {
        if (!estaProduzindo) return;

        // Desenha um ícone acima do prédio
        Gizmos.color = Color.green;
        Vector3 pos = transform.position + Vector3.up * 5f;
        Gizmos.DrawWireSphere(pos, 0.5f);

        // Linha conectando ao prédio
        Gizmos.DrawLine(transform.position, pos);
    }

#if UNITY_EDITOR
    // Mostra info de produção no Inspector
    void OnValidate()
    {
        // Calcula produção total
        float total = producaoDinheiro + producaoPetroleo + producaoAco + producaoEnergia;
        
        if (total > 0)
        {
            gameObject.name = gameObject.name.Replace(" (Producing)", "");
            gameObject.name += " (Producing)";
        }
    }
#endif
}
