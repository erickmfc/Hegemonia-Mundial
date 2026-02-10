using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Script único que controla toda a UI de recursos do jogo.
/// Agora em formato de lista vertical unificada.
/// </summary>
public class PainelRecursos : MonoBehaviour
{
    [Header("Recursos Principais")]
    public TextMeshProUGUI textoDinheiro;
    public TextMeshProUGUI textoPetroleo;
    public TextMeshProUGUI textoAco;
    public TextMeshProUGUI textoEnergia;
    public TextMeshProUGUI textoPopulacao;

    [Header("Textos de Ganho (+/s)")]
    public TextMeshProUGUI ganhoTextoDinheiro;
    public TextMeshProUGUI ganhoTextoPetroleo;
    public TextMeshProUGUI ganhoTextoAco;
    public TextMeshProUGUI ganhoTextoEnergia;

    [Header("Status Extra (Opcional)")]
    public TextMeshProUGUI textoEstoque;
    public TextMeshProUGUI textoExercito;

    void Awake()
    {
        // CORREÇÃO CRÍTICA DE FONTE:
        // Varre todos os textos filhos e remove caracteres inválidos (emojis antigos)
        // que causam quadrados brancos na UI.
        LimparTextosComEmojis(transform);
    }

    void LimparTextosComEmojis(Transform pai)
    {
        // Lista expandida de emojis problemáticos reportados no Log
        // \U0001F4B0 = 💰
        // \u26FD = ⛽
        // \U0001F529 = 🔩 (Bolt/Nut) -> Trocado por "Part"
        // \u26A1 = ⚡
        // \U0001F4E6 = 📦
        // \U0001F465 = 👥
        // \u2694 = ⚔
        
        foreach(Transform filho in pai)
        {
            var tmp = filho.GetComponent<TextMeshProUGUI>();
            if(tmp != null && !string.IsNullOrEmpty(tmp.text))
            {
                // Substituição seguras
                string textoLimpo = tmp.text;
                textoLimpo = textoLimpo.Replace("\U0001F4B0", "$");   // Dinheiro
                textoLimpo = textoLimpo.Replace("\u26FD", "Oil");     // Combustivel
                textoLimpo = textoLimpo.Replace("\u26A1", "Pwr");     // Energia
                textoLimpo = textoLimpo.Replace("\U0001F4E6", "Mat"); // Materiais
                textoLimpo = textoLimpo.Replace("\U0001F465", "Pop"); // População
                textoLimpo = textoLimpo.Replace("\u2694", "Milit");   // Militar / Ammo
                textoLimpo = textoLimpo.Replace("\U0001F527", "Eng"); // Wrench (Engenharia)
                textoLimpo = textoLimpo.Replace("\U0001F529", "Part");// Nut and Bolt
                
                // Remove qualquer outro caractere "surrogate" que possa ter sobrado
                // (Opcional, mas ajuda a limpar lixo visual)
                
                tmp.text = textoLimpo;
            }
            
            // Recursivo para filhos
            if(filho.childCount > 0) LimparTextosComEmojis(filho);
        }
    }

    void Start()
    {
        AtualizarTudo();
        
        // Inscrevendo nos eventos
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados += AtualizarTudo;
            
        if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado += AtualizarTudo;
            
        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados += AtualizarTudo;
    }

    void OnDestroy()
    {
        // Cancelando inscrições para evitar erros
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados -= AtualizarTudo;
            
        if (CensoImperial.Instancia != null)
            CensoImperial.Instancia.OnCensoAtualizado -= AtualizarTudo;
            
        if (GerenciadorArmazens.Instancia != null)
            GerenciadorArmazens.Instancia.OnArmazensAtualizados -= AtualizarTudo;
    }

    /// <summary>
    /// Chamado sempre que algum valor muda
    /// </summary>
    void AtualizarTudo()
    {
        if (GerenciadorRecursos.Instancia == null) return;
        var r = GerenciadorRecursos.Instancia;

        // Atualiza Recursos Básicos
        AtualizarTexto(textoDinheiro, r.dinheiro, ganhoTextoDinheiro, r.dinheiroPorSegundo, "$");
        AtualizarTexto(textoPetroleo, r.petroleo, ganhoTextoPetroleo, r.petroleoPorSegundo, "");
        AtualizarTexto(textoAco, r.aco, ganhoTextoAco, r.acoPorSegundo, "");
        AtualizarTexto(textoEnergia, r.energia, ganhoTextoEnergia, 0, ""); 
        
        // População
        if(textoPopulacao != null)
            textoPopulacao.text = $"{r.populacaoAtual}/{r.populacaoMaxima}";

        // --- DADOS ADICIONAIS ---
        
        // Estoque / Armazém
        if (textoEstoque != null && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
        {
            float ocupacao = GerenciadorArmazens.Instancia.armazemRecursos.PercentualOcupacao();
            textoEstoque.text = $"{ocupacao:F0}%";
            
            // Vermelho se cheio (>90%)
            textoEstoque.color = (ocupacao >= 90) ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        // Exército (Total de unidades)
        if (textoExercito != null && CensoImperial.Instancia != null)
        {
            textoExercito.text = $"{CensoImperial.Instancia.totalUnidades}";
        }
    }

    /// <summary>
    /// Helper para formatar texto de valor e ganho de forma padronizada
    /// </summary>
    void AtualizarTexto(TextMeshProUGUI txtValor, float valor, TextMeshProUGUI txtGanho, float ganho, string prefixo)
    {
        // 1. Valor Principal
        if (txtValor != null) 
            txtValor.text = $"{prefixo}{valor:N0}";
        
        // 2. Texto de Ganho (+10/s)
        if (txtGanho != null)
        {
            if (ganho > 0)
            {
                txtGanho.text = $"(+{ganho:N0}/s)";
                txtGanho.color = new Color(0.4f, 1f, 0.4f); // Verde
            }
            else if (ganho < 0)
            {
                txtGanho.text = $"({ganho:N0}/s)";
                txtGanho.color = new Color(1f, 0.4f, 0.4f); // Vermelho
            }
            else
            {
                txtGanho.text = ""; // Esconde se for zero
            }
        }
    }
}
