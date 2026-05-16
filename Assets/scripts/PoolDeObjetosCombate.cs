using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PoolDeObjetosCombate
{
    private static readonly Dictionary<int, Queue<GameObject>> PoolPorPrefab = new Dictionary<int, Queue<GameObject>>();
    private static readonly Dictionary<int, int> PrewarmPorPrefab = new Dictionary<int, int>();
    private static Transform raizPool;

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        int prefabId = prefab.GetInstanceID();
        Queue<GameObject> fila;
        if (PoolPorPrefab.TryGetValue(prefabId, out fila))
        {
            while (fila.Count > 0)
            {
                GameObject instancia = fila.Dequeue();
                if (instancia == null)
                {
                    continue;
                }

                PoolDeObjetoCombateLink link = instancia.GetComponent<PoolDeObjetoCombateLink>();
                if (link != null)
                {
                    link.EstaNoPool = false;
                }

                Transform tr = instancia.transform;
                tr.SetParent(null, false);
                tr.SetPositionAndRotation(position, rotation);
                instancia.SetActive(true);
                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("pool_hits");
                return instancia;
            }
        }

        GameObject criada = CriarInstancia(prefab, position, rotation);
        if (criada == null)
        {
            return null;
        }

        GarantirLink(criada, prefab);
        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("pool_misses");
        return criada;
    }

    public static void Prewarm(GameObject prefab, int quantidade)
    {
        if (prefab == null || quantidade <= 0)
        {
            return;
        }

        int prefabId = prefab.GetInstanceID();
        Queue<GameObject> fila;
        if (!PoolPorPrefab.TryGetValue(prefabId, out fila))
        {
            fila = new Queue<GameObject>();
            PoolPorPrefab[prefabId] = fila;
        }

        int quantidadeAtual;
        PrewarmPorPrefab.TryGetValue(prefabId, out quantidadeAtual);
        int alvo = Mathf.Max(quantidadeAtual, quantidade);
        int criadasComSucesso = quantidadeAtual;
        for (int i = quantidadeAtual; i < alvo; i++)
        {
            long inicioInstancia = System.Diagnostics.Stopwatch.GetTimestamp();
            GameObject instancia = CriarInstancia(prefab, Vector3.zero, Quaternion.identity);
            if (instancia == null)
            {
                break;
            }

            GarantirLink(instancia, prefab);
            PoolDeObjetoCombateLink link = instancia.GetComponent<PoolDeObjetoCombateLink>();
            if (link != null)
            {
                link.EstaNoPool = true;
            }

            instancia.transform.SetParent(GetRaizPool(), false);
            instancia.SetActive(false);
            fila.Enqueue(instancia);
            criadasComSucesso = i + 1;
            RegistrarTempoPrewarmItem(inicioInstancia);
        }

        PrewarmPorPrefab[prefabId] = criadasComSucesso;
    }

    public static int ObterQuantidadePreaquecida(GameObject prefab)
    {
        if (prefab == null)
        {
            return 0;
        }

        int quantidadeAtual;
        return PrewarmPorPrefab.TryGetValue(prefab.GetInstanceID(), out quantidadeAtual)
            ? quantidadeAtual
            : 0;
    }

    public static IEnumerator PrewarmIncremental(GameObject prefab, int quantidade, int instanciasPorFrame = 1)
    {
        if (prefab == null || quantidade <= 0)
        {
            yield break;
        }

        int prefabId = prefab.GetInstanceID();
        Queue<GameObject> fila;
        if (!PoolPorPrefab.TryGetValue(prefabId, out fila))
        {
            fila = new Queue<GameObject>();
            PoolPorPrefab[prefabId] = fila;
        }

        int quantidadeAtual;
        PrewarmPorPrefab.TryGetValue(prefabId, out quantidadeAtual);
        int alvo = Mathf.Max(quantidadeAtual, quantidade);
        int limitePorFrame = Mathf.Max(1, instanciasPorFrame);
        int criadasNoFrame = 0;

        for (int i = quantidadeAtual; i < alvo; i++)
        {
            long inicioInstancia = System.Diagnostics.Stopwatch.GetTimestamp();
            GameObject instancia = CriarInstancia(prefab, Vector3.zero, Quaternion.identity);
            if (instancia == null)
            {
                yield break;
            }

            GarantirLink(instancia, prefab);
            PoolDeObjetoCombateLink link = instancia.GetComponent<PoolDeObjetoCombateLink>();
            if (link != null)
            {
                link.EstaNoPool = true;
            }

            instancia.transform.SetParent(GetRaizPool(), false);
            instancia.SetActive(false);
            fila.Enqueue(instancia);
            PrewarmPorPrefab[prefabId] = i + 1;
            RegistrarTempoPrewarmItem(inicioInstancia);

            criadasNoFrame++;
            if (criadasNoFrame >= limitePorFrame)
            {
                criadasNoFrame = 0;
                yield return null;
            }
        }
    }

    public static void Release(GameObject instancia)
    {
        if (instancia == null)
        {
            return;
        }

        PoolDeObjetoCombateLink link = instancia.GetComponent<PoolDeObjetoCombateLink>();
        if (link == null || link.PrefabOrigem == null)
        {
            Object.Destroy(instancia);
            return;
        }

        if (link.EstaNoPool)
        {
            return;
        }

        int prefabId = link.PrefabOrigem.GetInstanceID();
        Queue<GameObject> fila;
        if (!PoolPorPrefab.TryGetValue(prefabId, out fila))
        {
            fila = new Queue<GameObject>();
            PoolPorPrefab[prefabId] = fila;
        }

        link.EstaNoPool = true;
        Transform tr = instancia.transform;
        tr.SetParent(GetRaizPool(), false);
        instancia.SetActive(false);
        fila.Enqueue(instancia);
    }

    public static GameObject SpawnTemporario(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, Vector3? scale = null)
    {
        GameObject instancia = Spawn(prefab, position, rotation);
        if (instancia == null)
        {
            return null;
        }

        if (scale.HasValue)
        {
            instancia.transform.localScale = scale.Value;
        }

        AutoRetornoPoolDeCombate autoRetorno = instancia.GetComponent<AutoRetornoPoolDeCombate>();
        if (autoRetorno == null)
        {
            autoRetorno = instancia.AddComponent<AutoRetornoPoolDeCombate>();
        }

        autoRetorno.AgendarRetorno(lifetime);
        return instancia;
    }

    private static void GarantirLink(GameObject instancia, GameObject prefab)
    {
        if (instancia == null)
        {
            return;
        }

        PoolDeObjetoCombateLink link = instancia.GetComponent<PoolDeObjetoCombateLink>();
        if (link == null)
        {
            link = instancia.AddComponent<PoolDeObjetoCombateLink>();
        }

        link.Configurar(prefab);
        link.EstaNoPool = false;
    }

    private static GameObject CriarInstancia(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        try
        {
            Object instanciaObj = Object.Instantiate((Object)prefab, position, rotation);
            GameObject instancia = instanciaObj as GameObject;
            if (instancia != null)
            {
                return instancia;
            }

            Component componente = instanciaObj as Component;
            if (componente != null)
            {
                return componente.gameObject;
            }

            if (instanciaObj != null)
            {
                Debug.LogWarning($"[PoolDeObjetosCombate] Prefab {prefab.name} instanciou como {instanciaObj.GetType().Name}, nao como GameObject.");
                Object.Destroy(instanciaObj);
            }
        }
        catch (System.InvalidCastException ex)
        {
            Debug.LogWarning($"[PoolDeObjetosCombate] Falha ao instanciar prefab {prefab.name}: {ex.Message}");
        }

        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("pool_misses");
        return null;
    }

    private static void RegistrarTempoPrewarmItem(long inicioInstancia)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicioInstancia) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("pool_prewarm_item_ms", elapsedMs);
        }
    }

    private static Transform GetRaizPool()
    {
        if (raizPool != null)
        {
            return raizPool;
        }

        GameObject root = GameObject.Find("__PoolDeObjetosCombate");
        if (root == null)
        {
            root = new GameObject("__PoolDeObjetosCombate");
            root.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(root);
        }

        raizPool = root.transform;
        return raizPool;
    }
}

