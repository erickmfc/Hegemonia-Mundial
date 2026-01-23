using UnityEngine;
using UnityEngine.UI;

public class BarraDeVida : MonoBehaviour
{
    [Header("Referências")]
    public SistemaDeDanos sistemaDeDanos;
    public Image barraPreenchimento; // O "Fill" da barra

    [Header("Visual")]
    public Gradient gradienteVida; // Verde -> Amarelo -> Vermelho
    public bool esconderSeCheia = true;

    private Camera camPrincipal;
    private Canvas canvasLocal;

    void Start()
    {
        camPrincipal = Camera.main;
        canvasLocal = GetComponent<Canvas>();

        // Tenta achar o sistema de danos no pai se não estiver atribuído
        if (sistemaDeDanos == null)
        {
            sistemaDeDanos = GetComponentInParent<SistemaDeDanos>();
        }
    }

    void LateUpdate()
    {
        // 1. BILLBOARD (Olhar para a câmera)
        if (camPrincipal != null) 
        {
            transform.LookAt(transform.position + camPrincipal.transform.forward);
        }

        // 2. ATUALIZAR BARRA
        if (sistemaDeDanos != null && barraPreenchimento != null)
        {
            float pct = (float)sistemaDeDanos.vidaAtual / (float)sistemaDeDanos.vidaMaxima;
            barraPreenchimento.fillAmount = pct;

            // Cor
            if(gradienteVida != null)
            {
                barraPreenchimento.color = gradienteVida.Evaluate(pct);
            }

            // Esconder se 100%
            if(canvasLocal != null && esconderSeCheia)
            {
                canvasLocal.enabled = (pct < 0.99f && pct > 0);
            }
        }
    }
}
