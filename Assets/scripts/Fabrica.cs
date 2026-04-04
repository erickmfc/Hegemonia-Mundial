using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Fabrica : MonoBehaviour
{
    [Header("Tipo de Fábrica")]
    public bool ehQuartel; // Marque TRUE se for Tenda/Soldado. Desmarque se for Hangar/Tanque.

    [Header("Pontos de Spawn (Arraste aqui os filhos)")]
    public Transform pontoNascimento;
    public Transform pontoSaida;

    [Header("Múltiplos Pontos de Saída (Opcional)")]
    [Tooltip("Se esta lista estiver vazia, o script buscará automaticamente por filhos chamados 'Ponto_Saida'.")]
    public List<Transform> pontosSaidaExtras = new List<Transform>();
    private int indiceSaidaGlobal = 0;

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void Start()
    {
        // Busca automática de pontos de saída se não houver nenhum configurado
        if (pontosSaidaExtras == null || pontosSaidaExtras.Count == 0)
        {
            pontosSaidaExtras = new List<Transform>();
            foreach (Transform filho in transform)
            {
                if (filho.name.Contains("Ponto_Saida"))
                    pontosSaidaExtras.Add(filho);
            }
        }

        // Garante que o ponto principal esteja incluído se existir
        if (pontoSaida != null && !pontosSaidaExtras.Contains(pontoSaida))
            pontosSaidaExtras.Insert(0, pontoSaida);

        // Registro no Gerente de Jogo (Apenas Time 1)
        var idComp = GetComponentInParent<IdentidadeUnidade>();
        if (idComp != null && idComp.teamID != 1) return; 
        StartCoroutine(RegistrarNoGerente(0.1f));
    }

    IEnumerator RegistrarNoGerente(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Pega o gerente principal do jogo
        GerenteDeJogo gerente = FindFirstObjectByType<GerenteDeJogo>(); 
        string meuNome = gameObject.name.ToLower();
        
        // AQUI MANTIVEMOS SUA LÓGICA NAVAL INTACTA! NADA FOI APAGADO.
        if (meuNome.Contains("naval") || meuNome.Contains("navio") || meuNome.Contains("estaleiro") || meuNome.Contains("pier")) yield break;

        if(meuNome.Contains("hangar")) ehQuartel = false;
        if(meuNome.Contains("tenda") || meuNome.Contains("quartel")) ehQuartel = true;

        if (gerente != null)
        {
            if (ehQuartel) gerente.AtualizarPontoQuartel(pontoNascimento, pontoSaida);
            else gerente.AtualizarPontoHangar(pontoNascimento, pontoSaida);
        }
    }

    private static Dictionary<Transform, int> _contadorSlot = new Dictionary<Transform, int>();

    public GameObject ProduzirUnidade(GameObject prefab)
    {
        if (prefab == null) return null;

        Transform spawn = (pontoNascimento != null) ? pontoNascimento : transform;
        
        // Escolhe o próximo ponto de saída (Round Robin entre os 5 disponíveis)
        Transform saidaAlvo = pontoSaida;
        if (pontosSaidaExtras != null && pontosSaidaExtras.Count > 0)
        {
            saidaAlvo = pontosSaidaExtras[indiceSaidaGlobal % pontosSaidaExtras.Count];
            indiceSaidaGlobal++;
        }

        if (saidaAlvo == null) saidaAlvo = transform;

        // Calcula slot na fila para o ponto escolhido
        if (!_contadorSlot.ContainsKey(saidaAlvo)) _contadorSlot[saidaAlvo] = 0;
        _contadorSlot[saidaAlvo]++;
        int slotIdx = _contadorSlot[saidaAlvo] - 1;

        float espacamento = CalcularEspacamentoSaida(prefab);
        Vector3 posSlot = saidaAlvo.position + (saidaAlvo.forward * (5f + slotIdx * espacamento));

        // Validação NavMesh para Spawn
        Vector3 posSpawnFinal = spawn.position;
        UnityEngine.AI.NavMeshHit nh;
        if (UnityEngine.AI.NavMesh.SamplePosition(spawn.position, out nh, 10f, UnityEngine.AI.NavMesh.AllAreas))
            posSpawnFinal = nh.position;

        // Instancia no spawn interno
        GameObject unidade = Instantiate(prefab, posSpawnFinal, spawn.rotation);

        // Identidade
        var idF = GetComponentInParent<IdentidadeUnidade>();
        var idU = unidade.GetComponent<IdentidadeUnidade>();
        if (idF != null && idU != null) { idU.teamID = idF.teamID; idU.nomeDoPais = idF.nomeDoPais; }

        // EXCLUSIVO: Corotina para delay de 1 segundo antes de sair
        StartCoroutine(MoverParaSaidaComDelay(unidade, posSlot, 1.0f));

        // Registro IA
        if (idU != null && idU.teamID != 1)
        {
            var myCommander = IA_ComandanteRegistry.GetCommanderByTeam(idU.teamID);
            if (myCommander != null && myCommander.cerebroGeneral != null) myCommander.cerebroGeneral.RegistrarUnidade(unidade);
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
        }

        return unidade;
    }

    IEnumerator MoverParaSaidaComDelay(GameObject unidade, Vector3 destino, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (unidade != null)
        {
            var controle = unidade.GetComponent<ControleUnidade>();
            if (controle != null)
                controle.MoverParaPonto(destino);
            else
            {
                var nav = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null && nav.isOnNavMesh) nav.SetDestination(destino);
            }
        }
    }

    float CalcularEspacamentoSaida(GameObject prefab)
    {
        if (prefab == null) return 8f;

        float espacamento = 8f;

        var agent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            espacamento = Mathf.Max(espacamento, (agent.radius * 2.4f) + 1.5f);
        }

        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        bool temBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var c in colliders)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;

            if (!temBounds)
            {
                bounds = c.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        if (temBounds)
        {
            float maiorLado = Mathf.Max(bounds.size.x, bounds.size.z);
            espacamento = Mathf.Max(espacamento, (maiorLado * 1.35f) + 1.0f);
        }

        return Mathf.Clamp(espacamento, 6f, 30f);
    }
}
