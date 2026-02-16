using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI; // Necessário para a UI
using System.Collections;
using System.Collections.Generic;

public class Estaleiro : MonoBehaviour
{
    [System.Serializable]
    public class SlotConstrucao
    {
        public string nomeSlot = "Slot";
        public Transform pontoDeConstrucao; // Onde o navio fica sendo "montado"
        public bool estaOcupado = false;
        
        [HideInInspector] public GameObject visualAtual;
        [HideInInspector] public GameObject prefabAtual;
        [HideInInspector] public float progresso; // 0 a 100
        [HideInInspector] public Vector3 escalaOriginal; // Para lembrar o tamanho correto
        
        // UI de Progresso Interna
        [HideInInspector] public GameObject barCanvasObj;
        [HideInInspector] public Image barFillImage;
    }

    [Header("Estrutura e Vagas")]
    public Transform pontoDeSaida; // Para onde o navio vai depois de pronto
    public SlotConstrucao[] slots; // Configure 2 slots no Inspector

    [Header("Configuração de Construção")]
    public float tempoDeConstrucao = 5.0f; // Tempo em segundos para construir
    public bool usarAnimacaoEscala = true; // Se true, o navio cresce do chão (Scale Y)

    [Header("Visual da Barra de Progresso")]
    public GameObject prefabBarraProgresso; // Opcional: Prefab customizado
    public Vector3 offsetBarra = new Vector3(0, 10f, 0); // Altura da barra sobre o navio
    public Vector2 tamanhoBarra = new Vector2(4, 0.5f); // Tamanho se gerada via código

    [Header("Ajustes de Altura")]
    public bool forcarNivelDaAgua = true; 
    public float nivelDaAgua = 0f; 
    public float offsetAltura = 0f; 

    [Header("Efeitos Visuais")]
    public ParticleSystem efeitoConclusao; 
    
