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

        // --- MUDANÇA: NÃO MOVE MAIS SOZINHO ---
        // O navio fica parado no slot, sob controle total do jogador.
        // StartCoroutine(MoverParaSaida(navioPronto)); 
        
        // Ativa a lógica imediatamente no local de nascimento
        ReativarLogicaUnidade(navioPronto);

        // Efeitos
        if (efeitoConclusao != null)
        {
            Instantiate(efeitoConclusao, slot.pontoDeConstrucao.position, Quaternion.identity);
        }

        // Libera o slot visualmente, mas o navio físico ainda está lá (cuidado com sobreposição se construir outro rápido!)
        slot.estaOcupado = false;
        slot.visualAtual = null;
        slot.prefabAtual = null;
        slot.progresso = 0f;
    }

    // A rotina MoverParaSaida foi removida/desativada para dar controle total ao jogador.
    /*
    IEnumerator MoverParaSaida(GameObject navio)
    {
        ... (código antigo removido para limpeza) ...
    }
    */

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
        Debug.Log($"[Estaleiro] Reativando unidade {unidade.name}...");

        // 1. Configurar Identidade e Camadas (Dados estáticos)
        unidade.layer = LayerMask.NameToLayer("Default");
        
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        if(identidade == null) identidade = unidade.AddComponent<IdentidadeUnidade>();
        identidade.teamID = 1; 
        identidade.nomeDoPais = "Hegemonia";

        // 2. Posicionamento e NavMesh (Dados físicos)
        // Se tiver forçando nível da água, já ajusta a altura ANTES de ligar qualquer coisa
        if(forcarNivelDaAgua)
        {
             Vector3 pos = unidade.transform.position;
             pos.y = nivelDaAgua;
             unidade.transform.position = pos;
        }

        var agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null) 
        {
            // O agente precisa estar habilitado para aceitar Warp, mas evitamos updatePosition imediato se possível
            agent.enabled = true;

            // Verifica se tem NavMeshAgent (Navio com NavMesh)
            var navMeshAgent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navMeshAgent != null)
            {
                // Busca ponto válido
                NavMeshHit hit;
                if (NavMesh.SamplePosition(unidade.transform.position, out hit, 20f, NavMesh.AllAreas))
                {
                    navMeshAgent.Warp(hit.position);
                }
            }

            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        // 3. Colisores (Física de interação)
        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = true;

        // 4. Scripts (Lógica - Ligar por último para que encontrem tudo pronto no OnEnable/Start)
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) 
        {
            // Não reativamos o NavMeshAgent aqui de novo (já foi tratado)
            if (!(script is NavMeshAgent)) 
            {
                script.enabled = true;
            }
        }
        
        // Garante ControleUnidade presente e ativo
        var ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl == null) ctrl = unidade.AddComponent<ControleUnidade>();
        ctrl.enabled = true;

        Debug.Log($"[Estaleiro] Unidade {unidade.name} ativada, posicionada e scripts ligados.");
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
            Gizmos.DrawLine(pontoDeSaida.position, pontoDeSaida.position + pontoDeSaida.forward * 10f);
        }
    }

}
