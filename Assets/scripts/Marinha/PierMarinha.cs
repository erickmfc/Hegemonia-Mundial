using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class PierMarinha : MonoBehaviour
{
    [System.Serializable]
    public class VagaDeAtracagem
    {
        public string nomeDaVaga = "Vaga 01";
        [Tooltip("O ponto FINAL onde o navio fica parado.")]
        public Transform pontoDeAtracagem; 
        
        [Tooltip("Opcional: O navio irá para CÁ primeiro, se alinhará, e só depois entrará na vaga.")]
        public Transform pontoDeManobra; 

        public IdentidadeNaval.CategoriaNavio categoriaAceita;
        
        [Header("Estado (Apenas Leitura)")]
        public IdentidadeNaval navioOcupante;

        public bool EstaLivre()
        {
            if (navioOcupante == null) return true;
            if (!navioOcupante.EstaAtracado) {
                navioOcupante = null; 
                return true;
            }
            return false;
        }
    }

    [Header("Configuração das Bases")]
    public List<VagaDeAtracagem> vagasDisponiveis = new List<VagaDeAtracagem>();

    [Header("Configurações Gerais")]
    public float raioDeBusca = 1500f; // Aumentei para pegar navios mais longe
    public float velocidadeManobra = 3.5f; // Velocidade lenta para entrar na vaga

    [Header("Configuração de Saída")]
    public Transform[] pontosDeSaida;

    void OnMouseDown()
    {
        Debug.Log("[Pier] Solicitando atracagem automática...");
        ChamarNaviosParaVagasLivres();
    }

    // --- VISUALIZAÇÃO NO EDITOR ---
    void OnDrawGizmos()
    {
        if (vagasDisponiveis == null) return;
        
        foreach(var vaga in vagasDisponiveis)
        {
            if(vaga.pontoDeAtracagem != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(vaga.pontoDeAtracagem.position, 2f);
                
                if (vaga.pontoDeManobra != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(vaga.pontoDeManobra.position, 2f);
                    Gizmos.DrawLine(vaga.pontoDeManobra.position, vaga.pontoDeAtracagem.position);
                    
                    // Seta indicando frente
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(vaga.pontoDeManobra.position, vaga.pontoDeManobra.forward * 10f);
                }
            }
        }
    }

    public void AtribuirVaga(VagaDeAtracagem vaga, IdentidadeNaval navio)
    {
        // 🔹 Validação de Segurança antes de ocupar a vaga
        if (navio == null) return;
        
        var agent = navio.GetComponent<NavMeshAgent>();
        if (agent == null) agent = navio.GetComponentInChildren<NavMeshAgent>();
        
        if (agent == null)
        {
            Debug.LogError($"[Pier] Navio {navio.nomeDoNavio} não tem NavMeshAgent! Cancelando atracagem.");
            return;
        }

        vaga.navioOcupante = navio;
        
        // Vamos conduzir o navio manualmente via Coroutine
        StartCoroutine(RotinaDeAtracagem(vaga, navio));
    }

    IEnumerator RotinaDeAtracagem(VagaDeAtracagem vaga, IdentidadeNaval navio)
    {
        NavMeshAgent agent = navio.GetComponent<NavMeshAgent>();
        if(agent == null) yield break;

        // Avisa o navio que ele está "ocupado" atracando
        // Usamos ReceberOrdemDeAtracagem apenas para setar flags internas se houver, 
        // mas vamos controlar o destino aqui.
        navio.ReceberOrdemDeAtracagem(vaga.pontoDeManobra != null ? vaga.pontoDeManobra : vaga.pontoDeAtracagem);

        // ---------------------------------------------------------
        // FASE 1: IR PARA O PONTO DE MANOBRA (WAYPOINT)
        // ---------------------------------------------------------
        if (vaga.pontoDeManobra != null)
        {
            Debug.Log($"[Pier] {navio.nomeDoNavio} indo para ponto de MANOBRA...");
            agent.enabled = true;
            agent.isStopped = false;
            agent.SetDestination(vaga.pontoDeManobra.position);

            // Espera chegar
            while (agent.pathPending || agent.remainingDistance > 5f)
            {
                yield return null;
            }

            // Chegou no waypoint. Agora, alinhar rotação com o waypoint (ficar de frente pro pier)
            Debug.Log($"[Pier] {navio.nomeDoNavio} alinhando para entrada...");
            agent.isStopped = true; // Para o NavMesh temporariamente
            agent.enabled = false; // Desliga Agent para rotação manual suave

            Quaternion rotacaoAlvo = vaga.pontoDeManobra.rotation;
            
            // Rotaciona suavemente até alinhar
            float tempoGiro = 0f;
            while (Quaternion.Angle(navio.transform.rotation, rotacaoAlvo) > 1f && tempoGiro < 5f)
            {
                navio.transform.rotation = Quaternion.RotateTowards(navio.transform.rotation, rotacaoAlvo, 40f * Time.deltaTime);
                // Leve nudge pra frente pra não girar estático
                navio.transform.position = Vector3.MoveTowards(navio.transform.position, vaga.pontoDeManobra.position, 2f * Time.deltaTime);
                tempoGiro += Time.deltaTime;
                yield return null;
            }
        }

        // ---------------------------------------------------------
        // FASE 2: ENTRAR NA VAGA FINAL (Reta Final)
        // ---------------------------------------------------------
        Debug.Log($"[Pier] {navio.nomeDoNavio} entrando na vaga final...");
        
        // Garante que agent está desligado para fazermos movimento linear preciso (sem pathfinding batendo no pier)
        if(agent.enabled) agent.enabled = false;

        Vector3 posFinal = vaga.pontoDeAtracagem.position;
        Quaternion rotFinal = vaga.pontoDeAtracagem.rotation;

        while (Vector3.Distance(navio.transform.position, posFinal) > 0.1f || Quaternion.Angle(navio.transform.rotation, rotFinal) > 1f)
        {
            navio.transform.position = Vector3.MoveTowards(navio.transform.position, posFinal, velocidadeManobra * Time.deltaTime);
            navio.transform.rotation = Quaternion.RotateTowards(navio.transform.rotation, rotFinal, 10f * Time.deltaTime);
            yield return null;
        }

        // ---------------------------------------------------------
        // FINALIZADO
        // ---------------------------------------------------------
        navio.transform.position = posFinal;
        navio.transform.rotation = rotFinal;

        // Reativa agent parado para manter colisão/referência
        agent.enabled = true;
        agent.Warp(posFinal);
        agent.isStopped = true;

        Debug.Log($"[Pier] {navio.nomeDoNavio} ATRACADO 100%.");
    }


    // --- SISTEMA DE SAÍDA ---

    public void LiberarTodosNavios()
    {
        foreach(var vaga in vagasDisponiveis)
        {
            if (vaga.navioOcupante != null) LiberarVaga(vaga);
        }
    }

    public void LiberarVaga(VagaDeAtracagem vaga, Transform saidaDestino = null)
    {
        if (vaga.navioOcupante == null) return;

        IdentidadeNaval navio = vaga.navioOcupante;
        
        // Se não foi passado destino, calcula o mais próximo automático
        if (saidaDestino == null) saidaDestino = GetSaidaMaisProxima(navio.transform.position);

        if (saidaDestino != null)
        {
            // O próprio navio lida com a ré usando seu script IdentidadeNaval
            navio.SairDaDoca(saidaDestino.position);
            Debug.Log($"[Pier] Liberando {navio.nomeDoNavio} para {saidaDestino.name}");
        }
        else
        {
            // Fallback: Ré genérica e tchau
            Vector3 saidaGenerica = navio.transform.position - (navio.transform.forward * 50f);
            navio.SairDaDoca(saidaGenerica); 
        }

        vaga.navioOcupante = null; 
    }

    // --- UTILS ---

    Transform GetSaidaMaisProxima(Vector3 posicaoNavio)
    {
        if (pontosDeSaida == null || pontosDeSaida.Length == 0) return null;

        Transform melhorSaida = null;
        float menorDistancia = float.MaxValue;

        foreach (Transform saida in pontosDeSaida)
        {
            if (saida == null) continue;
            float dist = Vector3.Distance(posicaoNavio, saida.position);
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                melhorSaida = saida;
            }
        }
        return melhorSaida;
    }

    public void ChamarNaviosParaVagasLivres()
    {
        IdentidadeNaval[] todosNavios = FindObjectsOfType<IdentidadeNaval>();

        foreach (var vaga in vagasDisponiveis)
        {
            if (vaga.EstaLivre())
            {
                IdentidadeNaval melhorCandidato = FindBestShipForSpot(vaga, todosNavios);
                if (melhorCandidato != null)
                {
                    AtribuirVaga(vaga, melhorCandidato);
                }
            }
        }
    }

    IdentidadeNaval FindBestShipForSpot(VagaDeAtracagem vaga, IdentidadeNaval[] navios)
    {
        IdentidadeNaval candidato = null;
        float menorDistancia = raioDeBusca;

        foreach (var navio in navios)
        {
            if (navio.categoriaNavio == vaga.categoriaAceita && !navio.EstaAtracado)
            {
                // Verifica distância
                float dist = Vector3.Distance(transform.position, navio.transform.position);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    candidato = navio;
                }
            }
        }
        return candidato;
    }

    // --- CONSTRUÇÃO DE NAVIOS (API PARA MENU) ---
    public void ConstruirNavio(GameObject prefabNavio)
    {
        if (prefabNavio == null) return;

        // 1. Onde ele nasce? (Usa a saída como spawn point)
        Transform pontoSpawn = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0) pontoSpawn = pontosDeSaida[0];

        // 2. Cria o Navio
        GameObject novoNavio = Instantiate(prefabNavio, pontoSpawn.position, pontoSpawn.rotation);
        
        Debug.Log($"[Pier] Construiu novo navio: {novoNavio.name}");

        // 3. Tenta já colocar numa vaga (se disponível) ou mandar sair
        IdentidadeNaval id = novoNavio.GetComponent<IdentidadeNaval>();
        if (id != null)
        {
            // Procura uma vaga livre do tipo certo
            bool vagaEncontrada = false;
            foreach (var vaga in vagasDisponiveis)
            {
                if (vaga.EstaLivre() && vaga.categoriaAceita == id.categoriaNavio)
                {
                    AtribuirVaga(vaga, id);
                    vagaEncontrada = true;
                    break;
                }
            }

            if (!vagaEncontrada)
            {
                Debug.Log("[Pier] Sem vagas. Navio enviado para saída.");
                // Se não tem vaga, manda sair para não entupir o spawn
                if(pontosDeSaida.Length > 1) 
                    id.MoverPara(pontosDeSaida[1].position); // Vai para o segundo ponto (mar aberto)
                else
                    id.MoverPara(transform.position + transform.forward * 100f);
            }
        }
    }
}
