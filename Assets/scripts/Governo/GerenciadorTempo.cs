using UnityEngine;
using System;

public class GerenciadorTempo : MonoBehaviour
{
    public static GerenciadorTempo Instancia { get; private set; }

    [Header("Configuração de Tempo")]
    public float duracaoDiaSegundos = 10f;
    public int totalDias = 1;

    public event Action OnDataAlterada;

    private float tempoAcumulado = 0f;

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
        if (tempoAcumulado >= duracaoDiaSegundos)
        {
            tempoAcumulado = 0f;
            totalDias++;
            OnDataAlterada?.Invoke();
        }
    }

    public void RestaurarDias(int dias)
    {
        totalDias = dias;
        tempoAcumulado = 0f;
        OnDataAlterada?.Invoke();
    }
}
