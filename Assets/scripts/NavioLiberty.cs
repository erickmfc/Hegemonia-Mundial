using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Configuração automática do USS Liberty Prime.
/// Este script NÃO controla movimento — isso já é feito por:
///   - ControleUnidade (seleção + clique direito)
///   - NavegacaoInteligenteNaval (rotação + marcha ré)
///   - TransporteAnfibio (embarque/desembarque)
///   - IdentidadeNaval (identidade do pier)
///
/// O que este script faz:
///   1. Configura Rigidbody (kinematic, sem gravidade, anti-capotamento)
///   2. Configura NavMeshAgent (updateRotation = false)
///   3. Auto-preenche IdentidadeNaval se necessário
///   4. Ativa efeitos visuais (fumaça)
/// </summary>
public class NavioLiberty : MonoBehaviour
{
    [Header("Efeitos Visuais")]
    public ParticleSystem fumacaChamine;

    // Referências internas
    private NavMeshAgent agente;
    private IdentidadeNaval identidade;

    void Awake()
    {
        agente     = GetComponent<NavMeshAgent>();
        identidade = GetComponent<IdentidadeNaval>();

        // ═══════════════════════════════════════════════════════════
        // 1. RIGIDBODY — Impede queda e capotamento
        // ═══════════════════════════════════════════════════════════
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.interpolation = RigidbodyInterpolation.None;
            // Permite girar no Y (virar proa), mas trava X e Z (sem capotamento)
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ
                           | RigidbodyConstraints.FreezePositionY;
        }

        // ═══════════════════════════════════════════════════════════
        // 2. NAVMESHAGENT — NavegacaoInteligenteNaval controla rotação
        // ═══════════════════════════════════════════════════════════
        if (agente != null)
        {
            // NavegacaoInteligenteNaval.Start() já faz updateRotation=false,
            // mas este Awake roda ANTES, então garantimos aqui também
            agente.updateRotation = false;
            agente.updatePosition = true;
        }

        // ═══════════════════════════════════════════════════════════
        // 3. COLLIDERS DOS FILHOS — Garante que não caem (Rigidbody filho)
        // ═══════════════════════════════════════════════════════════
        foreach (Rigidbody rbFilho in GetComponentsInChildren<Rigidbody>())
        {
            if (rbFilho == rb) continue; // Já configuramos o principal
            rbFilho.isKinematic = true;
            rbFilho.useGravity  = false;
        }
    }

    void Start()
    {
        // ═══════════════════════════════════════════════════════════
        // 4. IDENTIDADE NAVAL — Auto-preenche se vazio
        // ═══════════════════════════════════════════════════════════
        if (identidade != null)
        {
            if (string.IsNullOrEmpty(identidade.nomeDoNavio))
                identidade.nomeDoNavio = "USS Liberty Prime";

            identidade.categoriaNavio = IdentidadeNaval.CategoriaNavio.TransporteGrande;
        }

        // ═══════════════════════════════════════════════════════════
        // 5. EFEITOS VISUAIS — Fumaça das chaminés
        // ═══════════════════════════════════════════════════════════
        if (fumacaChamine == null)
        {
            foreach (var p in GetComponentsInChildren<ParticleSystem>())
            {
                string nome = p.name.ToLower();
                if (nome.Contains("fum") || nome.Contains("smoke") || nome.Contains("chamin"))
                {
                    fumacaChamine = p;
                    break;
                }
            }
        }

        if (fumacaChamine != null)
            fumacaChamine.Play();

        Debug.Log($"[Liberty] {identidade?.nomeDoNavio} pronto. RB: kinematic, NavAgent: updateRot=false");
    }
}
