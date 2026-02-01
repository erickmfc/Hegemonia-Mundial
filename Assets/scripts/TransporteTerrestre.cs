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

        Debug.Log($"[{gameObject.name}] BUSCANDO PASSAGEIROS... (Raio: {distanciaParaEmbarque}m)");

        // --- BUSCA GLOBAL POR PROXIMIDADE ---
        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaParaEmbarque);
        List<GameObject> processados = new List<GameObject>(); // Evita duplicatas na mesma passada
        
        foreach (var hit in hits)
        {
            if (totalEmbarcados >= capacidadeMaxima) break;

            // Tenta achar a unidade lógica (onde está o ControleUnidade ou NavMesh)
            // Em vez de ir para a Raiz (que pode ser um agrupamento), busca o componente funcional.
            ControleUnidade ctrl = hit.GetComponentInParent<ControleUnidade>();
            GameObject soldado = null;

            if (ctrl != null) soldado = ctrl.gameObject;
            else 
            {
                // Tenta achar NavMesh se não tiver Controle
                var nav = hit.GetComponentInParent<NavMeshAgent>();
                if(nav != null) soldado = nav.gameObject;
                else soldado = hit.transform.root.gameObject; // Última tentativa
            }

            if (soldado == null || !soldado.activeInHierarchy) continue;
            if (processados.Contains(soldado)) continue; // Já testou este cara
            processados.Add(soldado); 

            // Ignora a mim mesmo
            if (soldado == gameObject || soldado.transform.root == transform.root) continue;

             // --- FILTRAGEM ---

            // 1. Ignora Veículos de Transporte e Tanques Grandes
            // Se tiver script de transporte ou for muito grande
            if (soldado.GetComponent<TransporteTerrestre>() != null) continue;
            
            // 2. Filtro por Nome (Mais permissivo)
            string nomeLower = soldado.name.ToLower();
            
            // Bloqueia Navios e Aviões óbvios
            if (nomeLower.Contains("uss") || nomeLower.Contains("ship") || nomeLower.Contains("aviao") || nomeLower.Contains("jet")) continue;

            // 3. Verifica se é "Embarcável" (Biológico ou Pequeno)
            SistemaDeDanos sd = soldado.GetComponent<SistemaDeDanos>();
            bool ehViavel = false;
            
            if (sd != null && sd.unidadeBiologica) 
            {
                ehViavel = true;
            }
            else
            {
                // Fallback: Verifica tamanho físico
                var nav = soldado.GetComponent<NavMeshAgent>();
                // Soldados < 0.8, Tanques > 1.5. Aceitamos até 1.0 (Exoesqueletos?)
                if(nav != null && nav.radius < 1.0f) ehViavel = true;
                
                // Fallback nominal expandido
                if(nomeLower.Contains("soldado") || nomeLower.Contains("soldier") || 
                   nomeLower.Contains("infant") || nomeLower.Contains("atira") || 
                   nomeLower.Contains("rifle") || nomeLower.Contains("fuzil") || 
                   nomeLower.Contains("sniper") || nomeLower.Contains("medico") ||
                   nomeLower.Contains("eng") || nomeLower.Contains("person") ||
                   nomeLower.Contains("caoc") || nomeLower.Contains("unidade")) // CAOC pode ser unidade especial?
                {
                    ehViavel = true;
                }
            }

            // SÓ EMBARCA SE FOR VIÁVEL
            if (ehViavel)
            {
                // Verifica time (Opcional: Só embarca aliados)
                var identidade = soldado.GetComponent<IdentidadeUnidade>();
                var minhaIdentidade = GetComponent<IdentidadeUnidade>();
                
                bool mesmoTime = true;
                if(identidade != null && minhaIdentidade != null)
                {
                    if(identidade.teamID != minhaIdentidade.teamID) mesmoTime = false;
                }

                if(mesmoTime)
                {
                    Debug.Log($"Embarcando Passageiro: {soldado.name}");
                    EmbarcarUnidade(soldado);
                    totalEmbarcados++;
                }
            }
        }
        
        if (totalEmbarcados == soldadosInternos.Count + ContarVisiveis())
        {
             // Se não mudou nada
             Debug.Log("Nenhum passageiro válido encontrado por perto.");
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

    public bool TemPassageiros
    {
        get { return soldadosInternos.Count > 0 || ContarVisiveis() > 0; }
    }
}
