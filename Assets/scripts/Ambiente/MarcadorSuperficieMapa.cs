using System.Collections.Generic;
using UnityEngine;

public enum TipoSuperficieMapa
{
    Agua,
    Chao
}

public enum ClassificacaoSuperficieMapa
{
    Desconhecida,
    Agua,
    Chao,
    Costa
}

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class MarcadorSuperficieMapa : MonoBehaviour
{
    [Header("Tipo")]
    [SerializeField] private TipoSuperficieMapa tipoSuperficie = TipoSuperficieMapa.Agua;

    [Header("Fonte")]
    [SerializeField] private bool usarCollidersDosFilhos = true;
    [SerializeField] private bool usarRenderersDosFilhos = true;
    [SerializeField] private bool atualizarEmTempoReal = true;
    [SerializeField] private bool desenharGizmos = true;

    [Header("Fallback")]
    [SerializeField] private Vector3 tamanhoFallback = new Vector3(120f, 12f, 120f);

    [Header("Amostra")]
    [SerializeField, Min(0f)] private float margemHorizontal = 0.5f;
    [SerializeField, Min(0.1f)] private float alturaAmostraExtra = 4f;

    private Collider[] _colliders = new Collider[0];
    private Renderer[] _renderers = new Renderer[0];
    private Bounds _bounds;
    private bool _hasBounds;
    private bool _cachePronto;

    public TipoSuperficieMapa TipoSuperficie
    {
        get { return tipoSuperficie; }
    }

    public Bounds Bounds
    {
        get { return _bounds; }
    }

    public bool HasBounds
    {
        get { return _hasBounds; }
    }

    private void Reset()
    {
        InferirTipoPeloNome();
        RebuildCaches();
        AtualizarBounds();
    }

    private void OnEnable()
    {
        RebuildCaches();
        AtualizarBounds();
        RegistroSuperficieMapa.Registrar(this);
    }

    private void OnDisable()
    {
        RegistroSuperficieMapa.Desregistrar(this);
    }

    private void OnDestroy()
    {
        RegistroSuperficieMapa.Desregistrar(this);
    }

    private void OnValidate()
    {
        RebuildCaches();
        AtualizarBounds();
        if (isActiveAndEnabled)
        {
            RegistroSuperficieMapa.Registrar(this);
        }
    }

    private void LateUpdate()
    {
        if (!atualizarEmTempoReal)
        {
            return;
        }

        if (!_cachePronto)
        {
            RebuildCaches();
        }

        AtualizarBounds();
        RegistroSuperficieMapa.Registrar(this);
    }

    public bool ContainsXZ(Vector3 position, float padding = 0f)
    {
        if (!_hasBounds)
        {
            return false;
        }

        float minX = _bounds.min.x - padding;
        float maxX = _bounds.max.x + padding;
        float minZ = _bounds.min.z - padding;
        float maxZ = _bounds.max.z + padding;

        return position.x >= minX && position.x <= maxX && position.z >= minZ && position.z <= maxZ;
    }

    public bool TrySampleSurfaceHeight(Vector3 position, out float height)
    {
        height = transform.position.y;

        float bestHeight = float.MinValue;
        bool found = false;

        float rayStartY = _hasBounds
            ? Mathf.Max(
                Mathf.Max(_bounds.max.y + alturaAmostraExtra, position.y + alturaAmostraExtra),
                transform.position.y + alturaAmostraExtra)
            : position.y + alturaAmostraExtra + 50f;

        Ray ray = new Ray(new Vector3(position.x, rayStartY, position.z), Vector3.down);

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                RaycastHit hit;
                if (!collider.Raycast(ray, out hit, rayStartY + 2000f))
                {
                    continue;
                }

                if (!found || hit.point.y > bestHeight)
                {
                    bestHeight = hit.point.y;
                    found = true;
                }
            }
        }

        if (!found)
        {
            if (_hasBounds)
            {
                bestHeight = tipoSuperficie == TipoSuperficieMapa.Agua ? _bounds.center.y : _bounds.max.y;
            }
            else
            {
                bestHeight = transform.position.y;
            }

            found = true;
        }

        height = bestHeight;
        return found;
    }

    private void InferirTipoPeloNome()
    {
        string nome = gameObject.name.ToLowerInvariant();
        if (nome.Contains("agua") || nome.Contains("water") || nome.Contains("ocean") || nome.Contains("sea") || nome.Contains("mar"))
        {
            tipoSuperficie = TipoSuperficieMapa.Agua;
            return;
        }

        if (nome.Contains("terra") || nome.Contains("chao") || nome.Contains("ground") || nome.Contains("terrain") || nome.Contains("land"))
        {
            tipoSuperficie = TipoSuperficieMapa.Chao;
        }
    }

    private void RebuildCaches()
    {
        _colliders = usarCollidersDosFilhos
            ? GetComponentsInChildren<Collider>(true)
            : new Collider[] { GetComponent<Collider>() };

        _renderers = usarRenderersDosFilhos
            ? GetComponentsInChildren<Renderer>(true)
            : new Renderer[] { GetComponent<Renderer>() };

        _cachePronto = true;
    }

    private void AtualizarBounds()
    {
        bool encontrou = false;

        if (usarCollidersDosFilhos && _colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!encontrou)
                {
                    _bounds = collider.bounds;
                    encontrou = true;
                }
                else
                {
                    _bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (!encontrou && usarRenderersDosFilhos && _renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!encontrou)
                {
                    _bounds = renderer.bounds;
                    encontrou = true;
                }
                else
                {
                    _bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!encontrou)
        {
            _bounds = new Bounds(transform.position, tamanhoFallback);
            encontrou = true;
        }

        _hasBounds = encontrou;

        if (_hasBounds)
        {
            _bounds.Expand(new Vector3(margemHorizontal * 2f, 0f, margemHorizontal * 2f));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmos)
        {
            return;
        }

        if (!_cachePronto)
        {
            RebuildCaches();
        }

        AtualizarBounds();

        Gizmos.color = tipoSuperficie == TipoSuperficieMapa.Agua
            ? new Color(0.1f, 0.45f, 1f, 0.35f)
            : new Color(0.55f, 0.35f, 0.15f, 0.35f);

        Gizmos.DrawWireCube(_bounds.center, _bounds.size);
        Gizmos.DrawSphere(_bounds.center, Mathf.Max(1f, Mathf.Min(_bounds.size.x, _bounds.size.z) * 0.05f));
    }
}

