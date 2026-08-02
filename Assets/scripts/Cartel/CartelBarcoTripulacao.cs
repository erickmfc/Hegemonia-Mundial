using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hegemonia.Cartel
{
    /// <summary>
    /// Mantém os tripulantes do cartel presos a pontos locais do barco e
    /// apresenta a carga roubada em uma posição elevada do convés.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hegemonia/Cartel/Tripulacao do barco")]
    public sealed class CartelBarcoTripulacao : MonoBehaviour
    {
        [Header("Pontos arrastáveis no Scene")]
        [Tooltip("Arraste aqui os Transforms dos pontos onde cada tripulante ficará.")]
        public Transform[] pontosTripulacao;
        [Tooltip("Arraste aqui o Transform que marca a altura e a posição da carga.")]
        public Transform pontoCarga;

        [HideInInspector]
        public CartelManualCreate createBaseTripulacao;
        [HideInInspector]
        public Vector3[] posicoesTripulacao =
        {
            new Vector3(-0.85f, 1.05f, 0.55f),
            new Vector3(0.85f, 1.05f, 0.55f),
            new Vector3(-0.75f, 1.05f, -0.65f),
            new Vector3(0.75f, 1.05f, -0.65f)
        };

        [Header("Compatibilidade com Create")]
        [HideInInspector]
        public CartelManualCreate createCarga;
        [HideInInspector]
        public Vector3 posicaoCargaLocal = new Vector3(0f, 1.35f, -1.15f);
        public GameObject prefabCargaVisual;
        public bool criarCargaVisualSeNaoHouverPrefab = true;
        [Min(0.1f)] public float escalaCarga = 0.85f;

        private sealed class TripulanteAncorado
        {
            public GameObject unidade;
            public Transform ponto;
            public Transform paiOriginal;
            public Vector3 posicaoMundoOriginal;
            public Quaternion rotacaoMundoOriginal;
            public Vector3 escalaMundoOriginal;
            public Vector3 escalaLocalOriginal;
            public ControleUnidade controle;
            public bool controleEstavaAtivo;
            public NavMeshAgent agente;
            public bool agenteEstavaAtivo;
            public Rigidbody[] rigidbodies;
            public bool[] rigidbodiesKinematic;
            public bool[] rigidbodiesGravity;
            public Collider[] colliders;
            public bool[] collidersAtivos;
            public Animator animator;
            public bool rootMotionEstavaAtivo;
        }

        private readonly List<TripulanteAncorado> tripulantes = new List<TripulanteAncorado>();
        private readonly List<Transform> pontos = new List<Transform>();
        private GameObject cargaVisual;
        private float quantidadeCarga;

        public float QuantidadeCarga { get { return quantidadeCarga; } }
        public int TripulantesABordo { get { return tripulantes.Count; } }

        private void Awake()
        {
            GarantirPontosLocais();
        }

        private void LateUpdate()
        {
            LimparTripulantesInvalidos();

            for (int i = 0; i < tripulantes.Count; i++)
            {
                TripulanteAncorado registro = tripulantes[i];
                if (registro == null || registro.unidade == null || registro.ponto == null)
                {
                    continue;
                }

                if (registro.unidade.transform.parent != transform)
                {
                    registro.unidade.transform.SetParent(transform, false);
                }

                registro.unidade.transform.position = registro.ponto.position;
                registro.unidade.transform.rotation = registro.ponto.rotation;
            }

            AtualizarTransformacaoDaCarga();
        }

        public bool TemVaga()
        {
            return tripulantes.Count < pontos.Count;
        }

        public bool FixarTripulante(GameObject unidade, int indicePreferido = -1)
        {
            if (unidade == null || unidade == gameObject)
            {
                return false;
            }

            GarantirPontosLocais();
            if (tripulantes.Exists(item => item != null && item.unidade == unidade))
            {
                return true;
            }

            int indice = EscolherIndiceLivre(indicePreferido);
            if (indice < 0)
            {
                return false;
            }

            Transform unidadeTransform = unidade.transform;
            TripulanteAncorado registro = new TripulanteAncorado
            {
                unidade = unidade,
                ponto = pontos[indice],
                paiOriginal = unidadeTransform.parent,
                posicaoMundoOriginal = unidadeTransform.position,
                rotacaoMundoOriginal = unidadeTransform.rotation,
                escalaMundoOriginal = unidadeTransform.lossyScale,
                escalaLocalOriginal = unidadeTransform.localScale,
                controle = unidade.GetComponent<ControleUnidade>() ?? unidade.GetComponentInChildren<ControleUnidade>(true),
                agente = unidade.GetComponent<NavMeshAgent>() ?? unidade.GetComponentInChildren<NavMeshAgent>(true),
                rigidbodies = unidade.GetComponentsInChildren<Rigidbody>(true),
                colliders = unidade.GetComponentsInChildren<Collider>(true),
                animator = unidade.GetComponent<Animator>() ?? unidade.GetComponentInChildren<Animator>(true)
            };

            registro.controleEstavaAtivo = registro.controle != null && registro.controle.enabled;
            registro.agenteEstavaAtivo = registro.agente != null && registro.agente.enabled;
            registro.rigidbodiesKinematic = new bool[registro.rigidbodies.Length];
            registro.rigidbodiesGravity = new bool[registro.rigidbodies.Length];
            registro.collidersAtivos = new bool[registro.colliders.Length];

            for (int i = 0; i < registro.rigidbodies.Length; i++)
            {
                if (registro.rigidbodies[i] == null) continue;
                registro.rigidbodiesKinematic[i] = registro.rigidbodies[i].isKinematic;
                registro.rigidbodiesGravity[i] = registro.rigidbodies[i].useGravity;
                registro.rigidbodies[i].isKinematic = true;
                registro.rigidbodies[i].useGravity = false;
            }

            for (int i = 0; i < registro.colliders.Length; i++)
            {
                if (registro.colliders[i] == null) continue;
                registro.collidersAtivos[i] = registro.colliders[i].enabled;
                registro.colliders[i].enabled = false;
            }

            if (registro.controle != null)
            {
                registro.controle.enabled = false;
            }

            if (registro.agente != null)
            {
                if (registro.agente.isActiveAndEnabled && registro.agente.isOnNavMesh)
                {
                    registro.agente.isStopped = true;
                }
                registro.agente.enabled = false;
            }

            if (registro.animator != null)
            {
                registro.rootMotionEstavaAtivo = registro.animator.applyRootMotion;
                registro.animator.applyRootMotion = false;
                TocarParado(registro.animator);
            }

            ArmaNaMaoRuntime fixadorArma = unidade.GetComponent<ArmaNaMaoRuntime>();
            if (fixadorArma == null) fixadorArma = unidade.AddComponent<ArmaNaMaoRuntime>();
            fixadorArma.RepararAgora();

            unidadeTransform.SetParent(transform, true);
            unidadeTransform.position = registro.ponto.position;
            unidadeTransform.rotation = registro.ponto.rotation;
            tripulantes.Add(registro);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (pontosTripulacao != null)
            {
                Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.95f);
                for (int i = 0; i < pontosTripulacao.Length; i++)
                {
                    Transform ponto = pontosTripulacao[i];
                    if (ponto == null) continue;
                    Gizmos.DrawWireSphere(ponto.position, 0.18f);
                    Gizmos.DrawLine(ponto.position, ponto.position + ponto.up * 0.6f);
                }
            }

            if (pontoCarga != null)
            {
                Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
                Gizmos.DrawWireCube(pontoCarga.position, Vector3.one * 0.45f);
                Gizmos.DrawLine(pontoCarga.position, pontoCarga.position + pontoCarga.up * 0.9f);
            }
        }
#endif

        public void LiberarTripulantes()
        {
            for (int i = tripulantes.Count - 1; i >= 0; i--)
            {
                LiberarTripulante(tripulantes[i]);
            }

            tripulantes.Clear();
        }

        public void DefinirCarga(float quantidade)
        {
            quantidadeCarga = Mathf.Max(0f, quantidade);
            if (quantidadeCarga <= 0.01f)
            {
                if (cargaVisual != null) cargaVisual.SetActive(false);
                return;
            }

            GarantirCargaVisual();
            if (cargaVisual == null) return;

            cargaVisual.SetActive(true);
            float altura = Mathf.Clamp(0.6f + Mathf.Log10(Mathf.Max(1f, quantidadeCarga)) * 0.16f, 0.6f, 1.8f);
            cargaVisual.transform.localScale = new Vector3(escalaCarga, altura, escalaCarga * 1.25f);
            AtualizarTransformacaoDaCarga();
        }

        private void GarantirPontosLocais()
        {
            pontos.Clear();
            if (pontosTripulacao != null && pontosTripulacao.Length > 0)
            {
                for (int i = 0; i < pontosTripulacao.Length; i++)
                {
                    if (pontosTripulacao[i] != null)
                    {
                        pontos.Add(pontosTripulacao[i]);
                    }
                }

                if (pontos.Count > 0)
                {
                    return;
                }
            }

            if (posicoesTripulacao == null || posicoesTripulacao.Length == 0)
            {
                posicoesTripulacao = new[]
                {
                    new Vector3(-0.85f, 1.05f, 0.55f),
                    new Vector3(0.85f, 1.05f, 0.55f),
                    new Vector3(-0.75f, 1.05f, -0.65f),
                    new Vector3(0.75f, 1.05f, -0.65f)
                };
            }

            while (pontos.Count < posicoesTripulacao.Length)
            {
                GameObject ponto = new GameObject("PontoTripulante_" + (pontos.Count + 1).ToString("00"));
                ponto.transform.SetParent(transform, false);
                pontos.Add(ponto.transform);
            }

            for (int i = 0; i < posicoesTripulacao.Length; i++)
            {
                Vector3 baseLocal = ObterPosicaoLocal(createBaseTripulacao, Vector3.zero);
                pontos[i].localPosition = baseLocal + posicoesTripulacao[i];
                pontos[i].localRotation = Quaternion.identity;
            }
        }

        private Vector3 ObterPosicaoLocal(CartelManualCreate create, Vector3 fallback)
        {
            if (create == null)
            {
                return fallback;
            }

            if (create.transform.parent == transform)
            {
                return create.transform.localPosition;
            }

            return transform.InverseTransformPoint(create.transform.position);
        }

        private int EscolherIndiceLivre(int indicePreferido)
        {
            if (indicePreferido >= 0 && indicePreferido < pontos.Count &&
                !tripulantes.Exists(item => item != null && item.ponto == pontos[indicePreferido]))
            {
                return indicePreferido;
            }

            for (int i = 0; i < pontos.Count; i++)
            {
                if (!tripulantes.Exists(item => item != null && item.ponto == pontos[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private void GarantirCargaVisual()
        {
            if (cargaVisual != null) return;

            if (prefabCargaVisual != null)
            {
                cargaVisual = Instantiate(prefabCargaVisual, transform);
                cargaVisual.name = "CargaPetroleoCartel";
                return;
            }

            if (!criarCargaVisualSeNaoHouverPrefab) return;

            cargaVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargaVisual.name = "CargaPetroleoCartel";
            cargaVisual.transform.SetParent(transform, false);

            Collider col = cargaVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer renderer = cargaVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.color = new Color(0.12f, 0.07f, 0.025f, 1f);
                    renderer.material = material;
                }
            }
        }

        private void AtualizarTransformacaoDaCarga()
        {
            if (cargaVisual == null || !cargaVisual.activeSelf) return;
            if (cargaVisual.transform.parent != transform)
            {
                cargaVisual.transform.SetParent(transform, false);
            }

            if (pontoCarga != null)
            {
                cargaVisual.transform.position = pontoCarga.position;
                cargaVisual.transform.rotation = pontoCarga.rotation;
            }
            else
            {
                cargaVisual.transform.localPosition = ObterPosicaoLocal(createCarga, posicaoCargaLocal);
                cargaVisual.transform.localRotation = Quaternion.identity;
            }
        }

        private void TocarParado(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            string[] estados = { "parado", "Idle", "idle", "Standing" };
            for (int i = 0; i < estados.Length; i++)
            {
                int hash = Animator.StringToHash(estados[i]);
                if (animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, 0f);
                    return;
                }
            }
        }

        private void LimparTripulantesInvalidos()
        {
            for (int i = tripulantes.Count - 1; i >= 0; i--)
            {
                if (tripulantes[i] == null || tripulantes[i].unidade == null)
                {
                    tripulantes.RemoveAt(i);
                }
            }
        }

        private void LiberarTripulante(TripulanteAncorado registro)
        {
            if (registro == null || registro.unidade == null) return;

            Transform unidadeTransform = registro.unidade.transform;
            unidadeTransform.SetParent(registro.paiOriginal, true);
            unidadeTransform.position = registro.posicaoMundoOriginal;
            unidadeTransform.rotation = registro.rotacaoMundoOriginal;
            unidadeTransform.localScale = registro.escalaLocalOriginal;

            if (registro.controle != null) registro.controle.enabled = registro.controleEstavaAtivo;
            if (registro.agente != null)
            {
                registro.agente.enabled = registro.agenteEstavaAtivo;
                if (registro.agenteEstavaAtivo) registro.agente.isStopped = false;
            }

            for (int i = 0; i < registro.rigidbodies.Length; i++)
            {
                if (registro.rigidbodies[i] == null) continue;
                registro.rigidbodies[i].isKinematic = registro.rigidbodiesKinematic[i];
                registro.rigidbodies[i].useGravity = registro.rigidbodiesGravity[i];
            }

            for (int i = 0; i < registro.colliders.Length; i++)
            {
                if (registro.colliders[i] != null) registro.colliders[i].enabled = registro.collidersAtivos[i];
            }

            if (registro.animator != null) registro.animator.applyRootMotion = registro.rootMotionEstavaAtivo;
        }
    }
}
