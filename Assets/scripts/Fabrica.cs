using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

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

    public GerenciadorExtracoes GerenciadorExtracoesLocal
    {
        get { return GetComponent<GerenciadorExtracoes>(); }
    }

    public bool PossuiPainelIndustrial
    {
        get { return GerenciadorExtracoesLocal != null; }
    }

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
        ProducaoAutomaticaEdificio.Garantir(gameObject, ProducaoAutomaticaEdificio.TipoInstalacao.Fabrica);
        // Busca automática de pontos de saída se não houver nenhum configurado
        if (pontosSaidaExtras == null || pontosSaidaExtras.Count == 0)
        {
            pontosSaidaExtras = new List<Transform>();
            foreach (Transform filho in transform)
            {
                string nomeFilho = filho.name.Trim();
                if (nomeFilho.Contains("Ponto_Saida") || nomeFilho.Contains("Saida_Soldado") || nomeFilho.Contains("Saida"))
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

    private void OnMouseDown()
    {
        if (!PossuiPainelIndustrial)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        InteractionModeSnapshot snapshot = InteractionModeService.CurrentSnapshot();
        if (snapshot.HasOwner && snapshot.Owner != InteractionOwner.FactoryIndustryPanel)
        {
            return;
        }

        IdentidadeUnidade identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null && identidade.teamID != 1)
        {
            return;
        }

        FabricaMineriosMenuController.AbrirPara(this);
    }

    private static Dictionary<Transform, int> _contadorSlot = new Dictionary<Transform, int>();

    public GameObject ProduzirUnidade(GameObject prefab)
    {
        if (prefab == null) return null;
        long spawnStart = System.Diagnostics.Stopwatch.GetTimestamp();
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefab.name);

        Transform spawn = (pontoNascimento != null) ? pontoNascimento : transform;
        
        // Escolhe o próximo ponto de saída (Round Robin entre os 5 disponíveis)
        Transform saidaAlvo = pontoSaida;
        if (pontosSaidaExtras != null && pontosSaidaExtras.Count > 0)
        {
            saidaAlvo = pontosSaidaExtras[indiceSaidaGlobal % pontosSaidaExtras.Count];
            indiceSaidaGlobal++;
        }

        if (saidaAlvo == null) saidaAlvo = transform;

        float espacamento = CalcularEspacamentoSaida(prefab);
        Vector3 baseSaida = saidaAlvo.position;
        Vector3 direcaoSaida = saidaAlvo.forward;

        // Se a saída estiver muito próxima do nascimento, força um ponto externo à tenda.
        float distanciaSaida = Vector3.Distance(spawn.position, baseSaida);
        if (distanciaSaida < 4f)
        {
            Vector3 frenteFallback = transform.forward;
            frenteFallback.y = 0f;
            if (frenteFallback.sqrMagnitude < 0.01f) frenteFallback = Vector3.forward;
            frenteFallback.Normalize();

            baseSaida = spawn.position + (frenteFallback * 12f);
            direcaoSaida = frenteFallback;
        }

        // Calcula slot na fila para o ponto escolhido
        if (!_contadorSlot.ContainsKey(saidaAlvo)) _contadorSlot[saidaAlvo] = 0;
        _contadorSlot[saidaAlvo]++;
        int slotIdx = _contadorSlot[saidaAlvo] - 1;

        Vector3 posSlot = baseSaida + (direcaoSaida * (5f + slotIdx * espacamento));
        bool destinoValidado = false;
        long navmeshDestinoStart = System.Diagnostics.Stopwatch.GetTimestamp();
        UnityEngine.AI.NavMeshHit hitDestino;
        if (UnityEngine.AI.NavMesh.SamplePosition(posSlot, out hitDestino, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            posSlot = hitDestino.position;
            destinoValidado = true;
        }
        RegistrarTempoDiagnostico("navmesh_spawn_ms", navmeshDestinoStart);

        // Validação NavMesh para Spawn
        Vector3 posSpawnFinal = spawn.position;
        bool spawnValidado = false;
        UnityEngine.AI.NavMeshHit nh;
        long navmeshSpawnStart = System.Diagnostics.Stopwatch.GetTimestamp();
        if (UnityEngine.AI.NavMesh.SamplePosition(spawn.position, out nh, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            posSpawnFinal = nh.position;
            spawnValidado = true;
        }
        RegistrarTempoDiagnostico("navmesh_spawn_ms", navmeshSpawnStart);

        // Instancia no spawn interno
        GameObject unidade = Instantiate(prefab, posSpawnFinal, spawn.rotation);
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // Identidade e Logistica GDD
        var idF = GetComponentInParent<IdentidadeUnidade>();
        var idU = unidade.GetComponent<IdentidadeUnidade>();
        if (idF != null && idU != null) 
        { 
            idU.teamID = idF.teamID; 
            idU.nomeDoPais = idF.nomeDoPais; 
            
            // Consumo de população
            if (idU.militaresConsumidos > 0 && SistemaGovernoMundial.Instancia != null)
            {
                var pais = SistemaGovernoMundial.Instancia.ObterPais(idF.teamID);
                if (pais != null)
                {
                    // Usa alistáveis primeiro, depois civis
                    int falta = idU.militaresConsumidos;
                    if (pais.alistaveis >= falta)
                    {
                        pais.alistaveis -= falta;
                    }
                    else
                    {
                        int doAlistamento = pais.alistaveis;
                        falta -= doAlistamento;
                        pais.alistaveis = 0;
                        pais.populacaoCivil = Mathf.Max(0, pais.populacaoCivil - falta);
                    }
                    pais.populacaoMilitarAtiva += idU.militaresConsumidos;
                }
            }
        }
        if (idU != null
            && idU.tipoUnidade == TipoUnidade.Infantaria
            && (unidade.GetComponent<MovimentoRealTerrestre>() != null
                || unidade.GetComponent<CaminhaoTanqueAbastecimento>() != null))
        {
            idU.tipoUnidade = TipoUnidade.Veiculo;
        }

        CombustivelUnidade.Garantir(unidade, true);

        // EXCLUSIVO: Corotina para delay de 1 segundo antes de sair
        StartCoroutine(MoverParaSaidaComDelay(unidade, posSlot, posSpawnFinal, spawnValidado, destinoValidado, 1.0f));

        // Registro IA
        if (idU != null && idU.teamID != 1)
        {
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
        }

        RegistrarTempoDiagnostico("prefab_init_ms", initStart);
        RegistrarTempoDiagnostico("spawn_land_ms", spawnStart);

        return unidade;
    }

    IEnumerator MoverParaSaidaComDelay(GameObject unidade, Vector3 destino, Vector3 spawnValidadoPosicao, bool spawnJaValidado, bool destinoJaValidado, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (unidade != null)
        {
            if (!destinoJaValidado)
            {
                long navmeshDestinoStart = System.Diagnostics.Stopwatch.GetTimestamp();
                UnityEngine.AI.NavMeshHit hitDestino;
                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out hitDestino, 6f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    destino = hitDestino.position;
                }
                RegistrarTempoDiagnostico("navmesh_spawn_ms", navmeshDestinoStart);
            }

            var controle = unidade.GetComponent<ControleUnidade>();
            if (controle != null)
            {
                var agente = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agente != null && !agente.isOnNavMesh)
                {
                    if (spawnJaValidado)
                    {
                        agente.Warp(spawnValidadoPosicao);
                    }
                    else
                    {
                        long navmeshSpawnStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        UnityEngine.AI.NavMeshHit hitSpawn;
                        if (UnityEngine.AI.NavMesh.SamplePosition(unidade.transform.position, out hitSpawn, 10f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            agente.Warp(hitSpawn.position);
                        }
                        else
                        {
                            UnityEngine.AI.NavMeshHit hitDest;
                            if (UnityEngine.AI.NavMesh.SamplePosition(destino, out hitDest, 15.0f, UnityEngine.AI.NavMesh.AllAreas))
                            {
                                agente.Warp(hitDest.position);
                            }
                            else
                            {
                                agente.Warp(destino);
                            }
                        }
                        RegistrarTempoDiagnostico("navmesh_spawn_ms", navmeshSpawnStart);
                    }
                }
                controle.EmitirOrdemMover(destino);
            }
            else
            {
                var nav = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null)
                {
                    if (!nav.isOnNavMesh)
                    {
                        if (spawnJaValidado)
                        {
                            nav.Warp(spawnValidadoPosicao);
                        }
                        else
                        {
                            long navmeshSpawnStart = System.Diagnostics.Stopwatch.GetTimestamp();
                            UnityEngine.AI.NavMeshHit hitSpawn;
                            if (UnityEngine.AI.NavMesh.SamplePosition(unidade.transform.position, out hitSpawn, 10f, UnityEngine.AI.NavMesh.AllAreas))
                            {
                                nav.Warp(hitSpawn.position);
                            }
                            else
                            {
                                UnityEngine.AI.NavMeshHit hitDest;
                                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out hitDest, 15.0f, UnityEngine.AI.NavMesh.AllAreas))
                                {
                                    nav.Warp(hitDest.position);
                                }
                                else
                                {
                                    nav.Warp(destino);
                                }
                            }
                            RegistrarTempoDiagnostico("navmesh_spawn_ms", navmeshSpawnStart);
                        }
                    }

                    if (nav.isOnNavMesh)
                    {
                        nav.isStopped = false;
                        unidade.SendMessage("MoverParaPonto", destino, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }
    }

    private static void RegistrarTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
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