    void Start()
    {
        // Validação básica
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[Estaleiro] Nenhum slot de construção configurado!");
        }
    }

    void Update()
    {
        // Processa a construção em cada slot ocupado
        foreach (var slot in slots)
        {
            if (slot.estaOcupado && slot.visualAtual != null)
            {
                ProcessarConstrucao(slot);
            }
        }
    }

    public bool TemVaga
    {
        get { return ObterSlotLivre() != null; }
    }

    public bool ConstruirUnidade(GameObject prefabDoNavio)
    {
        SlotConstrucao slotLivre = ObterSlotLivre();

        if (slotLivre != null)
        {
            IniciarConstrucao(slotLivre, prefabDoNavio);
            return true;
        }
        else
        {
            Debug.LogWarning("[Estaleiro] Todos os slots estão ocupados!");
            return false;
        }
    }

    SlotConstrucao ObterSlotLivre()
    {
        if (slots == null) return null;
        foreach (var slot in slots)
        {
            if (!slot.estaOcupado) return slot;
        }
        return null;
    }

    void IniciarConstrucao(SlotConstrucao slot, GameObject prefab)
    {
        slot.estaOcupado = true;
        slot.prefabAtual = prefab;
        slot.progresso = 0f;

        // Calcula posição de nascimento
        Vector3 posFinal = slot.pontoDeConstrucao.position;
        if (forcarNivelDaAgua)
        {
            posFinal.y = nivelDaAgua + offsetAltura;
        }
        else
        {
            posFinal.y += offsetAltura;
        }

        // Instancia o visual
        GameObject novoNavio = Instantiate(prefab, posFinal, slot.pontoDeConstrucao.rotation);
        novoNavio.transform.SetParent(null); 

        // Salva a escala
        slot.escalaOriginal = novoNavio.transform.localScale;

        // Desativa lógica
        DesativarLogicaUnidade(novoNavio);

        slot.visualAtual = novoNavio;

        // Animação Escala Inicial
        if (usarAnimacaoEscala)
        {
            novoNavio.transform.localScale = new Vector3(slot.escalaOriginal.x, 0.001f, slot.escalaOriginal.z);
        }

        // --- CRIAR BARRA DE PROGRESSO ---
        CriarBarraProgresso(slot);

        Debug.Log($"[Estaleiro] Iniciando construção de {prefab.name} no {slot.nomeSlot}");
    }

    void CriarBarraProgresso(SlotConstrucao slot)
    {
        if (slot.visualAtual == null) return;

        GameObject canvasObj = new GameObject("CanvasBarra_" + slot.nomeSlot);
        canvasObj.transform.position = slot.pontoDeConstrucao.position + offsetBarra;
        canvasObj.transform.SetParent(this.transform); // Parente é o estaleiro para organização

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Configura tamanho do canvas (pequeno, apenas para a barra)
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(5, 1); // 5 metros por 1 metro
        rt.localScale = Vector3.one;

        // Fundo da Barra (Vermelho/Escuro)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0f, 0f, 0.8f);
        RectTransform rtBg = bgObj.GetComponent<RectTransform>();
        rtBg.anchorMin = Vector2.zero; rtBg.anchorMax = Vector2.one;
        rtBg.sizeDelta = Vector2.zero;

        // Fill da Barra (Verde)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(canvasObj.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = Color.green;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f; // Começa vazio
        
        RectTransform rtFill = fillObj.GetComponent<RectTransform>();
        rtFill.anchorMin = Vector2.zero; rtFill.anchorMax = Vector2.one;
        rtFill.sizeDelta = Vector2.zero;

        // LookAt Camera Script (Simples)
        canvasObj.AddComponent<OlharParaCamera>(); 

        slot.barCanvasObj = canvasObj;
        slot.barFillImage = fillImg;
    }

    void ProcessarConstrucao(SlotConstrucao slot)
    {
        // Incrementa progresso
        float incremento = (Time.deltaTime / tempoDeConstrucao) * 100f;
        slot.progresso += incremento;

        // Atualiza Visual (Escala)
        if (usarAnimacaoEscala)
        {
            float pct = Mathf.Clamp01(slot.progresso / 100f);
            float scaleY = Mathf.Lerp(0.001f, slot.escalaOriginal.y, pct);
            slot.visualAtual.transform.localScale = new Vector3(slot.escalaOriginal.x, scaleY, slot.escalaOriginal.z);
        }

        // Atualiza Barra de Progresso
        if (slot.barFillImage != null)
        {
            slot.barFillImage.fillAmount = slot.progresso / 100f;
        }

        // Verifica Conclusão
        if (slot.progresso >= 100f)
        {
            FinalizarConstrucao(slot);
        }
    }

    void FinalizarConstrucao(SlotConstrucao slot)
    {
        Debug.Log($"[Estaleiro] Construção finalizada no {slot.nomeSlot}!");

        GameObject navioPronto = slot.visualAtual; 

        // Restaura escala
        navioPronto.transform.localScale = slot.escalaOriginal;

        // Destroi a barra
        if (slot.barCanvasObj != null)
        {
            Destroy(slot.barCanvasObj);
            slot.barCanvasObj = null;
            slot.barFillImage = null;
        }

        ReativarLogicaUnidade(navioPronto);

        // Efeitos
        if (efeitoConclusao != null)
        {
            Instantiate(efeitoConclusao, slot.pontoDeConstrucao.position, Quaternion.identity);
        }

        // Libera o slot
        slot.estaOcupado = false;
        slot.visualAtual = null;
        slot.prefabAtual = null;
        slot.progresso = 0f;
    }

    void DesativarLogicaUnidade(GameObject unidade)
    {
        // Desativa NavMeshAgent
        var agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Desativa scripts
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) script.enabled = false;

        // Desativa Colliders
        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = false;
    }

    void ReativarLogicaUnidade(GameObject unidade)
    {
        // 1. Setup Básico
        unidade.layer = LayerMask.NameToLayer("Default");
        
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        if(identidade == null) identidade = unidade.AddComponent<IdentidadeUnidade>();
        identidade.teamID = 1; 
        identidade.nomeDoPais = "Hegemonia";

        // 2. Posição
        if(forcarNivelDaAgua)
        {
             Vector3 pos = unidade.transform.position;
             pos.y = nivelDaAgua;
             unidade.transform.position = pos;
        }

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.enabled = true;

            // Warp para garantir NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(unidade.transform.position, out hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            agent.updatePosition = true;
            agent.updateRotation = true;
            // IMPORTANTE: Reseta velocidades para evitar pulo
            agent.velocity = Vector3.zero;
            agent.isStopped = false;
        }

        // 3. Reativa Colliders
        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = true;

        // 4. Reativa Scripts (EXCETO NavMeshAgent que já foi)
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) 
        {
            if (!(script is UnityEngine.AI.NavMeshAgent)) 
            {
                script.enabled = true;
            }
        }
        
        // Garante ControleUnidade
        var ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl == null) ctrl = unidade.AddComponent<ControleUnidade>();
        ctrl.enabled = true;
    }

    void OnDrawGizmos()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot.pontoDeConstrucao != null)
            {
                Gizmos.color = slot.estaOcupado ? Color.red : Color.green;
                Gizmos.DrawWireCube(slot.pontoDeConstrucao.position, Vector3.one * 5f);
            }
        }
        
        if(pontoDeSaida != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(pontoDeSaida.position, 2f);
        }
    }
}
