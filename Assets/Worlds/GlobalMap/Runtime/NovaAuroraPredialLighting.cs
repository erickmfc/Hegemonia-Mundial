using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the Nova Aurora window emission from the existing visual day/night cycle.
/// Add this component to the root of the city model.
/// </summary>
[DisallowMultipleComponent]
public sealed class NovaAuroraPredialLighting : MonoBehaviour
{
    private const string WindowMaterialName = "NA_Janelas_Iluminadas";
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color NightWindowColor = new Color(1.0f, 0.58f, 0.30f, 1.0f);

    [Header("Iluminação das janelas")]
    [SerializeField, Min(0f)] private float intensidadeNoite = 1.8f;
    [SerializeField, Min(0.05f)] private float intervaloAtualizacao = 0.25f;

    private readonly List<Material> _materiaisEmissivos = new List<Material>(4);
    private CicloDiaNoite _cicloDiaNoite;
    private float _tempoAteAtualizacao;
    private float _ultimaIntensidade = -1f;
    private bool _avisouSemMaterial;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AoCarregarCena;

        if (!Application.isPlaying)
        {
            return;
        }

        CachearMateriais();
        ResolverCiclo();
        AplicarIntensidade(0f, true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;

        if (Application.isPlaying)
        {
            AplicarIntensidade(0f, true);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _tempoAteAtualizacao += Time.deltaTime;
        if (_tempoAteAtualizacao < Mathf.Max(0.05f, intervaloAtualizacao))
        {
            return;
        }

        _tempoAteAtualizacao = 0f;
        if (_cicloDiaNoite == null)
        {
            ResolverCiclo();
        }

        if (_cicloDiaNoite == null)
        {
            return;
        }

        float fatorNoturno = CalcularFatorNoturno(_cicloDiaNoite.HoraAtual);
        AplicarIntensidade(fatorNoturno, false);
    }

    private void AoCarregarCena(Scene scene, LoadSceneMode mode)
    {
        if (Application.isPlaying)
        {
            ResolverCiclo();
        }
    }

    private void ResolverCiclo()
    {
#if UNITY_2023_1_OR_NEWER
        _cicloDiaNoite = FindFirstObjectByType<CicloDiaNoite>();
#else
        _cicloDiaNoite = FindObjectOfType<CicloDiaNoite>();
#endif
    }

    private void CachearMateriais()
    {
        _materiaisEmissivos.Clear();
        var renderizadores = GetComponentsInChildren<Renderer>(true);
        var encontrados = new HashSet<Material>();

        foreach (var renderizador in renderizadores)
        {
            var materiais = renderizador.sharedMaterials;
            foreach (var material in materiais)
            {
                if (material == null ||
                    material.name.IndexOf(WindowMaterialName, StringComparison.Ordinal) < 0 ||
                    !material.HasProperty(EmissionColorId) ||
                    !encontrados.Add(material))
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                _materiaisEmissivos.Add(material);
            }
        }

        if (_materiaisEmissivos.Count == 0 && !_avisouSemMaterial)
        {
            Debug.LogWarning("Nova Aurora: não encontrei o material NA_Janelas_Iluminadas nos renderizadores do modelo.", this);
            _avisouSemMaterial = true;
        }
    }

    private void AplicarIntensidade(float fatorNoturno, bool forcar)
    {
        float intensidade = Mathf.Clamp01(fatorNoturno) * intensidadeNoite;
        if (!forcar && Mathf.Abs(intensidade - _ultimaIntensidade) < 0.01f)
        {
            return;
        }

        _ultimaIntensidade = intensidade;
        Color cor = intensidade <= 0f ? Color.black : NightWindowColor * intensidade;
        cor.a = 1f;

        for (int i = 0; i < _materiaisEmissivos.Count; i++)
        {
            Material material = _materiaisEmissivos[i];
            if (material != null)
            {
                material.SetColor(EmissionColorId, cor);
            }
        }
    }

    private static float CalcularFatorNoturno(float hora)
    {
        hora = Mathf.Repeat(hora, 24f);

        if (hora >= 19f || hora < 5.5f)
        {
            return 1f;
        }

        if (hora >= 16.5f)
        {
            float t = Mathf.InverseLerp(16.5f, 19f, hora);
            return Suavizar(t);
        }

        if (hora < 7.5f)
        {
            float t = Mathf.InverseLerp(5.5f, 7.5f, hora);
            return 1f - Suavizar(t);
        }

        return 0f;
    }

    private static float Suavizar(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
