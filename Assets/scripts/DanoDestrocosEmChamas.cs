using UnityEngine;
using System.Collections;

public class DestrocosEmChamas : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Escala do fogo. 1.0 é o padrão.")]
    public float tamanhoFogo = 1.0f;

    [Tooltip("Se marcado, o fogo começa imediatamente. Se não, você pode chamar IniciarFogo() via script.")]
    public bool iniciarNoStart = true;

    [Header("Explosões Finais")]
    [Tooltip("Define se os destroços devem explodir algumas vezes antes de sumir.")]
    public bool causarExplosoes = true;
    public int qtdExplosoes = 3;
    public float vidaUtil = 15.0f; // Tempo que o objeto dura (deve ser compatível com o tempo de Destroy no SistemaDeDanos)

    void Start()
    {
        if (iniciarNoStart) IniciarFogo();
        if (causarExplosoes) StartCoroutine(RotinaExplosoes());
    }

    public void IniciarFogo()
    {
        // ... Lógica existente de fogo ...
        if (GerenciadorFXGlobal.Instancia != null)
        {
            GameObject fogo = GerenciadorFXGlobal.Instancia.CriarEfeitoContinuo("Fogo", transform);
            
            if (fogo != null)
            {
                fogo.transform.localScale = Vector3.one * tamanhoFogo;
                fogo.transform.localPosition += Vector3.up * 0.5f;
            }
        }
    }

    IEnumerator RotinaExplosoes()
    {
        // Distribui as explosões ao longo da vida útil (ex: 15s)
        // Deixa uma margem inicial de 1s e final de 2s para não ficar estranho
        float tempoDisponivel = vidaUtil - 3.0f;
        if (tempoDisponivel <= 0) tempoDisponivel = 1.0f;

        // Tenta espaçar as explosões
        for(int i = 0; i < qtdExplosoes; i++)
        {
            // Espera aleatória entre os intervalos
            float intervalo = tempoDisponivel / qtdExplosoes;
            float espera = Random.Range(1.0f, intervalo);
            
            yield return new WaitForSeconds(espera);

            // Kabum
             if (GerenciadorFXGlobal.Instancia != null)
            {
                GerenciadorFXGlobal.Instancia.TocarExplosao(transform.position); // Explosão Visual
                
                // Som
                if(GerenciadorFXGlobal.Instancia.somExplosao != null)
                {
                    AudioSource.PlayClipAtPoint(GerenciadorFXGlobal.Instancia.somExplosao, transform.position);
                }
            }
        }
    }
}
