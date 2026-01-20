using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Painel secundário que exibe estatísticas detalhadas:
/// - Ocupação dos Armazéns
/// - Detalhes da População
/// - Contagem Militar por tipo
/// </summary>
public class PainelStatusImperial : MonoBehaviour
{
    [Header("UI Referências")]
    public TextMeshProUGUI textoArmazem;
    public TextMeshProUGUI textoPopulacaoDetalhada;
    public TextMeshProUGUI textoExercito;

    [Header("Configuração")]
    public Color corAlerta = new Color(1f, 0.4f, 0.4f);
    public Color corNormal = new Color(0.9f, 0.9f, 0.9f);

    void Start()
    {
        // Se inscreve nos eventos de atualização
        if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado += AtualizarUI;
            
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados += AtualizarUI;

        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados += AtualizarUI;

        AtualizarUI();
    }
    
    void OnDestroy()
    {
         if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado -= AtualizarUI;
            
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados -= AtualizarUI;

        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados -= AtualizarUI;
    }

    public void AtualizarUI()
    {
        // 1. ATUALIZAR ARMAZÉM
        if (textoArmazem != null && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
        {
            var dados = GerenciadorArmazens.Instancia.armazemRecursos;
            float ocupacao = dados.PercentualOcupacao();
            textoArmazem.text = $"📦 ESTOQUE: {ocupacao:F1}%";
            
            // Tooltip Fake (detalhes se precisar)
            // textoArmazem.text += $" ({dados.EspacoDisponivel()} livres)";
            
            textoArmazem.color = (ocupacao >= 95f) ? corAlerta : corNormal;
        }

        // 2. ATUALIZAR POPULAÇÃO
        if (textoPopulacaoDetalhada != null && GerenciadorRecursos.Instancia != null)
        {
            var g = GerenciadorRecursos.Instancia;
            textoPopulacaoDetalhada.text = $"👥 POPULAÇÃO: {g.populacaoAtual} / {g.populacaoMaxima}";
            
            if(g.populacaoAtual >= g.populacaoMaxima) 
                textoPopulacaoDetalhada.color = corAlerta;
            else 
                textoPopulacaoDetalhada.color = corNormal;
        }

        // 3. ATUALIZAR EXÉRCITO (CENSO)
        if (textoExercito != null && CensoImperial.Instancia != null)
        {
            var c = CensoImperial.Instancia;
            // Formato: ⚔️ 50 (💂30 🚛10 🚁5 🚢5)
            textoExercito.text = $"⚔️ MILITAR: {c.totalUnidades}  <size=80%>(💂{c.infantaria}  🚛{c.veiculos}  🚁{c.aereo}  🚢{c.naval})</size>";
        }
    }
}
