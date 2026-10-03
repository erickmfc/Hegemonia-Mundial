using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gira as helices do C-400 em torno dos eixos locais exportados no GLB.
/// Helices adjacentes giram em sentidos alternados para equilibrar o torque.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimacaoHelicesC400 : MonoBehaviour
{
    [Min(0f)] public float velocidadeGrausPorSegundo = 1800f;
    public bool girarQuandoEstacionado;

    private Transform[] helices;
    private C700TransporteAereo transporte;

    private void Awake()
    {
        transporte = GetComponent<C700TransporteAereo>();
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        List<Transform> encontradas = new List<Transform>(4);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (EhPivoHelice(transforms[i].name))
                encontradas.Add(transforms[i]);
        }
        encontradas.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        helices = encontradas.ToArray();
    }

    private static bool EhPivoHelice(string nome)
    {
        return string.Equals(nome, "Propeller1", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(nome, "Propeller2", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(nome, "Propeller3", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(nome, "Propeller4", System.StringComparison.OrdinalIgnoreCase);
    }

    private void Update()
    {
        bool girando = girarQuandoEstacionado || transporte == null
            || (transporte.estadoAtual != C700TransporteAereo.EstadoC700.Solo
                && transporte.estadoAtual != C700TransporteAereo.EstadoC700.Estacionado
                && transporte.estadoAtual != C700TransporteAereo.EstadoC700.Carregando
                && transporte.estadoAtual != C700TransporteAereo.EstadoC700.FalhaMissao);
        if (!girando || helices == null) return;

        float velocidade = Mathf.Max(0f, velocidadeGrausPorSegundo) * Time.deltaTime;
        for (int i = 0; i < helices.Length; i++)
        {
            if (helices[i] != null)
                helices[i].Rotate(Vector3.up, (i % 2 == 0 ? 1f : -1f) * velocidade, Space.Self);
        }
    }
}
