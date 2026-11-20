using System.Collections.Generic;
using UnityEngine;

/// <summary>Manifesto leve de passageiros civis, sem criar unidades de combate.</summary>
[DisallowMultipleComponent]
public sealed class ManifestoPesquisadoresExpedicao : MonoBehaviour
{
    [SerializeField, Min(0)] private int quantidadePesquisadores;
    [SerializeField] private List<string> nomesPesquisadores = new List<string>();

    public int QuantidadePesquisadores => quantidadePesquisadores;
    public IReadOnlyList<string> NomesPesquisadores => nomesPesquisadores;

    public void Configurar(int quantidade, IReadOnlyList<string> nomes)
    {
        quantidadePesquisadores = Mathf.Max(0, quantidade);
        if (nomesPesquisadores == null) nomesPesquisadores = new List<string>();
        nomesPesquisadores.Clear();
        if (nomes != null)
            for (int i = 0; i < nomes.Count && nomesPesquisadores.Count < quantidadePesquisadores; i++)
                if (!string.IsNullOrWhiteSpace(nomes[i])) nomesPesquisadores.Add(nomes[i]);
        while (nomesPesquisadores.Count < quantidadePesquisadores)
            nomesPesquisadores.Add("Pesquisador " + (nomesPesquisadores.Count + 1).ToString("00"));
    }
}