public sealed class PoolDeObjetoCombateLink : MonoBehaviour
{
    [SerializeField] private GameObject prefabOrigem;

    public GameObject PrefabOrigem
    {
        get { return prefabOrigem; }
    }

    public bool EstaNoPool { get; set; }

    public void Configurar(GameObject prefab)
    {
        prefabOrigem = prefab;
    }
}

public sealed class AutoRetornoPoolDeCombate : MonoBehaviour
{
    private float expirarEm;
    private bool ativo;
    private ParticleSystem[] particulas;
    private AudioSource[] audios;

    public void AgendarRetorno(float lifetime)
    {
        if (particulas == null)
        {
            particulas = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (audios == null)
        {
            audios = GetComponentsInChildren<AudioSource>(true);
        }

        expirarEm = Time.time + Mathf.Max(0.05f, lifetime);
        ativo = true;

        for (int i = 0; i < particulas.Length; i++)
        {
            ParticleSystem ps = particulas[i];
            if (ps == null)
            {
                continue;
            }

            ps.Clear(true);
            ps.Play(true);
        }
    }

    void Update()
    {
        if (!ativo || Time.time < expirarEm)
        {
            return;
        }

        ativo = false;
        PoolDeObjetosCombate.Release(gameObject);
    }

    void OnDisable()
    {
        ativo = false;

        if (particulas != null)
        {
            for (int i = 0; i < particulas.Length; i++)
            {
                ParticleSystem ps = particulas[i];
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if (audios != null)
        {
            for (int i = 0; i < audios.Length; i++)
            {
                AudioSource source = audios[i];
                if (source != null)
                {
                    source.Stop();
                }
            }
        }
    }
}
