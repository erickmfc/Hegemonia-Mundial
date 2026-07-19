using UnityEngine;
using System;

public class GerenciadorTempo : MonoBehaviour
{
    public static GerenciadorTempo Instancia { get; private set; }

    [Header("Configuração de Tempo")]
    [Tooltip("Duracao de um dia de jogo em segundos reais.")]
    public float duracaoDiaSegundos = 30f;
    public int totalDias = 1;

    public event Action OnDataAlterada;

    private float tempoAcumulado = 0f;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;

#if UNITY_2023_1_OR_NEWER
        GerenciadorTempo existente = FindFirstObjectByType<GerenciadorTempo>();
#else
        GerenciadorTempo existente = FindObjectOfType<GerenciadorTempo>();
#endif
        if (existente != null)
        {
            Instancia = existente;
            return;
        }

        GameObject go = new GameObject("GerenciadorTempo_Runtime");
        Instancia = go.AddComponent<GerenciadorTempo>();
    }

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (MenuPausaController.EstaPausado) return;

        tempoAcumulado += Time.deltaTime;
        float duracao = Mathf.Max(1f, duracaoDiaSegundos);
        while (tempoAcumulado >= duracao)
        {
            tempoAcumulado -= duracao;
            totalDias++;
            OnDataAlterada?.Invoke();
        }
    }

    public void RestaurarDias(int dias)
    {
        totalDias = Mathf.Max(1, dias);
        tempoAcumulado = 0f;
        OnDataAlterada?.Invoke();
    }
}
