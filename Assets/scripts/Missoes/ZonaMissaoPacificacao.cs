using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área local de uma missão da Operação Pacificação. Os próprios volumes se
/// registram no controlador; não há busca por objetos nem atualização por frame.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class ZonaMissaoPacificacao : MonoBehaviour
{
    private static readonly List<ZonaMissaoPacificacao> zonasRegistradas = new List<ZonaMissaoPacificacao>(16);
    public static event Action<ZonaMissaoPacificacao, bool> RegistroAlterado;

    [Header("Identificação da área")]
    [SerializeField] private string identificadorUnico = string.Empty;
    [SerializeField, Min(1f)] private float raioConfiguravel = 100f;
    [SerializeField] private string identificadorTerritorial = string.Empty;

    private SphereCollider volume;

    public string IdentificadorUnico => identificadorUnico;
    public float RaioConfiguravel => Mathf.Max(1f, raioConfiguravel);
    public float RaioMundo
    {
        get
        {
            Vector3 escala = transform.lossyScale;
            float maiorEixo = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z));
            return RaioConfiguravel * Mathf.Max(0.0001f, maiorEixo);
        }
    }
    public string IdentificadorTerritorial => identificadorTerritorial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarRegistroRuntime()
    {
        zonasRegistradas.Clear();
        RegistroAlterado = null;
    }

    public static void PreencherRegistradas(List<ZonaMissaoPacificacao> destino)
    {
        if (destino == null) return;
        destino.Clear();
        for (int i = zonasRegistradas.Count - 1; i >= 0; i--)
        {
            ZonaMissaoPacificacao zona = zonasRegistradas[i];
            if (zona == null || !zona.isActiveAndEnabled)
            {
                zonasRegistradas.RemoveAt(i);
                continue;
            }

            destino.Add(zona);
        }
    }

    private void Awake()
    {
        AtualizarVolume();
    }

    private void OnEnable()
    {
        GarantirIdentificador();
        AtualizarVolume();
        if (!zonasRegistradas.Contains(this))
        {
            zonasRegistradas.Add(this);
            RegistroAlterado?.Invoke(this, true);
        }
    }

    private void OnDisable()
    {
        if (zonasRegistradas.Remove(this)) RegistroAlterado?.Invoke(this, false);
    }

    private void OnValidate()
    {
        GarantirIdentificador();
        AtualizarVolume();
    }

    private void GarantirIdentificador()
    {
        if (string.IsNullOrWhiteSpace(identificadorUnico)
            && gameObject.name.StartsWith("Creatys_PAC_", StringComparison.Ordinal))
        {
            identificadorUnico = gameObject.name;
        }
    }

    private void AtualizarVolume()
    {
        if (volume == null) volume = GetComponent<SphereCollider>();
        if (volume == null) return;
        volume.isTrigger = true;
        volume.radius = Mathf.Max(1f, raioConfiguravel);
    }

    public bool Contem(Vector3 posicao)
    {
        float raio = RaioMundo;
        return (posicao - transform.position).sqrMagnitude <= raio * raio;
    }

    private void OnTriggerEnter(Collider outro)
    {
        IdentidadeUnidade identidade = outro != null
            ? outro.GetComponentInParent<IdentidadeUnidade>()
            : null;
        OperacaoPacificacaoControlador.Instancia?.RegistrarEntradaEmArea(this, identidade);
    }

    private void OnDrawGizmos()
    {
        float raio = RaioMundo;
        Gizmos.color = new Color(0.20f, 0.85f, 0.70f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, raio);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * raio * 0.25f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * raio * 0.28f,
            string.IsNullOrWhiteSpace(identificadorUnico) ? gameObject.name : identificadorUnico);
#endif
    }
}
