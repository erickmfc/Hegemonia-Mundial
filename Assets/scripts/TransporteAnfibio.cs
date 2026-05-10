using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TransporteAnfibio : MonoBehaviour
{
    [Header("Componentes da Nave")]
    public Transform portaTraseira; // Rampa (BackDoor)
    public Transform pontoDeEntrada; // Na água/terra (FRENTE da rampa)
    public Transform pontoDeArmazenamento; // DENTRO da garagem (FUNDO)
    public Transform pontoDeDecolagem; // NO CONVÉS (Para aviões/helis)
    
    [Header("Configuração da Porta")]
    public float anguloFechado = 0f;
    public float anguloAberto = 93.37f; // Ajuste Positivo conforme pedido
    public float velocidadePorta = 2.0f;
    
    // Variáveis para manter a porta alinhada com o navio (Y e Z originais)
    // Isso impede que a porta gire ao contrário ou entre na fuselagem
    private float portaRotY, portaRotZ; 

    [Header("Capacidade")]
    public float raioDeCaptura = 60f; 
    public List<GameObject> unidadesGuardadas = new List<GameObject>();
    public List<GameObject> unidadesNaFila = new List<GameObject>();

    [Header("Interface (Menu 'O')")]
    public GameObject prefabMenuCarga; // VAZIO = Usa menu padrão automático
    private bool menuAberto = false;
    private ControleUnidade controleUnidadeCache;
    private GUISkin skinMenuCache;
    private GUIStyle estiloTituloMenu;
    private GUIStyle estiloTextoMenu;
    private GUIStyle estiloBotaoMenu;
    private Collider[] bufferCaptura = new Collider[128];
    private readonly HashSet<IdentidadeUnidade> processadosCaptura = new HashSet<IdentidadeUnidade>(128);

    // Estados
    private enum Estado { Navegando, AbrindoParaEmbarque, Embarcando, Fechando, NavegandoParaTerra, Desembarcando }
    private Estado estadoAtual = Estado.Navegando;
    
    void Start()
    {
        controleUnidadeCache = GetComponent<ControleUnidade>();

        // 1. CAPTURA CRÍTICA DE ROTAÇÃO
        // Salva com qual ângulo a porta começou (geralmente Y=-180 ou Y=0)
        // Para nunca perder essa referência durante a animação
        if (portaTraseira != null)
        {
            portaRotY = portaTraseira.localEulerAngles.y;
            portaRotZ = portaTraseira.localEulerAngles.z;
        }

        // Se não tiver ponto de decolagem, cria um no teto (chute)
        if (pontoDeDecolagem == null)
        {
            GameObject p3 = new GameObject("Ponto_Deck");
            p3.transform.SetParent(transform);
            p3.transform.localPosition = new Vector3(0, 15f, 0); // Alto
            pontoDeDecolagem = p3.transform;
        }
    }

    void Update()
    {
        // Só processa comandos se estiver selecionado
        if (controleUnidadeCache != null && controleUnidadeCache.selecionado)
        {
            if (Input.GetKeyDown(KeyCode.O)) AlternarMenuCarga();
            
            // NOVOS CONTROLES:
            if (Input.GetKeyDown(KeyCode.U)) IniciarEmbarque(); // U = EMBARCAR
            if (Input.GetKeyDown(KeyCode.P)) CicloFecharDesembarcar(); // P = FECHAR/DESEMBARCAR
        }
        
        AnimarPorta();
    }

    // --- LÓGICA DE LANÇAMENTO AÉREO ---
    public void LancarUnidadeAerea(GameObject unidade)
    {
        if (!unidadesGuardadas.Contains(unidade)) return;

        Debug.Log($"🛫 Lançando {unidade.name} do convés!");
        unidadesGuardadas.Remove(unidade);
        
        unidade.transform.position = pontoDeDecolagem.position;
        unidade.transform.rotation = transform.rotation;
        unidade.SetActive(true);

        Helicoptero heli = unidade.GetComponent<Helicoptero>();
        if (heli != null)
        {
            heli.Decolar(pontoDeDecolagem.position + (Vector3.up * 20f) + (transform.forward * 30f));
        }
    }

    // --- NOVO SISTEMA DE CONTROLE ---
    void IniciarEmbarque()
    {
        if (estadoAtual != Estado.Navegando && estadoAtual != Estado.Fechando)
        {
            Debug.LogWarning("⚠️ [Transporte] Aguarde o ciclo atual terminar!");
            return;
        }
        
        Debug.Log("🚢 [Transporte] ABRINDO RAMPA - Chamando Unidades [U]...");
        estadoAtual = Estado.AbrindoParaEmbarque;
        StartCoroutine(RotinaAbrirEEmbarcar());
    }
    
    void CicloFecharDesembarcar()
    {
        switch (estadoAtual)
        {
            case Estado.Embarcando:
            case Estado.AbrindoParaEmbarque:
                Debug.Log("🚢 [Transporte] FECHANDO RAMPA [P]...");
                estadoAtual = Estado.Fechando;
                StartCoroutine(RotinaFechar());
                break;
            
            case Estado.Fechando:
            case Estado.Navegando:
                if (unidadesGuardadas.Count == 0)
                {
                    Debug.LogWarning("⚠️ [Transporte] Nenhuma unidade para desembarcar!");
                    return;
                }
                Debug.Log("🚢 [Transporte] INICIANDO DESEMBARQUE ANFÍBIO [P]!");
                estadoAtual = Estado.NavegandoParaTerra;
                StartCoroutine(RotinaAtracarEDesembarcar());
                break;
                
            default:
                Debug.LogWarning($"⚠️ [Transporte] Não pode desembarcar agora. Estado: {estadoAtual}");
                break;
        }
    }

    void AnimarPorta()
    {
        if (portaTraseira == null) return;

        // Define o ângulo alvo APENAS para o eixo X (Vermelho)
        float metaAnguloX = (estadoAtual == Estado.AbrindoParaEmbarque || 
                            estadoAtual == Estado.Embarcando || 
                            estadoAtual == Estado.Desembarcando) ? anguloAberto : anguloFechado;

        // Constrói a rotação alvo mantendo Y e Z originais
        Quaternion alvo = Quaternion.Euler(metaAnguloX, portaRotY, portaRotZ); 
        
        // Aplica suavemente
        portaTraseira.localRotation = Quaternion.Slerp(portaTraseira.localRotation, alvo, Time.deltaTime * velocidadePorta);
    }

    IEnumerator RotinaAbrirEEmbarcar()
    {
        yield return new WaitForSeconds(2.0f);
        estadoAtual = Estado.Embarcando;

        // USA OverlapSphere PARA ENCONTRAR UNIDADES PRÓXIMAS
        int totalHits = CapturarCollidersProximos();
        
        // Limpa fila antiga para recalcular quem está perto e válido agorar
        unidadesNaFila.Clear(); 
        
        // Lista auxiliar de Identidades já processadas para evitar duplicatas (vários colliders na mesma unidade)
        processadosCaptura.Clear();

        for (int i = 0; i < totalHits; i++)
        {
            Collider hit = bufferCaptura[i];
            if (hit == null) continue;

            // Pega a unidade real (quem tem a identidade), subindo a hierarquia a partir do colisor
            IdentidadeUnidade id = hit.GetComponentInParent<IdentidadeUnidade>();
            
            // Se não tem identidade, ignora (pode ser cenário, chão, etc)
            if (id == null) continue;

            // Se já processamos essa identidade (outro collider do mesmo tanque), pula
            if (processadosCaptura.Contains(id)) continue;
            processadosCaptura.Add(id);

            GameObject unidadeObj = id.gameObject;

            // Filtro: Não pegar a si mesmo
            if (unidadeObj == gameObject) continue;

            // ========== FILTRAGEM ANTI-NAVIO (TRIPLA PROTEÇÃO) ==========
            
            // 1. PROTEÇÃO POR COMPONENTE (Detecta scripts de navio na unidade)
            // Se tiver qualquer script naval, ignora. Transporte não carrega navio.
            if (unidadeObj.GetComponent<MovimentoNaval>() != null || 
                unidadeObj.GetComponent<ControladorNavioVigilante>() != null ||
                unidadeObj.GetComponent<TransporteAnfibio>() != null)
            {
                continue;
            }

            // 2. PROTEÇÃO POR IDENTIDADE (Time e Tipo)
            if (id.teamID != 1) 
            {
                // Ignora inimigos ou neutros
                continue;
            }

            // CRÍTICO: FILTRO DE TIPO - SÓ TERRESTRES E HELIS
            // Ignora outros navios e construções
            if (id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Estrutura)
            {
                // Debug.Log($"🚫 [Transporte] Ignorando {unidadeObj.name}: Tipo inválido ({id.tipoUnidade})");
                continue;
            }

            // 3. PROTEÇÃO POR NOME (Fallback final)
            string nomeUnidade = unidadeObj.name;
            if (nomeUnidade.IndexOf("uss ", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                nomeUnidade.IndexOf("navio", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                nomeUnidade.IndexOf("ship", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogWarning($"⚠️ [Transporte] Ignorando {unidadeObj.name} pelo nome (parece navio).");
                continue;
            }

            // Opcional: Se já estiver inativo (embarcado/morto), ignora
            if (!unidadeObj.activeInHierarchy) continue;
            
            // ACEITA APENAS: Infantaria, Veiculo (tanks), Aereo (helis)
            if (id.tipoUnidade == TipoUnidade.Infantaria || 
                id.tipoUnidade == TipoUnidade.Veiculo || 
                id.tipoUnidade == TipoUnidade.Aereo)
            {
                unidadesNaFila.Add(unidadeObj);
                Debug.Log($"✅ [Transporte] Convocando: {unidadeObj.name} (Tipo: {id.tipoUnidade})");
            }
        }

        if (unidadesNaFila.Count == 0)
        {
            Debug.LogWarning("[Transporte] Nenhuma unidade válida encontrada nas proximidades para embarcar.");
        }

        // Processa a fila de embarque UM POR UM
        foreach (GameObject unidade in unidadesNaFila)
        {
            if (unidade == null || !unidade.activeInHierarchy) continue;
            
            ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
            if (controle != null)
            {
                controle.EmitirOrdemMover(pontoDeEntrada.position);
            }
            else
            {
            // Move para a entrada
                NavMeshAgent nav = unidade.GetComponent<NavMeshAgent>();
                if (nav != null && nav.isActiveAndEnabled) 
                { 
                     if(nav.isOnNavMesh)
                     {
                        nav.isStopped = false; 
                        MovimentoFallbackTransicional.TrySetNavDestination(unidade, pontoDeEntrada.position);
                     }
                     else
                     {
                         Debug.LogWarning($"[Transporte] Unidade {unidade.name} tem NavMeshAgent mas não está no NavMesh. Tentando Warp...");
                         if(nav.Warp(unidade.transform.position))
                         {
                             MovimentoFallbackTransicional.TrySetNavDestination(unidade, pontoDeEntrada.position);
                         }
                     }
                }
                else
                {
                     // Tenta mover helis por comando
                     Helicoptero heli = unidade.GetComponent<Helicoptero>();
                     if(heli) heli.Decolar(pontoDeEntrada.position);
                }
            }

            yield return new WaitForSeconds(0.5f);
            
            // Espera chegar (Timeout de 12s para dar tempo)
            float timer = 0f;
            while (unidade != null && Vector3.Distance(unidade.transform.position, pontoDeEntrada.position) > 10f && timer < 12f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (unidade != null)
            {
                // Suga para dentro
                unidade.SetActive(false);
                unidadesGuardadas.Add(unidade);

                // Recarregar vida e combustível
                SistemaDeDanos dano = unidade.GetComponent<SistemaDeDanos>();
                if (dano != null) dano.vidaAtual = dano.vidaMaxima;
                CombustivelUnidade comb = unidade.GetComponent<CombustivelUnidade>();
                if (comb != null) comb.PreencherSemCusto();
            }
        }
        
        Debug.Log("[Transporte] Embarque finalizado. Pressione P para fechar a porta.");
    }

    IEnumerator RotinaFechar()
    {
        yield return new WaitForSeconds(3.0f);
        estadoAtual = Estado.Navegando;
    }

    IEnumerator RotinaAtracarEDesembarcar()
    {
        // 1. PARA O NAVIO (Desabilita NavMeshAgent se tiver)
        NavMeshAgent navioAgent = GetComponent<NavMeshAgent>();
        bool navioEstavaSeProcurando = false;
        if (navioAgent != null && navioAgent.enabled)
        {
            navioEstavaSeProcurando = true;
            navioAgent.isStopped = true;
            navioAgent.ResetPath();
        }
        
        // 2. Acha Praia
        bool achouPraia = false;
        Vector3 pontoPraia = Vector3.zero;
        
        // Raio cast mais agressivo (de cima pra baixo e inclinado)
        for (float i = 15; i < 250; i += 10)
        {
            Vector3 origem = transform.position + (transform.forward * i) + (Vector3.up * 80);
            if (Physics.Raycast(origem, Vector3.down, out RaycastHit hit, 200f))
            {
                // Se não for água (Layer Default ou Terrain) e altura > 0
                if (hit.point.y > 0.1f) 
                { 
                    achouPraia = true; 
                    pontoPraia = hit.point; 
                    Debug.Log("🏖️ Praia detectada em: " + hit.point);
                    break; 
                }
            }
        }

        Vector3 posicaoFinalNavio = transform.position; // Posição onde vai parar
        
        if (achouPraia)
        {
            // Navega até 35m da praia
            Vector3 alvoNavegacao = pontoPraia - (transform.forward * 35f); 
            // Mantém Y do navio (nível do mar)
            alvoNavegacao.y = transform.position.y;

            float t = 0;
            Vector3 start = transform.position;
            while(t < 1f) 
            { 
                t += Time.deltaTime * 0.15f; // Lento
                transform.position = Vector3.Lerp(start, alvoNavegacao, t); 
                yield return null; 
            }
            posicaoFinalNavio = alvoNavegacao;
        }
        else
        {
            Debug.LogWarning("⚠️ Nenhuma praia encontrada! Desembarcando na posição atual.");
            posicaoFinalNavio = transform.position;
        }

        // 3. TRAVA O NAVIO NA POSIÇÃO DURANTE DESEMBARQUE
        estadoAtual = Estado.Desembarcando;
        Debug.Log("⚓ [Transporte] Navio ANCORADO - Iniciando desembarque...");
        yield return new WaitForSeconds(2f); 

        // 4. Solta TERRESTRES EM POSIÇÕES VÁLIDAS DO NAVMESH
        List<GameObject> paraRemover = new List<GameObject>();
        
        foreach (GameObject unidade in unidadesGuardadas)
        {
            if (unidade == null) continue;
            
            // Se for Avião/Heli, NÃO solta na praia
            if (unidade.GetComponent<Helicoptero>() != null) continue;

            // FORÇA NAVIO A FICAR PARADO
            transform.position = posicaoFinalNavio;
            
            // === NOVA LÓGICA: PROCURA POSIÇÃO VÁLIDA NO NAVMESH ===
            Vector3 posicaoAlvo;
            bool achouPosicaoValida = false;
            
            // Tenta várias posições em frente ao navio (na praia)
            for (float distancia = 10f; distancia < 50f; distancia += 5f)
            {
                Vector3 pontoTeste = pontoDeEntrada.position + (transform.forward * distancia);
                
                // Tenta achar NavMesh próximo
                if (NavMesh.SamplePosition(pontoTeste, out NavMeshHit hitNav, 10f, NavMesh.AllAreas))
                {
                    posicaoAlvo = hitNav.position;
                    achouPosicaoValida = true;
                    
                    // Ativa unidade
                    unidade.transform.position = posicaoAlvo;
                    unidade.transform.rotation = transform.rotation;
                    unidade.SetActive(true);
                    
                    // Configura NavMeshAgent
                    NavMeshAgent nav = unidade.GetComponent<NavMeshAgent>();
                    if(nav) 
                    { 
                        nav.enabled = false; // Desliga temporariamente
                        yield return null;    // Espera 1 frame
                        nav.enabled = true;   // Religa
                        nav.Warp(posicaoAlvo);
                        
                        // Manda sair mais para frente
                        Vector3 destinoFinal = posicaoAlvo + (transform.forward * 20f);
                        if (NavMesh.SamplePosition(destinoFinal, out NavMeshHit hitDestino, 5f, NavMesh.AllAreas))
                        {
                            ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
                            if (controle != null)
                            {
                                controle.EmitirOrdemMover(hitDestino.position);
                            }
                            else
                            {
                                MovimentoFallbackTransicional.TrySetNavDestination(unidade, hitDestino.position);
                            }
                        }
                        
                        Debug.Log($"✅ Desembarcado: {unidade.name} em posição válida do NavMesh");
                    }
                    
                    paraRemover.Add(unidade);
                    break; // Achou posição válida, sai do loop
                }
            }
            
            // Se NÃO achou posição válida, avisa
            if (!achouPosicaoValida)
            {
                Debug.LogWarning($"⚠️ [Transporte] Não foi possível encontrar NavMesh para {unidade.name}! Ele ficará a bordo.");
            }
            
            yield return new WaitForSeconds(1.5f);
            
            // MANTÉM NAVIO PARADO
            transform.position = posicaoFinalNavio;
        }
        
        foreach(var r in paraRemover) unidadesGuardadas.Remove(r);

        Debug.Log("✅ [Transporte] Desembarque completo! Fechando rampa...");
        yield return new WaitForSeconds(3.0f);
        
        // 5. LIBERA O NAVIO PARA NAVEGAR NOVAMENTE
        if (navioAgent != null && navioEstavaSeProcurando)
        {
            navioAgent.isStopped = false;
        }
        
        estadoAtual = Estado.Fechando;
    }

    void AlternarMenuCarga() { menuAberto = !menuAberto; }

    private int CapturarCollidersProximos()
    {
        int totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioDeCaptura, bufferCaptura);
        while (totalHits >= bufferCaptura.Length && bufferCaptura.Length < 1024)
        {
            bufferCaptura = new Collider[bufferCaptura.Length * 2];
            totalHits = Physics.OverlapSphereNonAlloc(transform.position, raioDeCaptura, bufferCaptura);
        }

        return totalHits;
    }

    void OnGUI()
    {
        if (!menuAberto) return;
        PrepararEstilosMenuSeNecessario();

        // MENU REDUZIDO (80% do tamanho original) e MOVIDO 10% PARA CIMA (agora em 10% da tela)
        float largura = 200f;  
        float altura = 320f;   
        float posY = Screen.height * 0.10f; // Subiu 10% (era 0.20f)
        float posX = Screen.width - largura + 35f; // Movido 55 para a direita (era - 20, agora -20 + 55 = +35)
        GUI.Box(new Rect(posX, posY, largura, altura), "");
        GUI.Label(new Rect(posX, posY + 10, largura, 25), "📦 MANIFESTO DE CARGA", estiloTituloMenu);

        float y = posY + 40;
        if (unidadesGuardadas.Count == 0)
        {
            GUI.Label(new Rect(posX + 10, y, largura - 20, 18), "Nenhuma unidade a bordo.", estiloTextoMenu);
        }
        else
        {
            int maxLinhas = Mathf.Max(1, Mathf.FloorToInt((altura - 92f) / 20f));
            int limite = Mathf.Min(maxLinhas, unidadesGuardadas.Count);
            for (int i = 0; i < limite; i++)
            {
                GameObject u = unidadesGuardadas[i];
                if (u == null) continue;
                GUI.Label(new Rect(posX + 10, y, 120, 18), CompactarTextoMenu(u.name, 18), estiloTextoMenu);

                bool ehAereo = (u.GetComponent<Helicoptero>() != null);
                
                if (ehAereo)
                {
                    if (GUI.Button(new Rect(posX + largura - 75, y, 65, 18), "DECOLAR", estiloBotaoMenu))
                    {
                        LancarUnidadeAerea(u);
                        break;
                    }
                }
                else
                {
                    GUI.Label(new Rect(posX + largura - 75, y, 65, 18), "[Porão]", estiloTextoMenu);
                }
                y += 20;
            }

            if (unidadesGuardadas.Count > limite)
            {
                GUI.Label(new Rect(posX + 10, y, largura - 20, 18), "+" + (unidadesGuardadas.Count - limite) + " carga(s) a bordo", estiloTextoMenu);
            }
        }

        y = posY + altura - 45;
        GUI.Label(new Rect(posX + 10, y, largura - 20, 18), $"Status: {estadoAtual}", estiloTextoMenu);
        GUI.Label(new Rect(posX + 10, y+20, largura - 20, 18), $"[U] Embarcar  |  [P] Fechar/Sair", estiloTextoMenu);
    }

    private void PrepararEstilosMenuSeNecessario()
    {
        if (skinMenuCache == GUI.skin && estiloTituloMenu != null)
        {
            return;
        }

        skinMenuCache = GUI.skin;
        estiloTituloMenu = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        estiloTextoMenu = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11
        };
        estiloBotaoMenu = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10
        };
    }

    private static string CompactarTextoMenu(string texto, int maxChars)
    {
        if (string.IsNullOrEmpty(texto) || maxChars <= 0 || texto.Length <= maxChars)
        {
            return texto;
        }

        if (maxChars <= 3)
        {
            return texto.Substring(0, maxChars);
        }

        return texto.Substring(0, maxChars - 3) + "...";
    }
}
