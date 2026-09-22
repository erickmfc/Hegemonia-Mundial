using UnityEngine;

/// <summary>
/// Ajuste compartilhado da velocidade de deslocamento da frota.
/// Mantém o balanceamento dos prefabs intacto e evita editar cada navio
/// individualmente, inclusive os navios criados em runtime.
/// </summary>
public static class VelocidadeNavalGlobal
{
    /// <summary>
    /// Aumento operacional aplicado ao deslocamento horizontal dos navios.
    /// </summary>
    public const float Multiplicador = 1.5f;

    public static float Aplicar(float velocidadeBase)
    {
        if (float.IsNaN(velocidadeBase) || float.IsInfinity(velocidadeBase))
        {
            velocidadeBase = 0.1f;
        }

        return Mathf.Max(0.1f, velocidadeBase) * Multiplicador;
    }
}
