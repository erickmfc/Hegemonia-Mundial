using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.BrainMaster;

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

        // Controle de Manutenção Interno
        [System.NonSerialized] public float timerRecarga = 0f;
        [System.NonSerialized] public float timerRecargaContramedidas = 0f;
        [System.NonSerialized] public bool atracagemCompleta = false; 

        public bool EstaLivre()
        {
            if (navioOcupante == null)
            {
                atracagemCompleta = false;
                timerRecarga = 0f;
                timerRecargaContramedidas = 0f;
                return true;
            }
            if (!navioOcupante.EstaAtracado) {
                navioOcupante = null;
                atracagemCompleta = false;
                timerRecarga = 0f;
                timerRecargaContramedidas = 0f;
                return true;
            }
            return false;
        }
    }

    [Header("Configuração das Bases")]
    public List<VagaDeAtracagem> vagasDisponiveis = new List<VagaDeAtracagem>();

    [Header("Pontos de Logística (Arraste os GameObjects aqui)")]
    public Transform saida_petro;   // Ponto de aproximação (Entrada do Pier)
    public Transform Atraca_petro;  // Ponto de atracagem (Dock)

    [Header("Estado")]
    public bool ocupada = false;

    public void TentarOcupar()
    {
        ocupada = true;
    }

    public void Liberar()
    {
        ocupada = false;
    }

    public void ReceberPetroleo(int quantidade)
    {
        if (GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AdicionarRecursos(addPetroleo: quantidade);
            // Opcional: Mostrar feedback flutuante (não implementado aqui)
        }
    }

    [Header("Configurações Gerais")]
    public float raioDeBusca = 1500f; 
    public float velocidadeManobra = 3.5f;

    [Header("Manutenção e Reparo")]
    public float reparoPorSegundo = 10f; // Cura 10HP/s
    public float intervaloRecargaMissel = 1.0f; // 1 Míssil por segundo
    public float intervaloRecargaContramedidas = 2.0f; // 1 cartucho/manutencao por ciclo
    
    [Header("Configuração de Saída")]
    public Transform[] pontosDeSaida;

    [Header("Navegação (Petroleiros)")]
    // Estes pontos devem ser configurados no Inspector
    public Transform pontoEntrada;    // Onde o navio mira ao chegar
    public Transform pontoAcoplagem;  // Onde o navio DESCARREGA
    public Transform pontoSaidaNavio; // Para onde ele olha ao sair (pode ser um dos pontosDeSaida)



    // Métodos duplicados removidos

    private Construtor construtorLocal; // Referencia ao construtor da cena

    void Awake()
    {
        // Pontos do Petroleiro removidos conforme solicitado
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    Transform CriarPonto(string nome, Vector3 pos)
    {
        GameObject p = new GameObject(nome);
        p.transform.position = pos;
        p.transform.SetParent(this.transform);
        return p.transform;
    }

    void Start()
    {
        CorrigirPoseCosteiraSeNecessario();
        StartCoroutine(RotinaBuscaConstrucao());
        RegistrarNoGerente();
    }

    public void RegistrarNoGerente()
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
                // Registra o primeiro ponto de saída ou o próprio transform como spawn
                Transform spawn = (pontosDeSaida != null && pontosDeSaida.Length > 0) ? pontosDeSaida[0] : transform;
                Transform saida = (pontosDeSaida != null && pontosDeSaida.Length > 1) ? pontosDeSaida[1] : spawn;
                
                gerente.AtualizarPontoEstaleiro(spawn, saida);
            }
        }
    }

    bool EstruturaDoJogadorHumano()
    {
        IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
        if (id == null) id = GetComponentInParent<IdentidadeUnidade>();
        return id == null || id.teamID == 1;
    }

    bool IgnorarRegrasCosteirasManuais()
    {
        // IA já tem pontos manuais predefinidos, não precisa de validação costeira
        if (!EstruturaDoJogadorHumano())
        {
            return true;
        }

        return GetComponent<IA_ManualPlacementTag>() != null;
    }

    void CorrigirPoseCosteiraSeNecessario()
    {
        if (EstruturaDoJogadorHumano() || IgnorarRegrasCosteirasManuais())
        {
            return;
        }

        string validacao;
        if (NavalPlacementResolver.IsCurrentStructurePoseValid(gameObject, out validacao))
        {
            return;
        }

        NavalPlacementResolver.StructurePose pose;
        if (!NavalPlacementResolver.TryResolveStructurePose(gameObject, transform.position, transform.rotation, out pose))
        {
            return;
        }

        transform.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    IEnumerator RotinaBuscaConstrucao()
    {
        while(true)
        {
            yield return new WaitForSeconds(3.0f);
            if(construtorLocal == null)
                construtorLocal = Construtor.Instancia != null ? Construtor.Instancia : FindFirstObjectByType<Construtor>();
        }
    }

    void Update()
    {
        ProcessarManutencao();

        if (Input.GetKeyDown(KeyCode.V))
        {
            MenuPier.AlternarPorAtalho(this);
        }
    }

    void ProcessarManutencao()
    {
        foreach (var vaga in vagasDisponiveis)
        {
            IdentidadeNaval navio;
            if (!TryGetValidDockedShip(vaga, out navio))
            {
                continue;
            }

            try
            {

            // Lógica de reparo funciona se já atracou
            if (vaga.atracagemCompleta)
            {
                // 1. REPARO DE VIDA
                SistemaDeDanos vida = navio.GetComponent<SistemaDeDanos>();
                if (vida != null && vida.vidaAtual < vida.vidaMaxima)
                {
                    vida.Reparar(reparoPorSegundo * Time.deltaTime);
                }

                // 2. RECARGA DE MÍSSEIS
                LancadorNaval lancador = navio.GetComponentInChildren<LancadorNaval>();
                if (lancador != null && lancador.municaoTotal < lancador.municaoMaxima)
                {
                    vaga.timerRecarga += Time.deltaTime;
                    if (vaga.timerRecarga >= intervaloRecargaMissel)
                    {
                        lancador.Recarregar(1);
                        vaga.timerRecarga = 0f;
                    }
                }
                else
                {
                    vaga.timerRecarga = 0f;
                }

                // 3. REABASTECIMENTO DE CONTRAMEDIDAS
                SistemaAntiMissil[] sistemasAntiMissil = navio.GetComponentsInChildren<SistemaAntiMissil>(true);
                bool precisaContramedida = false;
                for (int i = 0; i < sistemasAntiMissil.Length; i++)
                {
                    SistemaAntiMissil sistema = sistemasAntiMissil[i];
                    if (sistema != null && sistema.PrecisaReabastecimentoPier())
                    {
                        precisaContramedida = true;
                        break;
                    }
                }

                if (precisaContramedida)
                {
                    vaga.timerRecargaContramedidas += Time.deltaTime;
                    if (vaga.timerRecargaContramedidas >= intervaloRecargaContramedidas)
                    {
                        for (int i = 0; i < sistemasAntiMissil.Length; i++)
                        {
                            SistemaAntiMissil sistema = sistemasAntiMissil[i];
                            if (sistema == null) continue;
                            sistema.ReabastecerNoPier(1);
                        }

                        vaga.timerRecargaContramedidas = 0f;
                    }
                }
                else
                {
                    vaga.timerRecargaContramedidas = 0f;
                }
            }
            }
            catch (MissingReferenceException)
            {
                LimparEstadoDaVaga(vaga, navio);
            }
        }
    }

    void OnMouseDown()
    {
        // IGNORA O CLIQUE SE O MOUSE ESTIVER EM CIMA DA UI
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Debug.Log("[Pier] Solicitando atracagem automática...");
        ChamarNaviosParaVagasLivres();
    }

    [Header("Indicadores Litorâneos (Terra/Água)")]
    public float offsetAguaFrente = 35f; 
    public float offsetTerraTras = -15f; 

    // --- VISUALIZAÇÃO NO EDITOR ---
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
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(vaga.pontoDeManobra.position, vaga.pontoDeManobra.forward * 10f);
                }
            }
        }
    }

    public void AtribuirVaga(VagaDeAtracagem vaga, IdentidadeNaval navio)
    {
        if (vaga == null || navio == null) return;
        
        var agent = navio.GetComponent<NavMeshAgent>();
        if (agent == null) agent = navio.GetComponentInChildren<NavMeshAgent>();
        
        if (agent == null)
        {
            Debug.LogError($"[Pier] Navio {navio.nomeDoNavio} não tem NavMeshAgent! Cancelando atracagem.");
            return;
        }

        vaga.navioOcupante = navio;
        vaga.atracagemCompleta = false; 
        StartCoroutine(RotinaDeAtracagem(vaga, navio));
    }

    IEnumerator RotinaDeAtracagem(VagaDeAtracagem vaga, IdentidadeNaval navio)
    {
        if (vaga == null || navio == null)
        {
            yield break;
        }

        NavMeshAgent agent = navio.GetComponent<NavMeshAgent>();
        ControleNavioRealista controleFisico = navio.GetComponent<ControleNavioRealista>();
        
        if (agent == null)
        {
            LimparEstadoDaVaga(vaga, navio);
            yield break;
        }

        float distanciaOriginal = 15f;
        if (vaga.pontoDeAtracagem == null)
        {
            Debug.LogWarning("[Pier] Vaga sem ponto de atracagem configurado. Cancelando atracagem.");
            LimparEstadoDaVaga(vaga, navio);
            if (navio != null)
            {
                navio.NotificarMovimento();
            }
            yield break;
        }

        if (controleFisico != null)
        {
            distanciaOriginal = controleFisico.distanciaChegada;
            // Permite chegar BEM perto para evitar "sliding" longo
            controleFisico.distanciaChegada = 2.0f; 
            controleFisico.modoOperacao = ControleNavioRealista.ModoOperacao.Ativo; 
        }

        navio.ReceberOrdemDeAtracagem(vaga.pontoDeManobra != null ? vaga.pontoDeManobra : vaga.pontoDeAtracagem);

        // FASE 1: NAVEGAÇÃO AUTÔNOMA (NAVMESH)
        if (vaga.pontoDeManobra != null)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                // GerenteDeJogo.Instancia.AtualizarPontoEstaleiro(pontosSpawn[0], saidaNavio); // This line was not in the original content, but was in the instruction's context. I will ignore it as per "make the change faithfully and without making any unrelated edits."
            }
            
            // Registra em todos os menus e no gerente
            MenuPier[] menus = FindObjectsByType<MenuPier>(FindObjectsSortMode.None);
            foreach(var m in menus) m.RegistrarNovoPier(this);

            var idPier = GetComponentInParent<IdentidadeUnidade>();
            if (idPier != null && GerenteDeJogo.Instancia != null && idPier.teamID == 1)
            {
                // O GerenteDeJogo lida com o registro de tudo que o jogador possui
            }
            float timerChegada = 0f;
            while (navio != null
                && vaga != null
                && vaga.navioOcupante == navio
                && agent != null
                && agent.isActiveAndEnabled
                && (agent.pathPending || agent.remainingDistance > 2.5f))
            {
                timerChegada += Time.deltaTime;
                if (timerChegada > 60f) break; // Timeout generoso
                yield return null;
            }

            // Fase manual: Desliga física
            if (!PodeContinuarAtracagem(vaga, navio))
            {
                RestaurarEstadoAposAtracagemCancelada(vaga, navio, controleFisico, distanciaOriginal);
                yield break;
            }

            if(controleFisico != null) controleFisico.enabled = false;

            if(agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true; 
                agent.enabled = false; 
            }

            // ALINHAMENTO IMPRECISO (Mantém dinâmico)
            if (vaga.pontoDeManobra == null)
            {
                RestaurarEstadoAposAtracagemCancelada(vaga, navio, controleFisico, distanciaOriginal);
                yield break;
            }

            Quaternion rotacaoAlvo = vaga.pontoDeManobra.rotation;
            float tempoGiro = 0f;
            // Aumentei pra 20s para dar tempo de virar navios pesados
            while (navio != null
                && vaga != null
                && vaga.navioOcupante == navio
                && vaga.pontoDeManobra != null
                && Quaternion.Angle(navio.transform.rotation, rotacaoAlvo) > 1f
                && tempoGiro < 20f)
            {
                navio.transform.rotation = Quaternion.RotateTowards(navio.transform.rotation, rotacaoAlvo, 40f * Time.deltaTime);
                navio.transform.position = Vector3.MoveTowards(navio.transform.position, vaga.pontoDeManobra.position, 2f * Time.deltaTime);
                tempoGiro += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            if(controleFisico != null) controleFisico.enabled = false;
        }

        // FASE 2: ENTRADA FINAL NA VAGA
        if (!PodeContinuarAtracagem(vaga, navio))
        {
            RestaurarEstadoAposAtracagemCancelada(vaga, navio, controleFisico, distanciaOriginal);
            yield break;
        }

        if(agent != null && agent.isActiveAndEnabled) agent.enabled = false;
        if(controleFisico != null) controleFisico.enabled = false;

        Vector3 posFinal = vaga.pontoDeAtracagem.position;
        Quaternion rotFinal = vaga.pontoDeAtracagem.rotation;

        float timerEntrada = 0f;
        // Timeout longo (60s) para não teleportar se estiver lento
        while (navio != null
            && vaga != null
            && vaga.navioOcupante == navio
            && (Vector3.Distance(navio.transform.position, posFinal) > 0.05f || Quaternion.Angle(navio.transform.rotation, rotFinal) > 0.5f)
            && timerEntrada < 60f)
        {
            navio.transform.position = Vector3.MoveTowards(navio.transform.position, posFinal, velocidadeManobra * Time.deltaTime);
            navio.transform.rotation = Quaternion.RotateTowards(navio.transform.rotation, rotFinal, 15f * Time.deltaTime);
            timerEntrada += Time.deltaTime;
            yield return null;
        }

        if (!PodeContinuarAtracagem(vaga, navio))
        {
            RestaurarEstadoAposAtracagemCancelada(vaga, navio, controleFisico, distanciaOriginal);
            yield break;
        }

        // Snap final imperceptível
        navio.transform.position = posFinal;
        navio.transform.rotation = rotFinal;

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(posFinal);
            agent.isStopped = true;
        }

        if (controleFisico != null)
        {
            controleFisico.enabled = true; 
            controleFisico.distanciaChegada = distanciaOriginal;
        }

        vaga.atracagemCompleta = true; 
        Debug.Log($"[Pier] {navio.nomeDoNavio} ATRACADO 100%.");
    }

    public void LiberarTodosNavios()
    {
        foreach(var vaga in vagasDisponiveis)
        {
            if (vaga.navioOcupante != null) LiberarVaga(vaga);
        }
    }

    public void LiberarVaga(VagaDeAtracagem vaga, Transform saidaDestino = null)
    {
        if (vaga == null) return;
        if (vaga.navioOcupante == null)
        {
            LimparEstadoDaVaga(vaga);
            return;
        }

        IdentidadeNaval navio = vaga.navioOcupante;
        if (navio == null)
        {
            LimparEstadoDaVaga(vaga);
            return;
        }

        try
        {
            if (saidaDestino == null) saidaDestino = GetSaidaMaisProxima(navio.transform.position);

            if (saidaDestino != null) navio.SairDaDoca(saidaDestino.position);
            else navio.SairDaDoca(navio.transform.position - (navio.transform.forward * 50f));

            LimparEstadoDaVaga(vaga, navio);
        }
        catch (MissingReferenceException)
        {
            LimparEstadoDaVaga(vaga, navio);
        }
    }

    Transform GetSaidaMaisProxima(Vector3 posicaoNavio)
    {
        if (pontosDeSaida == null || pontosDeSaida.Length == 0) return null;
        Transform melhorSaida = null;
        float menorDistancia = float.MaxValue;
        foreach (Transform saida in pontosDeSaida)
        {
            if (saida == null) continue;
            float dist = Vector3.Distance(posicaoNavio, saida.position);
            if (dist < menorDistancia) { menorDistancia = dist; melhorSaida = saida; }
        }
        return melhorSaida;
    }

    public void ChamarNaviosParaVagasLivres()
    {
        IdentidadeNaval[] todosNavios = Object.FindObjectsByType<IdentidadeNaval>(FindObjectsSortMode.None);
        foreach (var vaga in vagasDisponiveis)
        {
            if (vaga.EstaLivre())
            {
                IdentidadeNaval melhorCandidato = FindBestShipForSpot(vaga, todosNavios);
                if (melhorCandidato != null) AtribuirVaga(vaga, melhorCandidato);
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
                float dist = Vector3.Distance(transform.position, navio.transform.position);
                if (dist < menorDistancia) { menorDistancia = dist; candidato = navio; }
            }
        }
        return candidato;
    }

    public bool ConstruirNavio(GameObject prefabNavio)
    {
        if (prefabNavio == null) return false;
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefabNavio.name);

        string validacaoPier;
        if (!IgnorarRegrasCosteirasManuais() && !NavalPlacementResolver.IsCurrentStructurePoseValid(gameObject, out validacaoPier))
        {
            Debug.LogWarning("[PierMarinha] Construção naval bloqueada: " + validacaoPier);
            return false;
        }

        Transform pontoSpawn = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0) pontoSpawn = pontosDeSaida[0];
        Vector3 forwardSpawn = pontoSpawn != null ? pontoSpawn.forward : transform.forward;
        if (forwardSpawn.sqrMagnitude < 0.01f) forwardSpawn = transform.forward;

        Vector3 posSpawn;
        float nivelMar;
        if (!NavalPlacementResolver.TryResolveWaterSpawn(
            pontoSpawn.position,
            forwardSpawn,
            0f,
            420f,
            out posSpawn,
            out nivelMar,
            out validacaoPier))
        {
            Debug.LogWarning("[PierMarinha] Não foi possível achar água para criar o navio: " + validacaoPier);
            return false;
        }

        GameObject novoNavio = Instantiate(prefabNavio, posSpawn, pontoSpawn.rotation);
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();
        
        // CORRECAO DE NOME: Remove (Clone) para que a IA consiga contar na Meta!
        string nomeLimpo = prefabNavio.name.ToLower();
        if (nomeLimpo.Contains("sub")) novoNavio.name = "submarino";
        else novoNavio.name = "navio";

        // --- DEFINIR IDENTIDADE ---
        var idPier = GetComponentInParent<IdentidadeUnidade>();
        var idNavio = novoNavio.GetComponent<IdentidadeUnidade>();
        if (idNavio == null) idNavio = novoNavio.AddComponent<IdentidadeUnidade>();

        if (idPier != null && idNavio != null)
        {
            idNavio.teamID = idPier.teamID;
            idNavio.nomeDoPais = idPier.nomeDoPais;
        }
        else if (idNavio != null)
        {
            idNavio.teamID = 1;
            if (string.IsNullOrEmpty(idNavio.nomeDoPais)) idNavio.nomeDoPais = "Hegemonia";
        }

        IdentidadeNaval idNaval = novoNavio.GetComponent<IdentidadeNaval>();
        if (idNaval != null)
        {
            Vector3 destinoHint = (pontosDeSaida != null && pontosDeSaida.Length > 1 && pontosDeSaida[1] != null)
                ? pontosDeSaida[1].position
                : transform.position + (transform.forward * 130f);
            Vector3 destNaval;
            string destinoReason;
            float nivelDestino;
            if (!NavalPlacementResolver.TryResolveWaterSpawn(
                destinoHint,
                transform.forward,
                20f,
                220f,
                out destNaval,
                out nivelDestino,
                out destinoReason))
            {
                destNaval = posSpawn + (transform.forward.normalized * 130f);
                destNaval.y = nivelMar;
            }

            Vector3 direcaoSaida = destNaval - novoNavio.transform.position;
            direcaoSaida.y = 0f;
            if (direcaoSaida.sqrMagnitude > 0.01f)
            {
                novoNavio.transform.rotation = Quaternion.LookRotation(direcaoSaida.normalized, Vector3.up);
            }

            ControleUnidade controleUnidade = novoNavio.GetComponent<ControleUnidade>();
            ControleNavioRealista controleRealista = novoNavio.GetComponent<ControleNavioRealista>();
            ControleSubmarino controleSubmarino = novoNavio.GetComponent<ControleSubmarino>();
            bool movimentoDelegado = false;

            if (controleRealista != null)
            {
                controleRealista.PrepararSaidaInicial(destNaval, 8f);
            }

            if (controleSubmarino != null)
            {
                controleSubmarino.ForcarEstadoSuperficieImediato();
            }

            if (controleUnidade != null)
            {
                movimentoDelegado = controleUnidade.EmitirOrdemMover(destNaval);
            }

            if (!movimentoDelegado && controleRealista != null)
            {
                controleRealista.DefinirDestino(destNaval);
            }
            else if (!movimentoDelegado && controleSubmarino != null)
            {
                controleSubmarino.DefinirDestino(destNaval);
            }
            else if (!movimentoDelegado)
            {
                idNaval.MoverPara(destNaval);
            }
        }

        if (idPier != null && idPier.teamID != 1)
        {
            var myCommander = IA_ComandanteRegistry.GetCommanderByTeam(idPier.teamID);
            if (myCommander != null && myCommander.cerebroGeneral != null)
            {
                myCommander.cerebroGeneral.RegistrarUnidade(novoNavio);
            }

            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
        }
        
        Debug.Log($"[PierMarinha] {novoNavio.name} criado em {posSpawn}. Agua confirmada.");
        RegistrarTempoDiagnostico("prefab_init_ms", initStart);
        return true;
    }

    private static void RegistrarTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }
    }

    bool VerificarSeEhAgua(Vector3 pos)
    {
        return NavalPlacementResolver.IsWaterAtPosition(pos);
    }

    // --- INTERFACE VISUAL DE REPARO ---
    // --- INTERFACE VISUAL DE REPARO ---
    void OnGUI()
    {
        if (Camera.main == null) return;

        foreach (var vaga in vagasDisponiveis)
        {
            // MOSTRA SEMPRE QUE ATRACADO (Mesmo 100%)
            IdentidadeNaval navio;
            if (TryGetValidDockedShip(vaga, out navio) && vaga.atracagemCompleta)
            {
                // Pega posição do navio na tela
                try
                {
                    Vector3 posMundo = navio.transform.position + Vector3.up * 8f;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(posMundo);
                if (screenPos.z < 0) continue;

                float y = Screen.height - screenPos.y - 120f; 

                float pctVida = 1f;
                int municao = 0, munMax = 0;

                SistemaDeDanos vida = navio.GetComponent<SistemaDeDanos>();
                if (vida != null) pctVida = vida.vidaAtual / vida.vidaMaxima;

                LancadorNaval lancador = navio.GetComponentInChildren<LancadorNaval>();
                if (lancador != null) { municao = lancador.municaoTotal; munMax = lancador.municaoMaxima; }

                float boxWidth = 200f;
                float boxHeight = (munMax > 0) ? 80f : 50f;
                Rect boxRect = new Rect(screenPos.x - boxWidth/2, y, boxWidth, boxHeight);
                
                Color oldColor = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.6f); 
                GUI.DrawTexture(boxRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = oldColor;

                GUIStyle styleHeader = new GUIStyle(GUI.skin.label);
                styleHeader.alignment = TextAnchor.MiddleCenter;
                styleHeader.fontStyle = FontStyle.Bold;
                styleHeader.normal.textColor = Color.white;
                styleHeader.fontSize = 12;

                GUI.Label(new Rect(screenPos.x - 100, y + 5, 200, 20), "MANUTENÇÃO NAVAL", styleHeader);

                GUIStyle styleStatus = new GUIStyle(GUI.skin.label);
                styleStatus.alignment = TextAnchor.MiddleCenter;
                styleStatus.fontStyle = FontStyle.Bold;
                styleStatus.fontSize = 14; 

                string txtVida = $"ESTRUTURA: {pctVida:P0}";
                styleStatus.normal.textColor = Color.Lerp(Color.red, Color.green, pctVida);
                GUI.Label(new Rect(screenPos.x - 100, y + 25, 200, 20), txtVida, styleStatus);

                if (munMax > 0)
                {
                    string txtMun = $"MÍSSEIS: {municao}/{munMax}";
                    float pctMun = (float)municao / munMax;
                    styleStatus.normal.textColor = Color.Lerp(Color.yellow, Color.cyan, pctMun);
                    GUI.Label(new Rect(screenPos.x - 100, y + 50, 200, 20), txtMun, styleStatus);
                }
                }
                catch (MissingReferenceException)
                {
                    LimparEstadoDaVaga(vaga, navio);
                }
            }
        }
    }

    bool PodeContinuarAtracagem(VagaDeAtracagem vaga, IdentidadeNaval navio)
    {
        return vaga != null
            && navio != null
            && vaga.navioOcupante == navio
            && vaga.pontoDeAtracagem != null;
    }

    bool TryGetValidDockedShip(VagaDeAtracagem vaga, out IdentidadeNaval navio)
    {
        navio = null;
        if (vaga == null || vaga.EstaLivre())
        {
            return false;
        }

        navio = vaga.navioOcupante;
        if (navio == null)
        {
            LimparEstadoDaVaga(vaga);
            return false;
        }

        return true;
    }

    void RestaurarEstadoAposAtracagemCancelada(VagaDeAtracagem vaga, IdentidadeNaval navio, ControleNavioRealista controleFisico, float distanciaOriginal)
    {
        if (controleFisico != null)
        {
            controleFisico.distanciaChegada = distanciaOriginal;
            controleFisico.enabled = true;
        }

        if (navio != null)
        {
            navio.NotificarMovimento();
        }

        LimparEstadoDaVaga(vaga, navio);
    }

    void LimparEstadoDaVaga(VagaDeAtracagem vaga, IdentidadeNaval navioEsperado = null)
    {
        if (vaga == null)
        {
            return;
        }

        if (navioEsperado == null || vaga.navioOcupante == null || vaga.navioOcupante == navioEsperado)
        {
            vaga.navioOcupante = null;
        }

        vaga.atracagemCompleta = false;
        vaga.timerRecarga = 0f;
        vaga.timerRecargaContramedidas = 0f;
    }
}