public static class RegistroSuperficieMapa
{
    private static readonly List<MarcadorSuperficieMapa> _agua = new List<MarcadorSuperficieMapa>();
    private static readonly List<MarcadorSuperficieMapa> _chao = new List<MarcadorSuperficieMapa>();

    public static void Registrar(MarcadorSuperficieMapa marcador)
    {
        if (marcador == null)
        {
            return;
        }

        LimparLista(_agua);
        LimparLista(_chao);

        List<MarcadorSuperficieMapa> listaPrincipal = marcador.TipoSuperficie == TipoSuperficieMapa.Agua ? _agua : _chao;
        List<MarcadorSuperficieMapa> listaSecundaria = marcador.TipoSuperficie == TipoSuperficieMapa.Agua ? _chao : _agua;

        if (!listaPrincipal.Contains(marcador))
        {
            listaPrincipal.Add(marcador);
        }

        if (listaSecundaria.Contains(marcador))
        {
            listaSecundaria.Remove(marcador);
        }
    }

    public static void Desregistrar(MarcadorSuperficieMapa marcador)
    {
        if (marcador == null)
        {
            return;
        }

        _agua.Remove(marcador);
        _chao.Remove(marcador);
    }

    public static bool HaSuperficie(TipoSuperficieMapa tipo)
    {
        return EncontrarPrimeiro(tipo) != null;
    }

    public static MarcadorSuperficieMapa EncontrarPrimeiro(TipoSuperficieMapa tipo)
    {
        List<MarcadorSuperficieMapa> lista = tipo == TipoSuperficieMapa.Agua ? _agua : _chao;
        LimparLista(lista);

        for (int i = 0; i < lista.Count; i++)
        {
            MarcadorSuperficieMapa marcador = lista[i];
            if (marcador != null && marcador.isActiveAndEnabled)
            {
                return marcador;
            }
        }

        return null;
    }

