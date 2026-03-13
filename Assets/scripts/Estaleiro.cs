using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI; // Necessário para a UI
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        [HideInInspector] public Text textProgresso;
    }

    [Header("Estrutura e Vagas")]
    public Transform pontoDeSaida; // Para onde o navio vai depois de pronto
    public SlotConstrucao[] slots; // Configure 2 slots no Inspector

    [Header("Configuração de Construção")]
    public float tempoDeConstrucao = 5.0f; // Tempo em segundos para construir
    public bool usarAnimacaoEscala = false; // DESATIVADO TEMPORARIAMENTE A PEDIDO

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
        GarantirSlotsExistentes();
        
        // Auto-cria ponto de saída se nulo
        if (pontoDeSaida == null)
        {
             GameObject goSaida = new GameObject("PontoDeSaida_Auto");
             goSaida.transform.SetParent(this.transform);
             goSaida.transform.position = transform.position + (transform.forward * (offsetAguaFrente + 40f));
             pontoDeSaida = goSaida.transform;
             Debug.Log("[Estaleiro] Ponto de saída não configurado, gerado automaticamente na água.");
        }

        RegistrarNoGerente();
    }

    void RegistrarNoGerente()
    {
        GerenteDeJogo gerente = GerenteDeJogo.Instancia;
        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();

        if (gerente != null)
        {
            // Pega identidade para saber se é do jogador
            IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
            if (id == null) id = GetComponentInParent<IdentidadeUnidade>();

            if (id == null || id.teamID == 1)
            {
                // Registra o primeiro slot como spawn se possível
                Transform spawn = (slots != null && slots.Length > 0) ? slots[0].pontoDeConstrucao : transform;
                gerente.AtualizarPontoEstaleiro(spawn, pontoDeSaida);
            }
        }
    }

    public void GarantirSlotsExistentes()
    {
        // Validação básica: auto-criação de slots se estiver nulo
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[Estaleiro] Nenhum slot de construção configurado no Inspector! Criando 3 slots básicos automaticamente.");
            slots = new SlotConstrucao[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject novoPonto = new GameObject($"Ponto_Auto_Estaleiro_{i}");
                novoPonto.transform.SetParent(this.transform);
                novoPonto.transform.position = transform.position + (transform.forward * offsetAguaFrente) + (transform.right * (i * 20f - 20f));
                
                slots[i] = new SlotConstrucao
                {
                    nomeSlot = (i == 0) ? "Atracagem_Grande" : $"Slot_{i}",
                    pontoDeConstrucao = novoPonto.transform,
                    estaOcupado = false
                };
            }
        }
    }

    void Update()
    {
        if (slots == null) return;
        // Processa a construção em cada slot ocupado
        foreach (var slot in slots)
        {
            if (slot.estaOcupado)
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
        GarantirSlotsExistentes();
        SlotConstrucao slotLivre = null;

        // REGRA ESPECÍFICA: Navios GRANDES devem procurar o slot "Atracagem_Grande"
        bool ehNavioGrande = prefabDoNavio.GetComponent<NavioPetroleiro>() != null
                          || prefabDoNavio.GetComponent<TransporteAnfibio>() != null
                          || prefabDoNavio.GetComponent<NavioLiberty>() != null;
        
        if (ehNavioGrande)
        {
            slotLivre = ObterSlotEspecificoLivre("Atracagem_Grande");
            if (slotLivre == null)
            {
                Debug.LogWarning("[Estaleiro] Navio grande requer 'Atracagem_Grande', mas está ocupada ou não existe. Tentando outros slots...");
            }
        }

        // Se não for petroleiro ou se o slot preferido estiver ocupado, busca qualquer um
        if (slotLivre == null)
        {
            slotLivre = ObterSlotLivre();
        }

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

    SlotConstrucao ObterSlotEspecificoLivre(string nomeAlvo)
    {
        if (slots == null) return null;
        foreach (var slot in slots)
        {
            // Verifica o nome configurado no Inspector ou o nome do objeto Transform
            bool nomeBate = (slot.nomeSlot == nomeAlvo) || 
                            (slot.pontoDeConstrucao != null && slot.pontoDeConstrucao.name == nomeAlvo);

            if (nomeBate && !slot.estaOcupado)
            {
                return slot;
            }
        }
        return null;
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

        // --- CRIAR BARRA DE PROGRESSO ---
        CriarBarraProgresso(slot);

        Debug.Log($"[Estaleiro] Iniciando construção de {prefab.name} no {slot.nomeSlot}. Aguardando conclusão...");
    }

    void CriarBarraProgresso(SlotConstrucao slot)
    {
        if (slot.prefabAtual == null) return;

        GameObject canvasObj = new GameObject("CanvasBarra_" + slot.nomeSlot);
        canvasObj.transform.position = slot.pontoDeConstrucao.position + offsetBarra;
        canvasObj.transform.SetParent(this.transform); // Parente é o estaleiro para organização

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 150); 
        rt.localScale = new Vector3(0.003f, 0.003f, 0.003f); // 70% menor

        // Texto Informativo com Porcentagem
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(canvasObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = "PREPARANDO... 0%";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // CORRIGIDO: Arial.ttf removido
        txt.fontSize = 80; 
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0f, 0.8f, 1f, 1f); // Azul Neon no texto
        
        Shadow ts = txtObj.AddComponent<Shadow>();
        ts.effectColor = Color.black; 
        ts.effectDistance = new Vector2(4, -4);

        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
        rtTxt.sizeDelta = Vector2.zero;

        // Texto plano sem seguir a câmera, com redução e virado 180 graus
        canvasObj.transform.localRotation = Quaternion.Euler(90f, 0f, 180f);

        slot.barCanvasObj = canvasObj;
        slot.barFillImage = null; // Removido linhas visíveis
        slot.textProgresso = txt;
    }

    void ProcessarConstrucao(SlotConstrucao slot)
    {
        // Incrementa progresso
        float incremento = (Time.deltaTime / tempoDeConstrucao) * 100f;
        slot.progresso += incremento;

        // Atualiza Barra de Progresso
        if (slot.barFillImage != null)
        {
            slot.barFillImage.fillAmount = slot.progresso / 100f;
        }
        if (slot.textProgresso != null)
        {
            slot.textProgresso.text = $"PREPARANDO... {Mathf.FloorToInt(slot.progresso)}%";
        }

        // Verifica Conclusão
        if (slot.progresso >= 100f)
        {
            FinalizarConstrucao(slot);
        }
    }

    void FinalizarConstrucao(SlotConstrucao slot)
    {
        Debug.Log($"[Estaleiro] Construção finalizada no {slot.nomeSlot}! Nascendo 100% puro.");

        // Calcula posição de nascimento exata
        Vector3 posFinal = slot.pontoDeConstrucao.position;
        if (forcarNivelDaAgua) posFinal.y = nivelDaAgua + offsetAltura;
        else posFinal.y += offsetAltura;

        // 1. INSTANCIA O PREFAB CRU E INTACTO!
        GameObject navioPronto = Instantiate(slot.prefabAtual, posFinal, slot.pontoDeConstrucao.rotation);
        navioPronto.transform.SetParent(null); 

        // Destroi a barra
        if (slot.barCanvasObj != null)
        {
            Destroy(slot.barCanvasObj);
            slot.barCanvasObj = null;
            slot.barFillImage = null;
        }

        // --- LÓGICA DE IDENTIDADE (Dinâmica) ---
        navioPronto.layer = LayerMask.NameToLayer("Default");
        IdentidadeUnidade idEstaleiro = GetComponentInParent<IdentidadeUnidade>();
        IdentidadeUnidade idNavio = navioPronto.GetComponent<IdentidadeUnidade>();
        if(idNavio == null) idNavio = navioPronto.AddComponent<IdentidadeUnidade>();
        
        if (idEstaleiro != null)
        {
            idNavio.teamID = idEstaleiro.teamID;
            idNavio.nomeDoPais = idEstaleiro.nomeDoPais;
        }
        else
        {
            idNavio.teamID = 1; 
            idNavio.nomeDoPais = "Hegemonia";
        }

        var ctrl = navioPronto.GetComponent<ControleUnidade>();
        if (ctrl == null) navioPronto.AddComponent<ControleUnidade>();

        // Ordena que o navio vá para o ponto de saída automaticamente
        var agenteNovo = navioPronto.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agenteNovo != null && pontoDeSaida != null)
        {
            // Garante que o NavMesh dele faça um Warp inicial seguro
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(navioPronto.transform.position, out hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agenteNovo.Warp(hit.position);
            }
        
            var navRealista = navioPronto.GetComponent<NavegacaoInteligenteNaval>();
            if (navRealista != null) navRealista.DefinirDestino(pontoDeSaida.position);
            else agenteNovo.SetDestination(pontoDeSaida.position);
        }

        // Registrar no General se for IA
        if (idNavio.teamID != 1)
        {
            var commanders = Object.FindObjectsByType<IA_Comandante>(FindObjectsSortMode.None);
            var myCommander = commanders.FirstOrDefault(c => c.identidade != null && c.identidade.teamID == idNavio.teamID);
            if (myCommander != null && myCommander.cerebroGeneral != null)
            {
                myCommander.cerebroGeneral.RegistrarUnidade(navioPronto);
            }
        }

        // --- LÓGICA ESPECÍFICA PARA PETROLEIRO ---
        NavioPetroleiro petroleiro = navioPronto.GetComponent<NavioPetroleiro>();
        if (petroleiro != null && pontoDeSaida != null)
        {
            petroleiro.DefinirSaidaEstaleiro(pontoDeSaida.position);
        }
        // ----------------------------------------

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

    [Header("Indicadores Litorâneos (Terra/Água)")]
    public float offsetAguaFrente = 35f; 
    public float offsetTerraTras = -15f; 

    public bool EstaNaConstrucaoValida(float nivelAgua = 0f)
    {
        Vector3 posFrente = transform.position + transform.forward * offsetAguaFrente;
        Vector3 posTras = transform.position + transform.forward * offsetTerraTras;

        float hFrente = 0f;
        float hTras = 0f;
        
        if(Terrain.activeTerrain != null)
        {
            hFrente = Terrain.activeTerrain.SampleHeight(posFrente);
            hTras = Terrain.activeTerrain.SampleHeight(posTras);
        }

        // Deve prever a frente perto d'água e traseira em solo alto
        return (hFrente <= nivelAgua + 1f) && (hTras > nivelAgua);
    }

    void OnDrawGizmos()
    {
        // GIZMO DE COLOCAÇÃO CORRETA (Frente Azul = Água, Atrás Marrom = Terra)
        Vector3 posAgua = transform.position + transform.forward * offsetAguaFrente;
        Vector3 posTerra = transform.position + transform.forward * offsetTerraTras;

        Gizmos.color = new Color(0f, 0.4f, 1f, 0.7f); // AZUL = ÁGUA
        Gizmos.DrawSphere(posAgua, 3.5f);
        Gizmos.DrawLine(posAgua, transform.position);

        Gizmos.color = new Color(0.6f, 0.3f, 0f, 0.7f); // MARROM = TERRA FIRME
        Gizmos.DrawSphere(posTerra, 3.5f);
        Gizmos.DrawLine(transform.position, posTerra);
        
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
