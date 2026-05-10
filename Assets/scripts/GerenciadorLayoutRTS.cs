using UnityEngine;
using UnityEngine.UI;

public class GerenciadorLayoutRTS : MonoBehaviour
{
    [Header("Paineis Principais")]
    public RectTransform hudEsquerdo;
    public RectTransform menuConstrucaoCentral;
    public RectTransform painelDireitoDetalhes; // Opcional, pode ser filho do menu central

    [ContextMenu("Aplicar Layout AAA (Warno/SupCom)")]
    public void AplicarLayoutTatico()
    {
        // 1. HUD ESQUERDO (Painel de Recursos/Comandos)
        if (hudEsquerdo != null)
        {
            // Ancorado no Top-Left
            hudEsquerdo.anchorMin = new Vector2(0, 1);
            hudEsquerdo.anchorMax = new Vector2(0, 1);
            hudEsquerdo.pivot = new Vector2(0, 1);
            
            hudEsquerdo.anchoredPosition = new Vector2(8, -8);
            hudEsquerdo.sizeDelta = new Vector2(160, 760); // Largura 160, Altura Max 760
            
            Debug.Log("[Warno Layout] HUD Esquerdo posicionado em (8, -8) com 160px de largura.");
        }

        // 2. MENU CENTRAL DE CONSTRUÇÃO
        if (menuConstrucaoCentral != null)
        {
            // Centralizado na tela
            menuConstrucaoCentral.anchorMin = new Vector2(0.5f, 0.5f);
            menuConstrucaoCentral.anchorMax = new Vector2(0.5f, 0.5f);
            menuConstrucaoCentral.pivot = new Vector2(0.5f, 0.5f);
            
            // Posicionado com offset tático: 5% p/ direita (+96) e 3% p/ cima (+32) em relação à base
            menuConstrucaoCentral.anchoredPosition = new Vector2(156, 22);
            menuConstrucaoCentral.sizeDelta = new Vector2(1480, 760);
            
            Debug.Log("[Warno Layout] Menu Central posicionado com offset (+60, -10).");
        }

        // 3. PAINEL DIREITO (Ficha Técnica)
        if (painelDireitoDetalhes != null && menuConstrucaoCentral != null)
        {
            // Garante que é filho do painel central
            painelDireitoDetalhes.SetParent(menuConstrucaoCentral, false);

            // Ancorado no Top-Right DENTRO do painel central
            painelDireitoDetalhes.anchorMin = new Vector2(1, 1);
            painelDireitoDetalhes.anchorMax = new Vector2(1, 1);
            painelDireitoDetalhes.pivot = new Vector2(1, 1);
            
            // Margem direita interna de 16px, e top de 16px para não colar na borda
            painelDireitoDetalhes.anchoredPosition = new Vector2(-16, -16); 
            
            // A altura acompanha o painel (menos as margens), largura fixa de 260
            painelDireitoDetalhes.sizeDelta = new Vector2(260, 728); 
            
            Debug.Log("[Warno Layout] Painel Direito ancorado dentro do Central.");
        }
        else if (painelDireitoDetalhes != null)
        {
             // Caso queira ele solto na tela
             painelDireitoDetalhes.anchorMin = new Vector2(1, 1);
             painelDireitoDetalhes.anchorMax = new Vector2(1, 1);
             painelDireitoDetalhes.pivot = new Vector2(1, 1);
             painelDireitoDetalhes.anchoredPosition = new Vector2(-12, -8);
             painelDireitoDetalhes.sizeDelta = new Vector2(260, 760);
        }

        Debug.Log("🔥 Layout Tático AAA Aplicado com Sucesso!");
    }
}
