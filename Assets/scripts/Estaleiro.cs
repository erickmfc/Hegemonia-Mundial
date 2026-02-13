using UnityEngine;
using UnityEngine.AI;
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
    }

    [Header("Estrutura e Vagas")]
    public Transform pontoDeSaida; // Para onde o navio vai depois de pronto
    public SlotConstrucao[] slots; // Configure 2 slots no Inspector

    [Header("Configuração de Construção")]
    public float tempoDeConstrucao = 5.0f; // Tempo em segundos para construir
    public bool usarAnimacaoEscala = true; // Se true, o navio cresce do chão (Scale Y)

    [Header("Ajustes de Altura (Correção de Spawn)")]
    public bool forcarNivelDaAgua = true; // Se true, ignora a altura do slot e usa o Y abaixo
    public float nivelDaAgua = 0f; // Altura da água (geralmente 0 ou a altura do estaleiro)
    public float offsetAltura = 0f; // Ajuste fino extra se o navio tiver pivot errado

    [Header("Efeitos Visuais")]
    public ParticleSystem efeitoConclusao; // Opcional: Efeito ao terminar
    
    // Singleton simples para facilitar acesso se houver apenas um, 
    // mas o menu busca por FindFirstObjectByType, então ok.
    
    void Start()
    {
        // Validação básica
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[Estaleiro] Nenhum slot de construção configurado!");
        }
        else
        {
            // Validação de segurança: Verifica se os slots não estão encavalados
            for (int i = 0; i < slots.Length; i++)
            {
                for (int j = i + 1; j < slots.Length; j++)
                {
                    if (slots[i].pontoDeConstrucao != null && slots[j].pontoDeConstrucao != null)
                    {
                        if (slots[i].pontoDeConstrucao == slots[j].pontoDeConstrucao)
                        {
                            Debug.LogError($"[Estaleiro] ERRO DE CONFIGURAÇÃO: O Slot '{slots[i].nomeSlot}' e o Slot '{slots[j].nomeSlot}' estão usando o MESMO objeto (Transform)! Os navios nascerão um dentro do outro. Use objetos diferentes na cena.");
                        }
                        else if (Vector3.Distance(slots[i].pontoDeConstrucao.position, slots[j].pontoDeConstrucao.position) < 2.0f)
                        {
                            Debug.LogWarning($"[Estaleiro] CUIDADO: Os slots '{slots[i].nomeSlot}' e '{slots[j].nomeSlot}' estão muito próximos (<2m). Pode haver sobreposição visual.");
                        }
                    }
                }
            }
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

        // Instancia o visual no ponto de construção ajustado
        GameObject novoNavio = Instantiate(prefab, posFinal, slot.pontoDeConstrucao.rotation);
        
        // IMPORTANTE: Não definir parente como o Estaleiro se o Estaleiro tiver escala distorcida!
        // Deixamos sem parente (na raiz da cena) para preservar a escala original do prefab.
        novoNavio.transform.SetParent(null); 

        // Salva a escala original do prefab para restaurar depois
        slot.escalaOriginal = novoNavio.transform.localScale;

        // Desativa componentes de lógica (NavMeshAgent, Scripts de ataque, etc)
        DesativarLogicaUnidade(novoNavio);

        slot.visualAtual = novoNavio;

        // Configuração inicial da animação
        if (usarAnimacaoEscala)
        {
            // Começa invisível no eixo Y (achatado no chão)
            novoNavio.transform.localScale = new Vector3(slot.escalaOriginal.x, 0.001f, slot.escalaOriginal.z);
        }

        Debug.Log($"[Estaleiro] Iniciando construção de {prefab.name} no {slot.nomeSlot}");
    }

    void ProcessarConstrucao(SlotConstrucao slot)
    {
        // Incrementa progresso
        float incremento = (Time.deltaTime / tempoDeConstrucao) * 100f;
        slot.progresso += incremento;

        // Atualiza Visual (Animação "Montando")
        if (usarAnimacaoEscala)
        {
            // Lerp da escala Y: de 0 até a escala original Y
            float pct = Mathf.Clamp01(slot.progresso / 100f);
            
            float scaleY = Mathf.Lerp(0.001f, slot.escalaOriginal.y, pct);
            
            slot.visualAtual.transform.localScale = new Vector3(slot.escalaOriginal.x, scaleY, slot.escalaOriginal.z);
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

        // Restaura escala final exata para garantir
        navioPronto.transform.localScale = slot.escalaOriginal;

        // Mover para a saída
        StartCoroutine(MoverParaSaida(navioPronto));

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
        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = true;

        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) script.enabled = true;
        
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        if(identidade == null) identidade = unidade.AddComponent<IdentidadeUnidade>();
        identidade.teamID = 1; 
        identidade.nomeDoPais = "Hegemonia";

        var agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = true;
    }

    IEnumerator MoverParaSaida(GameObject navio)
    {
        // 1. Garante que o NavMeshAgent esteja ativo para validação, mas os Colisores ainda OFF
        var agent = navio.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            
            // Força a altura do agente para o nível da água para garantir que ele ache o NavMesh
            if(forcarNivelDaAgua)
            {
                Vector3 posAjustada = navio.transform.position;
                posAjustada.y = nivelDaAgua; // Coloca o agente no nível 0 (NavMesh)
                navio.transform.position = posAjustada;
            }
        }

        yield return null; // Espera um frame para o Agente se registrar no NavMesh

        // 2. Posiciona corretamente no NavMesh (Warp)
        if (agent != null && pontoDeSaida != null)
        {
            NavMeshHit hit;
            // Busca num raio generoso (50f)
            if (NavMesh.SamplePosition(navio.transform.position, out hit, 50.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                agent.SetDestination(pontoDeSaida.position);
            }
            else
            {
                if (NavMesh.SamplePosition(pontoDeSaida.position, out hit, 20.0f, NavMesh.AllAreas))
                 {
                     agent.Warp(hit.position);
                     agent.SetDestination(pontoDeSaida.position);
                 }
            }
        }

        ReativarLogicaUnidade(navio);
    }

    void OnDrawGizmos()
    {
        // Gizmos helpers...
        if (slots != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var slot in slots)
            {
                if (slot.pontoDeConstrucao != null)
                {
                    Gizmos.DrawWireCube(slot.pontoDeConstrucao.position, new Vector3(5, 1, 15));
                    Gizmos.DrawLine(slot.pontoDeConstrucao.position, slot.pontoDeConstrucao.position + Vector3.up * 5);
                }
            }
            if(pontoDeSaida != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pontoDeSaida.position, 2f);
                
                // Draw lines to exit
                foreach (var slot in slots)
                {
                    if (slot.pontoDeConstrucao != null)
                        Gizmos.DrawLine(slot.pontoDeConstrucao.position, pontoDeSaida.position);
                }
            }
        }
    }

    void OnGUI()
    {
        if (Camera.main == null) return;

        foreach (var slot in slots)
        {
            if (slot.estaOcupado && slot.visualAtual != null)
            {
                // Pega posição do navio na tela
                Vector3 posMundo = slot.visualAtual.transform.position + Vector3.up * 8f; 
                Vector3 screenPos = Camera.main.WorldToScreenPoint(posMundo);
                
                // Se estiver atrás da câmera, ignora
                if (screenPos.z < 0) continue;

                float y = Screen.height - screenPos.y - 60f; // Ajuste de altura
                float boxWidth = 160f;
                float boxHeight = 40f;
                Rect boxRect = new Rect(screenPos.x - boxWidth/2, y, boxWidth, boxHeight);
                
                // Fundo semi-transparente
                Color oldColor = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.7f); 
                GUI.DrawTexture(boxRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = oldColor;

                // Texto de Status
                GUIStyle styleStatus = new GUIStyle(GUI.skin.label);
                styleStatus.alignment = TextAnchor.MiddleCenter;
                styleStatus.fontStyle = FontStyle.Bold;
                styleStatus.fontSize = 12; 
                styleStatus.normal.textColor = Color.cyan;

                string texto = $"CONSTRUINDO: {slot.progresso:F0}%";
                GUI.Label(boxRect, texto, styleStatus);
            }
        }
    }
}
