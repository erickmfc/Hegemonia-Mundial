using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class TransporteTerrestre : MonoBehaviour
{
    [Header("Configurações de Transporte")]
    public int capacidadeMaxima = 4;
    public float distanciaParaEmbarque = 10f; // Raio para puxar soldados
    public float distanciaDescarga = 3.0f;    // Distância exata pedida (3 metros)

    [Header("Assentos Visíveis (Atiradores)")]
    [Tooltip("Arraste os Transforms 'Lugar' aqui. Soldados nestes pontos continuarão ativos e visíveis.")]
    public Transform[] assentosVisiveis;

    // Estado Interno
    private List<GameObject> soldadosInternos = new List<GameObject>(); // Os que ficam escondidos/desativados
    private GameObject[] soldadosNosAssentos; // Os que ficam visíveis

    private bool selecionado = false;

    void Start()
    {
        // Inicializa array de assentos baseados nos slots disponíveis
        if (assentosVisiveis != null)
        {
            soldadosNosAssentos = new GameObject[assentosVisiveis.Length];
        }
        else
        {
            soldadosNosAssentos = new GameObject[0];
        }

        // Tenta auto-configurar se esqueceu de arrastar
        if (assentosVisiveis == null || assentosVisiveis.Length == 0)
        {
            var lugares = new List<Transform>();
            foreach(Transform child in transform)
            {
                if(child.name.Contains("Lugar")) lugares.Add(child);
            }
            if (lugares.Count > 0)
            {
                assentosVisiveis = lugares.ToArray();
                soldadosNosAssentos = new GameObject[assentosVisiveis.Length];
            }
        }
    }

    void Update()
    {
        // VerificarSelecao(); // REMOVIDO: Usar sistema padrão abaixo
        
        // Integração com ControleUnidade (Padrão do Projeto)
        var ctrl = GetComponent<ControleUnidade>();
        if(ctrl != null) selecionado = ctrl.selecionado;

        if (selecionado)
        {
            // O -> OPEN / Entrar
            if (Input.GetKeyDown(KeyCode.O)) 
            {
                TentarEmbarcar();
            }
            // U -> UNLOAD / Sair
            if (Input.GetKeyDown(KeyCode.U))
            {
                DesembarcarTudo();
            }
        }
    }

    void VerificarSelecao()
    {
        // Método obsoleto substituído por ControleUnidade.selecionado
    }

    public void TentarEmbarcar()
    {
        // Conta total atual
        int totalEmbarcados = soldadosInternos.Count + ContarVisiveis();
        if (totalEmbarcados >= capacidadeMaxima)
        {
            Debug.Log("Transporte Cheio!");
            return;
        }

        Debug.Log($"[{gameObject.name}] Tentando embarcar soldados... (Raio: {distanciaParaEmbarque}m)");

        // --- BUSCA GLOBAL POR PROXIMIDADE (Sem depender de Tags) ---
        // Pega tudo que tem collider em volta (Camadas Default, Player, etc)
        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaParaEmbarque);
        
        foreach (var hit in hits)
        {
            if (totalEmbarcados >= capacidadeMaxima) break;

            GameObject soldado = hit.gameObject;

            // Ignora a mim mesmo e meus filhos/partes
            if (soldado.transform.root == transform.root) continue;
            
            // Pega a raiz do objeto (caso o hit seja no pé ou braço)
            soldado = soldado.transform.root.gameObject;

            if (!soldado.activeInHierarchy) continue; 
            
            // Evita adicionar o mesmo objeto duas vezes (OverlapSphere pode pegar vários colliders do mesmo soldado)
            if (JaEstaEmbarcado(soldado)) continue;

             // --- FILTRAGEM ---

            // 1. Ignora Veículos (Verifica se TEM o script de transporte)
            if (soldado.GetComponent<TransporteTerrestre>() != null) continue;
            
            // 2. Ignora NAVIOS (Por Nome - Fallback de Segurança)
            string nomeLower = soldado.name.ToLower();
            if (nomeLower.Contains("uss") || nomeLower.Contains("corveta") || 
                nomeLower.Contains("fragata") || nomeLower.Contains("destroyer") ||
                nomeLower.Contains("navio") || nomeLower.Contains("ship") ||
                nomeLower.Contains("mako") || nomeLower.Contains("ironclad") ||
                nomeLower.Contains("liberty") || nomeLower.Contains("leviathan") ||
                nomeLower.Contains("sovereign"))
            {
                Debug.LogWarning($"⚠️ [TransporteTerrestre] BLOQUEADO: {soldado.name} parece ser um navio!");
                continue;
            }

            // 2. Identifica se é Biológico (Pessoa) vs Máquina
            SistemaDeDanos sd = soldado.GetComponent<SistemaDeDanos>();
            bool ehBiologico = false;
            
            if (sd != null) 
            {
                ehBiologico = sd.unidadeBiologica;
            }
            else
            {
                // Fallback visual/físico
                var nav = soldado.GetComponent<NavMeshAgent>();
                // Soldados têm raio pequeno (< 0.8), Tanques têm > 1.5
                if(nav != null && nav.radius < 0.8f) ehBiologico = true;
                
                // Fallback nominal
                string nome = soldado.name.ToLower();
                if(nome.Contains("soldado") || nome.Contains("soldier") || nome.Contains("infant") || nome.Contains("person")) ehBiologico = true;
            }

            // SÓ EMBARCA SE: For Biológico
            if (ehBiologico)
            {
                Debug.Log($"Embarcando: {soldado.name}");
                EmbarcarUnidade(soldado);
                totalEmbarcados++;
            }
        }
    }

    bool JaEstaEmbarcado(GameObject obj)
    {
        if (soldadosInternos.Contains(obj)) return true;
        foreach(var s in soldadosNosAssentos) if(s == obj) return true;
        return false;
    }

    void EmbarcarUnidade(GameObject soldado)
    {
        // 1. Tenta colocar num assento visível primeiro
        for (int i = 0; i < assentosVisiveis.Length; i++)
        {
            if (soldadosNosAssentos[i] == null)
            {
                // Coloca no assento
                soldadosNosAssentos[i] = soldado;
                
                // Lógica de fixação
                DesabilitarMovimento(soldado);
                
                soldado.transform.SetParent(assentosVisiveis[i]);
                soldado.transform.localPosition = Vector3.zero;
                soldado.transform.localRotation = Quaternion.identity;
                
                Debug.Log($"Soldado {soldado.name} ocupou o assento visível {i}.");
                return;
            }
        }

        // 2. Se não tem assento, vai para o compartimento interno (escondido)
        soldadosInternos.Add(soldado);
        soldado.SetActive(false); // Some com ele
        soldado.transform.SetParent(transform); // Leva junto como filho para não perder
        Debug.Log($"Soldado {soldado.name} embarcou no interior.");
    }

    void DesembarcarTudo()
    {
        // 1. Desembarca os Visíveis
        for (int i = 0; i < soldadosNosAssentos.Length; i++)
        {
            if (soldadosNosAssentos[i] != null)
            {
                EjetarUnidade(soldadosNosAssentos[i]);
                soldadosNosAssentos[i] = null;
            }
        }

        // 2. Desembarca os Internos
        // Nota: Iteramos de trás para frente para poder remover da lista
        for (int i = soldadosInternos.Count - 1; i >= 0; i--)
        {
            GameObject s = soldadosInternos[i];
            if (s != null)
            {
                s.SetActive(true); // Reaparece
                EjetarUnidade(s);
            }
            soldadosInternos.RemoveAt(i);
        }
    }

    void EjetarUnidade(GameObject unidade)
    {
        unidade.transform.SetParent(null); // Solta do carro
        
        // --- CÁLCULO DA POSIÇÃO DE SAÍDA ---
        // Pega uma posição aleatória num círculo de raio "distanciaDescarga"
        // Ou fixo nas laterais? O user pediu "saiam 3 metros do carro".
        
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0; // Mantém no plano
        direcaoAleatoria.Normalize();

        Vector3 pontoAlvo = transform.position + (direcaoAleatoria * distanciaDescarga);

        // Garante que seja no NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pontoAlvo, out hit, 2.0f, NavMesh.AllAreas))
        {
            unidade.transform.position = hit.position;
            HabilitarMovimento(unidade, hit.position);
        }
        else
        {
            // Se falhar o NavMesh, bota na posição bruta mesmo definindo Y do chão
            pontoAlvo.y = transform.position.y; 
            unidade.transform.position = pontoAlvo;
            HabilitarMovimento(unidade, pontoAlvo);
        }
    }

    // --- Utilitários de Controle de Agente ---

    void DesabilitarMovimento(GameObject unidade)
    {
        NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false; // Desliga o NavMeshAgent
        }
        
        Rigidbody rb = unidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Trava física
        }

        // DESLIGA O CONTROLE PARA NÃO SER SELECIONADO
        ControleUnidade ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
             ctrl.DefinirSelecao(false); // Garante que deseleciona visualmente antes
             ctrl.enabled = false; 
        }

        // DESLIGA O COLLIDER PARA CLIQUE PASSAR DIRETO PRO CARRO
        // (Nota: Isso impede que levem tiros diretos. Se quiser que levem tiro, teria que tratar no GerenteSelecao)
        Collider[] cols = unidade.GetComponentsInChildren<Collider>();
        foreach(var c in cols) c.enabled = false;
    }

    void HabilitarMovimento(GameObject unidade, Vector3 posicaoInicial)
    {
        // REATIVA COLLIDERS
        Collider[] cols = unidade.GetComponentsInChildren<Collider>();
        foreach(var c in cols) c.enabled = true;

        // REATIVA CONTROLE
        ControleUnidade ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl != null) ctrl.enabled = true;

        NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(posicaoInicial); // Reseta o agente na nova posição
            agent.enabled = true;
            agent.SetDestination(posicaoInicial); // Manda ficar parado onde caiu
        }
         
        Rigidbody rb = unidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // (Ou true se usar NavMesh sempre)
        }
    }
    
    int ContarVisiveis()
    {
        int c = 0;
        foreach(var s in soldadosNosAssentos) if(s != null) c++;
        return c;
    }
}
