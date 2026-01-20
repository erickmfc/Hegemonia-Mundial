using UnityEngine;

/// <summary>
/// Script para o galpão físico de recursos.
/// Adicione ao prefab do Armazém de Recursos.
/// </summary>
public class GalpaoRecursos : MonoBehaviour
{
    [Header("🔗 Conexão com Dados")]
    [Tooltip("Arraste aqui o ScriptableObject DadosArmazemRecursos")]
    public DadosArmazemRecursos dadosArmazem;

    [Header("📊 Informações do Galpão")]
    public string nomeGalpao = "Armazém Central";
    public bool ativo = true;

    [Header("🎨 Visual (Opcional)")]
    public GameObject efeitoArmazenamento; // Partículas quando recebe recursos
    public TextMesh textoCapacidade; // Texto 3D mostrando ocupação

    private float tempoAtualizacao = 0f;

    void Start()
    {
        if (dadosArmazem == null)
        {
            Debug.LogError($"❌ [{name}] DadosArmazemRecursos não foi atribuído!");
        }
        else
        {
            Debug.Log($"✅ [{name}] Galpão de Recursos ativado: {nomeGalpao}");
        }

        // Se inscreve nos eventos do gerenciador
        if (GerenciadorArmazens.Instancia != null)
        {
            GerenciadorArmazens.Instancia.OnArmazensAtualizados += AtualizarVisual;
        }
    }

    void Update()
    {
        // Atualiza texto de capacidade periodicamente
        tempoAtualizacao += Time.deltaTime;
        if (tempoAtualizacao >= 1f && textoCapacidade != null && dadosArmazem != null)
        {
            textoCapacidade.text = $"{dadosArmazem.PercentualOcupacao():F0}%";
            tempoAtualizacao = 0f;
        }
    }

    void AtualizarVisual()
    {
        // Ativa efeito visual quando recebe recursos
        if (efeitoArmazenamento != null && dadosArmazem != null)
        {
            efeitoArmazenamento.SetActive(true);
            Invoke(nameof(DesativarEfeito), 1f);
        }
    }

    void DesativarEfeito()
    {
        if (efeitoArmazenamento != null)
        {
            efeitoArmazenamento.SetActive(false);
        }
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
        if (dadosArmazem == null) return;

        // Desenha esfera colorida baseado na ocupação
        float ocupacao = dadosArmazem.PercentualOcupacao() / 100f;
        Gizmos.color = Color.Lerp(Color.green, Color.red, ocupacao);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 1f);
    }
}
