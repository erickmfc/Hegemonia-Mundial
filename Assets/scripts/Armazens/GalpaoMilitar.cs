using UnityEngine;

/// <summary>
/// Script para o galpão físico militar.
/// Adicione ao prefab do Armazém Militar.
/// </summary>
public class GalpaoMilitar : MonoBehaviour
{
    [Header("🔗 Conexão com Dados")]
    [Tooltip("Arraste aqui o ScriptableObject DadosArmazemMilitar")]
    public DadosArmazemMilitar dadosArmazemMilitar;

    [Header("📊 Informações do Galpão")]
    public string nomeGalpao = "Arsenal Militar";
    public bool ativo = true;
    public int nivelSeguranca = 5;

    [Header("🎨 Visual (Opcional)")]
    public GameObject efeitoSeguranca; // Luzes de segurança
    public TextMesh textoCapacidade; // Texto 3D mostrando ocupação
    public Light luzDeSeguranca; // Luz que muda de cor baseado na capacidade

    private float tempoAtualizacao = 0f;

    void Start()
    {
        if (dadosArmazemMilitar == null)
        {
            Debug.LogError($"❌ [{name}] DadosArmazemMilitar não foi atribuído!");
        }
        else
        {
            Debug.Log($"✅ [{name}] Galpão Militar ativado: {nomeGalpao} (Segurança: Nível {nivelSeguranca})");
        }

        // Se inscreve nos eventos do gerenciador
        if (GerenciadorArmazens.Instancia != null)
        {
            GerenciadorArmazens.Instancia.OnArmazensAtualizados += AtualizarVisual;
        }

        // Ativa sistema de segurança
        if (efeitoSeguranca != null)
        {
            efeitoSeguranca.SetActive(true);
        }
    }

    void Update()
    {
        // Atualiza visual periodicamente
        tempoAtualizacao += Time.deltaTime;
        if (tempoAtualizacao >= 1f && dadosArmazemMilitar != null)
        {
            // Atualiza texto
            if (textoCapacidade != null)
            {
                textoCapacidade.text = $"{dadosArmazemMilitar.PercentualOcupacao():F0}%";
            }

            // Atualiza cor da luz de segurança
            if (luzDeSeguranca != null)
            {
                float ocupacao = dadosArmazemMilitar.PercentualOcupacao() / 100f;
                luzDeSeguranca.color = Color.Lerp(Color.green, Color.yellow, ocupacao);
            }

            tempoAtualizacao = 0f;
        }
    }

    void AtualizarVisual()
    {
        // Atualização quando recebe/remove recursos
        if (luzDeSeguranca != null && dadosArmazemMilitar != null)
        {
            float ocupacao = dadosArmazemMilitar.PercentualOcupacao() / 100f;
            luzDeSeguranca.color = Color.Lerp(Color.green, Color.red, ocupacao);
        }
    }

    /// <summary>
    /// Equipa uma unidade com munição do armazém
    /// </summary>
    public bool EquiparUnidade(GameObject unidade)
    {
        if (dadosArmazemMilitar == null) return false;

        // Verifica se tem munição
        if (dadosArmazemMilitar.TemMunicaoParaUnidade(1))
        {
            dadosArmazemMilitar.RemoverRecursoMilitar(TipoRecursoMilitar.MunicaoLeve, 30);
            Debug.Log($"✅ Unidade {unidade.name} equipada com munição");
            return true;
        }

        Debug.LogWarning($"⚠️ Sem munição suficiente para equipar {unidade.name}");
        return false;
    }

    void OnDestroy()
    {
        // Remove inscrição do evento
        if (GerenciadorArmazens.Instancia != null)
        {
            GerenciadorArmazens.Instancia.OnArmazensAtualizados -= AtualizarVisual;
        }
    }

    // Desenha informações no Editor
    void OnDrawGizmos()
    {
        if (dadosArmazemMilitar == null) return;

        // Desenha esfera vermelha (área militar)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 2f);
        
        // Desenha área de segurança
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, nivelSeguranca);
    }
}
