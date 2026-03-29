using System.Collections.Generic;
using UnityEngine;

public static class PontoSaidaUtil
{
    public static Transform[] Garantir(Transform origem, Transform[] pontosExistentes, params string[] dicasNome)
    {
        Transform[] validos = FiltrarValidos(pontosExistentes);
        if (validos != null && validos.Length > 0)
        {
            return validos;
        }

        if (origem != null)
        {
            var encontrados = new List<Transform>();
            foreach (Transform filho in origem.GetComponentsInChildren<Transform>(true))
            {
                if (filho == null || filho == origem) continue;

                string nome = Normalizar(filho.name);
                for (int i = 0; i < dicasNome.Length; i++)
                {
                    if (!string.IsNullOrEmpty(dicasNome[i]) && nome.Contains(Normalizar(dicasNome[i])))
                    {
                        encontrados.Add(filho);
                        break;
                    }
                }
            }

            validos = FiltrarValidos(encontrados.ToArray());
            if (validos != null && validos.Length > 0)
            {
                return validos;
            }
        }

        Transform fallback = CriarFallback(origem);
        return fallback != null ? new[] { fallback } : null;
    }

    public static Transform CriarFallback(Transform origem, string nomeObjeto = "_AutoPontoSaida")
    {
        if (origem == null) return null;

        Transform existente = origem.Find(nomeObjeto);
        if (existente != null)
        {
            return existente;
        }

        GameObject marcador = new GameObject(nomeObjeto);
        Transform ponto = marcador.transform;
        ponto.SetParent(origem, false);
        ponto.localPosition = CalcularPosicaoLocal(origem);
        ponto.localRotation = Quaternion.identity;
        return ponto;
    }

    static Transform[] FiltrarValidos(Transform[] pontos)
    {
        if (pontos == null || pontos.Length == 0) return null;

        var lista = new List<Transform>(pontos.Length);
        for (int i = 0; i < pontos.Length; i++)
        {
            if (pontos[i] != null)
            {
                lista.Add(pontos[i]);
            }
        }

        return lista.Count > 0 ? lista.ToArray() : null;
    }

    static Vector3 CalcularPosicaoLocal(Transform origem)
    {
        Renderer render = origem.GetComponentInChildren<Renderer>();
        if (render != null)
        {
            Bounds bounds = render.bounds;
            float frente = Mathf.Max(1.5f, bounds.extents.z + 0.6f);
            float altura = Mathf.Max(0.4f, bounds.extents.y * 0.4f);
            Vector3 mundo = origem.position + origem.forward * frente + origem.up * altura;
            return origem.InverseTransformPoint(mundo);
        }

        return new Vector3(0f, 0.5f, 1.5f);
    }

    static string Normalizar(string valor)
    {
        return string.IsNullOrEmpty(valor) ? string.Empty : valor.Replace(" ", string.Empty).ToLowerInvariant();
    }
}