    public static bool TryGetAltura(Vector3 position, TipoSuperficieMapa tipo, out float height, float padding = 0f)
    {
        height = position.y;

        List<MarcadorSuperficieMapa> lista = tipo == TipoSuperficieMapa.Agua ? _agua : _chao;
        LimparLista(lista);

        bool encontrou = false;
        float melhorAltura = float.MinValue;

        for (int i = 0; i < lista.Count; i++)
        {
            MarcadorSuperficieMapa marcador = lista[i];
            if (marcador == null || !marcador.isActiveAndEnabled)
            {
                continue;
            }

            if (!marcador.ContainsXZ(position, padding))
            {
                continue;
            }

            float alturaMarcador;
            if (!marcador.TrySampleSurfaceHeight(position, out alturaMarcador))
            {
                continue;
            }

            if (!encontrou || alturaMarcador > melhorAltura)
            {
                melhorAltura = alturaMarcador;
                encontrou = true;
            }
        }

        if (encontrou)
        {
            height = melhorAltura;
        }

        return encontrou;
    }

    public static bool TryClassify(Vector3 position, out ClassificacaoSuperficieMapa classificacao, out float height, float coastTolerance = 1.5f, float padding = 0f)
    {
        bool temAgua = TryGetAltura(position, TipoSuperficieMapa.Agua, out float alturaAgua, padding);
        bool temChao = TryGetAltura(position, TipoSuperficieMapa.Chao, out float alturaChao, padding);

        if (temAgua && temChao)
        {
            if (Mathf.Abs(alturaAgua - alturaChao) <= coastTolerance)
            {
                classificacao = ClassificacaoSuperficieMapa.Costa;
                height = Mathf.Max(alturaAgua, alturaChao);
                return true;
            }

            if (alturaAgua > alturaChao)
            {
                classificacao = ClassificacaoSuperficieMapa.Agua;
                height = alturaAgua;
                return true;
            }

            classificacao = ClassificacaoSuperficieMapa.Chao;
            height = alturaChao;
            return true;
        }

        if (temAgua)
        {
            classificacao = ClassificacaoSuperficieMapa.Agua;
            height = alturaAgua;
            return true;
        }

        if (temChao)
        {
            classificacao = ClassificacaoSuperficieMapa.Chao;
            height = alturaChao;
            return true;
        }

        classificacao = ClassificacaoSuperficieMapa.Desconhecida;
        height = position.y;
        return false;
    }

    public static bool TryGetBounds(TipoSuperficieMapa tipo, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        List<MarcadorSuperficieMapa> lista = tipo == TipoSuperficieMapa.Agua ? _agua : _chao;
        LimparLista(lista);

        bool encontrou = false;
        for (int i = 0; i < lista.Count; i++)
        {
            MarcadorSuperficieMapa marcador = lista[i];
            if (marcador == null || !marcador.isActiveAndEnabled || !marcador.HasBounds)
            {
                continue;
            }

            if (!encontrou)
            {
                bounds = marcador.Bounds;
                encontrou = true;
            }
            else
            {
                bounds.Encapsulate(marcador.Bounds);
            }
        }

        return encontrou;
    }

    public static bool TryGetCombinedBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool encontrou = false;

        Bounds boundsAgua;
        if (TryGetBounds(TipoSuperficieMapa.Agua, out boundsAgua))
        {
            bounds = boundsAgua;
            encontrou = true;
        }

        Bounds boundsChao;
        if (TryGetBounds(TipoSuperficieMapa.Chao, out boundsChao))
        {
            if (!encontrou)
            {
                bounds = boundsChao;
                encontrou = true;
            }
            else
            {
                bounds.Encapsulate(boundsChao);
            }
        }

        return encontrou;
    }

    private static void LimparLista(List<MarcadorSuperficieMapa> lista)
    {
        for (int i = lista.Count - 1; i >= 0; i--)
        {
            MarcadorSuperficieMapa marcador = lista[i];
            if (marcador == null || !marcador.isActiveAndEnabled)
            {
                lista.RemoveAt(i);
            }
        }
    }
}
