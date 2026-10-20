using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Balsa visual e independente do casco que originou uma ocorrência SAR.
/// Mantém os sobreviventes sincronizados com as coletas do sistema de resgate.
/// </summary>
[DisallowMultipleComponent]
public sealed class BalsaSalvamento : MonoBehaviour
{
    public const int CapacidadePadrao = 24;

    private static readonly List<BalsaSalvamento> balsasAtivas = new List<BalsaSalvamento>(32);

    [Header("Ocupacao")]
    [SerializeField, Min(1)] private int capacidadeMaxima = CapacidadePadrao;
    [SerializeField, Min(0)] private int sobreviventes;
    [SerializeField] private string incidentId = string.Empty;

    [Header("Flutuacao")]
    [SerializeField, Min(0f)] private float velocidadeDeriva = 0.08f;
    [SerializeField, Min(0f)] private float distanciaMaximaDeriva = 18f;
    [SerializeField, Min(0f)] private float alturaSobreAgua = 0.04f;
    [SerializeField, Min(0f)] private float amplitudeOndulacao = 0.07f;
    [SerializeField, Min(0f)] private float frequenciaOndulacao = 0.8f;

    private Vector3 posicaoInicial;
    private Vector3 direcaoDeriva;
    private Quaternion rotacaoInicial;
    private float faseOndulacao;
    private float tempoFlutuando;
    private bool configurada;

    public string IncidentId => incidentId;
    public int Capacidade => Mathf.Clamp(capacidadeMaxima, 1, CapacidadePadrao);
    public int Sobreviventes => Mathf.Clamp(sobreviventes, 0, Capacidade);

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!balsasAtivas.Contains(this)) balsasAtivas.Add(this);
    }

    private void OnDisable()
    {
        balsasAtivas.Remove(this);
    }

    /// <summary>Configura a balsa imediatamente depois de instanciá-la.</summary>
    public void Configurar(string idIncidente, int quantidadeSobreviventes)
    {
        incidentId = idIncidente ?? string.Empty;
        capacidadeMaxima = CapacidadePadrao;
        sobreviventes = Mathf.Clamp(quantidadeSobreviventes, 0, CapacidadePadrao);
        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;

        uint hash = HashEstavel(incidentId);
        float angulo = (hash % 36000u) * (Mathf.PI / 18000f);
        direcaoDeriva = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
        faseOndulacao = ((hash >> 8) % 10000u) * (Mathf.PI * 2f / 10000f);
        tempoFlutuando = 0f;
        configurada = true;

        if (Application.isPlaying && !balsasAtivas.Contains(this)) balsasAtivas.Add(this);
        if (sobreviventes <= 0) DestruirBalsa();
    }

    private void Update()
    {
        if (!Application.isPlaying || !configurada || sobreviventes <= 0) return;
        if (MenuPausaController.EstaPausado) return;

        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        tempoFlutuando += delta;

        Vector3 posicao = posicaoInicial + direcaoDeriva * Mathf.Min(
            distanciaMaximaDeriva,
            velocidadeDeriva * tempoFlutuando);
        float onda = Mathf.Sin(tempoFlutuando * frequenciaOndulacao + faseOndulacao) * amplitudeOndulacao;
        if (RegistroSuperficieMapa.TryGetAltura(posicao, TipoSuperficieMapa.Agua, out float alturaAgua))
            posicao.y = alturaAgua + alturaSobreAgua + onda;
        else
            posicao.y = posicaoInicial.y + onda;

        transform.position = posicao;
        float balanco = Mathf.Sin(tempoFlutuando * frequenciaOndulacao * 0.7f + faseOndulacao) * 2f;
        float adernamento = Mathf.Sin(tempoFlutuando * frequenciaOndulacao + faseOndulacao) * 2.5f;
        transform.rotation = rotacaoInicial * Quaternion.Euler(balanco, 0f, adernamento);
    }

    public static bool ExisteParaIncidente(string idIncidente)
    {
        if (string.IsNullOrWhiteSpace(idIncidente)) return false;
        for (int i = balsasAtivas.Count - 1; i >= 0; i--)
        {
            BalsaSalvamento balsa = balsasAtivas[i];
            if (balsa == null) { balsasAtivas.RemoveAt(i); continue; }
            if (balsa.isActiveAndEnabled && string.Equals(balsa.incidentId, idIncidente, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static int ContarSobreviventes(string idIncidente)
    {
        if (string.IsNullOrWhiteSpace(idIncidente)) return 0;
        int total = 0;
        for (int i = balsasAtivas.Count - 1; i >= 0; i--)
        {
            BalsaSalvamento balsa = balsasAtivas[i];
            if (balsa == null) { balsasAtivas.RemoveAt(i); continue; }
            if (balsa.isActiveAndEnabled && string.Equals(balsa.incidentId, idIncidente, StringComparison.Ordinal))
                total += balsa.Sobreviventes;
        }
        return total;
    }

    /// <summary>Remove pessoas das balsas durante uma coleta SAR.</summary>
    public static int ResgatarSobreviventes(string idIncidente, int quantidade)
    {
        return RemoverSobreviventes(idIncidente, quantidade);
    }

    /// <summary>Remove pessoas das balsas quando a janela de sobrevivencia termina.</summary>
    public static int PerderSobreviventes(string idIncidente, int quantidade)
    {
        return RemoverSobreviventes(idIncidente, quantidade);
    }

    private static int RemoverSobreviventes(string idIncidente, int quantidade)
    {
        if (string.IsNullOrWhiteSpace(idIncidente) || quantidade <= 0) return 0;
        int removidos = 0;
        for (int i = balsasAtivas.Count - 1; i >= 0 && removidos < quantidade; i--)
        {
            BalsaSalvamento balsa = balsasAtivas[i];
            if (balsa == null) { balsasAtivas.RemoveAt(i); continue; }
            if (!string.Equals(balsa.incidentId, idIncidente, StringComparison.Ordinal)) continue;
            int restantes = quantidade - removidos;
            int retirados = Mathf.Min(restantes, balsa.Sobreviventes);
            balsa.sobreviventes -= retirados;
            removidos += retirados;
            if (balsa.sobreviventes <= 0) balsa.DestruirBalsa();
        }
        return removidos;
    }

    /// <summary>Remove apenas as balsas associadas a um incidente restaurado/substituido.</summary>
    public static void RemoverDoIncidente(string idIncidente)
    {
        if (string.IsNullOrWhiteSpace(idIncidente)) return;
        for (int i = balsasAtivas.Count - 1; i >= 0; i--)
        {
            BalsaSalvamento balsa = balsasAtivas[i];
            if (balsa == null) { balsasAtivas.RemoveAt(i); continue; }
            if (!string.Equals(balsa.incidentId, idIncidente, StringComparison.Ordinal)) continue;
            balsa.DestruirBalsa();
        }
    }

    private void DestruirBalsa()
    {
        balsasAtivas.Remove(this);
        if (Application.isPlaying) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    private static uint HashEstavel(string texto)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < texto.Length; i++)
                hash = (hash ^ texto[i]) * 16777619u;
            return hash;
        }
    }
}
