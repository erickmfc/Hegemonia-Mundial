using UnityEngine;
using System.Collections;

public class AnimacaoIA_Terminal : MonoBehaviour
{
    private bool iaPronta = false;
    private string textoAnimado = "";
    
    private string statusCustomizado = "";

    void Start()
    {
        // Se o motor neural não estiver na cena, não mostra a tela de sincronização
        if (Hegemonia.AI.Llama.LlamaClient.Instancia == null)
        {
            iaPronta = true;
            return;
        }

        // Inicia o loop da animação do texto
        StartCoroutine(AnimarTerminalMilitar());
    }

    public void AtualizarProgresso(string status, long completed, long total)
    {
        if (total > 0)
        {
            float pct = (float)completed / total * 100f;
            double completedMB = completed / (1024.0 * 1024.0);
            double totalMB = total / (1024.0 * 1024.0);
            statusCustomizado = $"{status} ({pct:F1}% - {completedMB:F1}MB / {totalMB:F1}MB)";
        }
        else
        {
            statusCustomizado = status;
        }
    }

    IEnumerator AnimarTerminalMilitar()
    {
        string[] spinner = { "[ - ]", "[ \\ ]", "[ | ]", "[ / ]" };
        int index = 0;

        while (!iaPronta)
        {
            string statusExibicao = string.IsNullOrEmpty(statusCustomizado) ? "Inicializando Motor Neural..." : statusCustomizado;
            // Alterna o ícone para dar sensação de processamento
            textoAnimado = $"SINCRONIZANDO ALTO COMANDO GLOBAL {spinner[index]}\n{statusExibicao}";
            index = (index + 1) % spinner.Length;
            
            yield return new WaitForSeconds(0.15f); // Velocidade do giro
        }
    }

    void OnGUI()
    {
        // Se a IA já carregou, não desenha mais nada na tela
        if (iaPronta) return;

        // Criação do estilo visual (Sem Canvas)
        GUIStyle estiloMilitar = new GUIStyle();
        estiloMilitar.fontSize = 22;
        estiloMilitar.fontStyle = FontStyle.Bold;
        estiloMilitar.normal.textColor = new Color(0.2f, 1f, 0.2f); // Verde neon (tipo terminal)
        estiloMilitar.alignment = TextAnchor.LowerRight;

        // Sombra do texto para dar leitura em qualquer fundo
        GUIStyle sombra = new GUIStyle(estiloMilitar);
        sombra.normal.textColor = Color.black;

        // Posição no canto inferior direito da tela
        Rect retangulo = new Rect(0, 0, Screen.width - 30, Screen.height - 30);
        Rect retanguloSombra = new Rect(2, 2, Screen.width - 30, Screen.height - 30);

        // Renderiza na tela
        GUI.Label(retanguloSombra, textoAnimado, sombra);
        GUI.Label(retangulo, textoAnimado, estiloMilitar);
    }

    // O LlamaClient vai chamar essa função quando o modelo estiver 100% pronto
    public void OcultarAnimacao()
    {
        iaPronta = true;
    }
}
