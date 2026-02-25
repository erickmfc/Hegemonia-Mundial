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

        // Controle de Manutenção Interno
        [System.NonSerialized] public float timerRecarga = 0f;
        [System.NonSerialized] public bool atracagemCompleta = false; 

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
    
    [Header("Configuração de Saída")]
    public Transform[] pontosDeSaida;

    [Header("Navegação (Petroleiros)")]
    // Estes pontos devem ser configurados no Inspector
    public Transform pontoEntrada;    // Onde o navio mira ao chegar
    public Transform pontoAcoplagem;  // Onde o navio DESCARREGA
    public Transform pontoSaidaNavio; // Para onde ele olha ao sair (pode ser um dos pontosDeSaida)

    private bool ocupadoPorPetroleiro = false;

    // Métodos duplicados removidos

    private Construtor construtorLocal; // Referencia ao construtor da cena

    void Awake()
    {
        // Pontos do Petroleiro removidos conforme solicitado
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
        StartCoroutine(RotinaBuscaConstrucao());
    }

    IEnumerator RotinaBuscaConstrucao()
    {
        while(true)
        {
            yield return new WaitForSeconds(3.0f);
            if(construtorLocal == null)
                construtorLocal = FindFirstObjectByType<Construtor>();
        }
    }

    void Update()
    {
        ProcessarManutencao();
    }

    void ProcessarManutencao()
    {
        foreach (var vaga in vagasDisponiveis)
        {
            // Lógica de reparo funciona se já atracou
            if (vaga.navioOcupante != null && vaga.atracagemCompleta)
            {
                // 1. REPARO DE VIDA
                SistemaDeDanos vida = vaga.navioOcupante.GetComponent<SistemaDeDanos>();
                if (vida != null && vida.vidaAtual < vida.vidaMaxima)
                {
                    vida.Reparar(reparoPorSegundo * Time.deltaTime);
                }

                // 2. RECARGA DE MÍSSEIS
                LancadorNaval lancador = vaga.navioOcupante.GetComponentInChildren<LancadorNaval>();
                if (lancador != null && lancador.municaoTotal < lancador.municaoMaxima)
                {
                    vaga.timerRecarga += Time.deltaTime;
                    if (vaga.timerRecarga >= intervaloRecargaMissel)
                    {
                        lancador.Recarregar(1);
                        vaga.timerRecarga = 0f;
                    }
                }
            }
        }
    }

    void OnMouseDown()
    {
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
        if (navio == null) return;
        
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
        NavMeshAgent agent = navio.GetComponent<NavMeshAgent>();
        ControleNavioRealista controleFisico = navio.GetComponent<ControleNavioRealista>();
        
        if(agent == null) yield break;

        float distanciaOriginal = 15f;
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
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                // GerenteDeJogo.Instancia.AtualizarPontoEstaleiro(pontosSpawn[0], saidaNavio); // This line was not in the original content, but was in the instruction's context. I will ignore it as per "make the change faithfully and without making any unrelated edits."
            }
            
            // Registra em todos os menus
            MenuPier[] menus = FindObjectsByType<MenuPier>(FindObjectsSortMode.None);
            foreach(var m in menus) m.RegistrarNovoPier(this);
            float timerChegada = 0f;
            while (agent.isActiveAndEnabled && navio != null && (agent.pathPending || agent.remainingDistance > 2.5f))
            {
                timerChegada += Time.deltaTime;
                if (timerChegada > 60f) break; // Timeout generoso
                yield return null;
            }

            // Fase manual: Desliga física
            if(controleFisico != null) controleFisico.enabled = false;

            if(agent.isActiveAndEnabled) 
            {
                agent.isStopped = true; 
                agent.enabled = false; 
            }

            // ALINHAMENTO IMPRECISO (Mantém dinâmico)
            Quaternion rotacaoAlvo = vaga.pontoDeManobra.rotation;
            float tempoGiro = 0f;
            // Aumentei pra 20s para dar tempo de virar navios pesados
            while (Quaternion.Angle(navio.transform.rotation, rotacaoAlvo) > 1f && tempoGiro < 20f)
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
        if(agent.isActiveAndEnabled) agent.enabled = false;
        if(controleFisico != null) controleFisico.enabled = false;

        Vector3 posFinal = vaga.pontoDeAtracagem.position;
        Quaternion rotFinal = vaga.pontoDeAtracagem.rotation;

        float timerEntrada = 0f;
        // Timeout longo (60s) para não teleportar se estiver lento
        while ((Vector3.Distance(navio.transform.position, posFinal) > 0.05f || Quaternion.Angle(navio.transform.rotation, rotFinal) > 0.5f) && timerEntrada < 60f)
        {
            navio.transform.position = Vector3.MoveTowards(navio.transform.position, posFinal, velocidadeManobra * Time.deltaTime);
            navio.transform.rotation = Quaternion.RotateTowards(navio.transform.rotation, rotFinal, 15f * Time.deltaTime);
            timerEntrada += Time.deltaTime;
            yield return null;
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
        if (vaga.navioOcupante == null) return;
        IdentidadeNaval navio = vaga.navioOcupante;
        if (saidaDestino == null) saidaDestino = GetSaidaMaisProxima(navio.transform.position);

        if (saidaDestino != null) navio.SairDaDoca(saidaDestino.position);
        else navio.SairDaDoca(navio.transform.position - (navio.transform.forward * 50f));

        vaga.navioOcupante = null; 
        vaga.atracagemCompleta = false;
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
        IdentidadeNaval[] todosNavios = FindObjectsOfType<IdentidadeNaval>();
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

    public void ConstruirNavio(GameObject prefabNavio)
    {
        if (prefabNavio == null) return;
        Transform pontoSpawn = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0) pontoSpawn = pontosDeSaida[0];
        GameObject novoNavio = Instantiate(prefabNavio, pontoSpawn.position, pontoSpawn.rotation);
        IdentidadeNaval id = novoNavio.GetComponent<IdentidadeNaval>();
        if (id != null)
        {
            if(pontosDeSaida != null && pontosDeSaida.Length > 1) id.MoverPara(pontosDeSaida[1].position); 
            else id.MoverPara(transform.position + transform.forward * 100f);
        }
    }

    // --- INTERFACE VISUAL DE REPARO ---
    // --- INTERFACE VISUAL DE REPARO ---
    void OnGUI()
    {
        if (Camera.main == null) return;

        foreach (var vaga in vagasDisponiveis)
        {
            // MOSTRA SEMPRE QUE ATRACADO (Mesmo 100%)
            if (vaga.navioOcupante != null && vaga.atracagemCompleta)
            {
                // Pega posição do navio na tela
                Vector3 posMundo = vaga.navioOcupante.transform.position + Vector3.up * 8f; 
                Vector3 screenPos = Camera.main.WorldToScreenPoint(posMundo);
                if (screenPos.z < 0) continue;

                float y = Screen.height - screenPos.y - 120f; 

                float pctVida = 1f;
                int municao = 0, munMax = 0;

                SistemaDeDanos vida = vaga.navioOcupante.GetComponent<SistemaDeDanos>();
                if (vida != null) pctVida = vida.vidaAtual / vida.vidaMaxima;

                LancadorNaval lancador = vaga.navioOcupante.GetComponentInChildren<LancadorNaval>();
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
        }
    }
}
