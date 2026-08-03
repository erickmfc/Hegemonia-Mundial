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
    private float ultimaVidaAtual = float.NaN;
    private float ultimaVidaMaxima = float.NaN;
    private bool ultimoCanvasHabilitado = true;

    void Start()
    {
        camPrincipal = Camera.main;
        canvasLocal = GetComponent<Canvas>();
        ultimoCanvasHabilitado = canvasLocal == null || canvasLocal.enabled;

        // Tenta achar o sistema de danos no pai se não estiver atribuído
        if (sistemaDeDanos == null)
        {
            sistemaDeDanos = GetComponentInParent<SistemaDeDanos>();
        }
    }

    void LateUpdate()
    {
        if (camPrincipal == null)
        {
            camPrincipal = Camera.main;
        }
        if (canvasLocal != null && !canvasLocal.enabled && sistemaDeDanos != null &&
            Mathf.Approximately(sistemaDeDanos.vidaAtual, ultimaVidaAtual) &&
            Mathf.Approximately(sistemaDeDanos.vidaMaxima, ultimaVidaMaxima))
        {
            return;
        }
        // 1. BILLBOARD (Olhar para a câmera)
        if (camPrincipal != null) 
        {
            transform.LookAt(transform.position + camPrincipal.transform.forward);
        }

        // 2. ATUALIZAR BARRA
        if (sistemaDeDanos != null && barraPreenchimento != null)
        {
            if (Mathf.Approximately(sistemaDeDanos.vidaAtual, ultimaVidaAtual) &&
                Mathf.Approximately(sistemaDeDanos.vidaMaxima, ultimaVidaMaxima))
            {
                return;
            }

            ultimaVidaAtual = sistemaDeDanos.vidaAtual;
            ultimaVidaMaxima = sistemaDeDanos.vidaMaxima;
            float pct = sistemaDeDanos.vidaMaxima > 0 ? (float)sistemaDeDanos.vidaAtual / (float)sistemaDeDanos.vidaMaxima : 0f;
            barraPreenchimento.fillAmount = pct;

            // Cor
            if(gradienteVida != null)
            {
                barraPreenchimento.color = gradienteVida.Evaluate(pct);
            }

            // Esconder se 100%
            if(canvasLocal != null && esconderSeCheia)
            {
                bool deveMostrar = (pct < 0.99f && pct > 0);
                if (ultimoCanvasHabilitado != deveMostrar)
                {
                    canvasLocal.enabled = deveMostrar;
                    ultimoCanvasHabilitado = deveMostrar;
                }
            }
        }
    }
}
