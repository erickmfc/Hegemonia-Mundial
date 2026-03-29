using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class GerenteSelecao : MonoBehaviour
{
    [Header("Configurações Visuais")]
    public RectTransform caixaSelecaoVisual; // Sua imagem verde
    public RectTransform canvasRect;         // O Pai de todos (Interface/Canvas)

    [Header("Controle")]
    public float espacamento = 2.5f; // Distância entre soldados na formação
    public List<ControleUnidade> unidadesSelecionadas = new List<ControleUnidade>();
    private Camera cameraPrincipal;
    private Construtor construtorCache;
    private DesenharLinhasOrdem desenhadorOrdensCache;

    private Vector2 inicioMouseScreen; // Posição pura do mouse na tela
    private bool arrastando = false;

    void Start()
    {
        cameraPrincipal = Camera.main;
        // Começa desligado e zerado
        if (caixaSelecaoVisual != null)
        {
            caixaSelecaoVisual.gameObject.SetActive(false);
            caixaSelecaoVisual.sizeDelta = Vector2.zero;
        }
    }

    void Update()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        Construtor construtorObj = ObterConstrutor();
        if (construtorObj != null && construtorObj.modoConstrucao)
        {
            arrastando = false;
            if (caixaSelecaoVisual != null)
            {
                caixaSelecaoVisual.gameObject.SetActive(false);
                caixaSelecaoVisual.sizeDelta = Vector2.zero;
            }
            return;
        }
        // Se clicar em cima de botões da UI, não faz nada
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.IsPointerOverGameObject())
        {
            // DEBUG: Se o clique direito foi bloqueado pela UI
            if (Input.GetMouseButtonDown(1))
            {
                Debug.LogWarning("[GerenteSelecao] Clique direito BLOQUEADO por UI (IsPointerOverGameObject=true)");
            }
            return;
        }

        // 1. CLICOU (Marca onde começou)
        if (Input.GetMouseButtonDown(0))
        {
            // ===== CORREÇÃO DEFINITIVA DO BUG DO CONSTRUTOR ABRINDO O MENU SOZINHO =====
            // Se estou segurando um prédio para colocar no chão, o Gerente ignora esse clique (Não arma o arrasto)
            Construtor construtorModoClique = ObterConstrutor();
            if (construtorModoClique != null && construtorModoClique.modoConstrucao)
            {
                return;
            }
            // ==============================================================================

            arrastando = true;
            inicioMouseScreen = Input.mousePosition; 
            DeselecionarTudo();
        }

        // 2. ARRASTANDO (Desenha a caixa)
        if (Input.GetMouseButton(0) && arrastando)
        {
            // Só mostra o verde se moveu um pouco o mouse (evita piscar)
            // Aumentei tolerância para 20 pixels para evitar "arrastar sem querer"
            if(Vector2.Distance(inicioMouseScreen, Input.mousePosition) > 20)
            {
                caixaSelecaoVisual.gameObject.SetActive(true);
            }
            
            if (caixaSelecaoVisual.gameObject.activeSelf)
                AtualizarDesenhoCaixa();
        }

        // 3. SOLTOU (Calcula quem pegou)
        if (Input.GetMouseButtonUp(0))
        {
            // Se "arrastando" for FALSO, significa que o MouseDown foi cancelado (ex: pelo Construtor colocando a Fábrica).
            // Portanto, o MouseUp deve ser ignorado para evitar que chame o CliqueSimples() numa Fábrica recém plantada!
            if (!arrastando) return; 

            if (arrastando && caixaSelecaoVisual.gameObject.activeSelf)
            {
                SelecionarUnidadesMatematica();
            }
            else
            {
                // Clique Simples (Sem arrastar)
                CliqueSimples();
            }

            // Limpeza
            arrastando = false;
            // Desativa imediatamente para não ficar visualmente preso
            if(caixaSelecaoVisual != null)
                caixaSelecaoVisual.gameObject.SetActive(false);
        }

        // 4. MOVIMENTO EM GRUPO (Botão Direito)
        if (Input.GetMouseButtonDown(1))
        {
            // --- CONEXÃO COM SISTEMA DE ORDENS (PATRULHA/SEGUIR) ---
            DesenharLinhasOrdem desenhador = ObterDesenhadorOrdens();
            if (desenhador != null && (desenhador.modoPatrulhaAtivo || desenhador.modoSeguirAtivo))
            {
                return; // Ignora o movimento padrão se estiver gravando patrulha ou seguir
            }
            // -----------------------------------------------------

            if(unidadesSelecionadas.Count > 0)
            {
                // Usa LayerMask para ignorar Triggers, UI, IgnoreRaycast (2) etc.
                // Default (0), Water (4), Terrain (8) etc.
                // Mas queremos ignorar IgnoreRaycast (2).
                int layerMaskMove = ~(1 << 2); 

                if (cameraPrincipal == null) return;
                Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                Vector3 destino = Vector3.zero;
                bool encontrouDestino = false;

                // Tenta acertar um Collider primeiro (terreno, prédios, etc.)
                if (Physics.Raycast(raio, out hit, Mathf.Infinity, layerMaskMove))
                {
                    destino = hit.point;
                    encontrouDestino = true;
                }
                else
                {
                    // FALLBACK: Calcula interseção com o plano da água (Y = 0)
                    // Isso garante que cliques sobre a água (que não tem Collider) funcionem!
                    UnityEngine.Plane planoAgua = new UnityEngine.Plane(Vector3.up, Vector3.zero); // Plano horizontal em Y=0
                    float distancia;
                    if (planoAgua.Raycast(raio, out distancia))
                    {
                        destino = raio.GetPoint(distancia);
                        encontrouDestino = true;
                    }
                }

                if (encontrouDestino)
                {
                    MostrarMarcadorDestino(destino);

                    // VERIFICA SE CLICOU EM UM AEROPORTO PARA OS AVIÕES POUSAREM (Abastecimento Manual)
                    TorreDeControle torre = null;
                    if (hit.collider != null)
                    {
                         torre = hit.collider.GetComponentInParent<TorreDeControle>();
                    }

                    MoverUnidadesEmGrupo(destino, torre);
                }
            }
        }
    }

    Construtor ObterConstrutor()
    {
        if (construtorCache == null)
        {
            construtorCache = FindFirstObjectByType<Construtor>();
        }

        return construtorCache;
    }

    DesenharLinhasOrdem ObterDesenhadorOrdens()
    {
        if (desenhadorOrdensCache == null)
        {
            desenhadorOrdensCache = FindFirstObjectByType<DesenharLinhasOrdem>();
        }

        return desenhadorOrdensCache;
    }

    // --- MARCADOR VISUAL DO CLIQUE ---
    void MostrarMarcadorDestino(Vector3 pos)
    {
        GameObject marcador = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(marcador.GetComponent<Collider>());
        marcador.transform.position = pos + Vector3.up * 0.1f;
        marcador.transform.localScale = new Vector3(2f, 0.05f, 2f);
        
        Renderer r = marcador.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Sprites/Default"));
        r.material.color = new Color(0f, 1f, 0.5f, 0.6f); // Verde neon
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        marcador.AddComponent<AnimadorMarcador>();
    }

    public class AnimadorMarcador : MonoBehaviour
    {
        float tempo = 0;
        void Update()
        {
            tempo += Time.deltaTime * 3f;
            transform.localScale = Vector3.Lerp(new Vector3(2f, 0.05f, 2f), Vector3.zero, tempo);
            if (tempo >= 1f) Destroy(gameObject);
        }
    }

    // --- NOVA LÓGICA DE FORMAÇÃO TÁTICA (MISTA) ---
    void MoverUnidadesEmGrupo_OLD(Vector3 destinoCentral, TorreDeControle torreDestino = null)
    {
        unidadesSelecionadas.RemoveAll(u => u == null);
        int totalOriginal = unidadesSelecionadas.Count;
        if (totalOriginal == 0) return;

        // 1. Classifica o esquadrão taticamente
        bool ehGrupoNaval = false;
        bool temVeiculo = false;

        List<ControleUnidade> infantaria = new List<ControleUnidade>();
        List<ControleUnidade> veiculos = new List<ControleUnidade>();
        List<ControleUnidade> aereos = new List<ControleUnidade>();

        foreach (var u in unidadesSelecionadas)
        {
            // Checagem Aérea (Não entra na grade limitante de contato físico)
            ControleAviao aviao = u.GetComponent<ControleAviao>();
            Helicoptero heli = u.GetComponent<Helicoptero>();
            
            if (aviao != null)
            {
                if (torreDestino != null)
                {
                    aviao.ComandoRetornarBase();
                    Debug.Log($"[GerenteSelecao] Selecionou Retornar pra Base via RMB! ({u.name})");
                }
                else
                {
                    u.MoverParaPonto(destinoCentral);
                }
                continue; // Avião resolvido
            }
            if (heli != null)
            {
                // Helicópteros ganham pequenos offsets individuais no ar para não se amalgamarem
                Vector3 deslocHeli = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                heli.Decolar(destinoCentral + deslocHeli);
                continue; // Helicóptero resolvido
            }

            // Checagem Naval
            if (u.GetComponent<IdentidadeNaval>() != null || 
                u.GetComponent<ControleSubmarino>() != null ||
                u.GetComponent<NavegacaoInteligenteNaval>() != null)
            {
                ehGrupoNaval = true;
            }

            // Checagem Terrestre (Tanque x Soldado)
            string n = u.name.ToLower();
            if (n.Contains("tank") || n.Contains("tanque") || n.Contains("blindado") || n.Contains("hammer") || n.Contains("humvee") || n.Contains("lancador"))
            {
                temVeiculo = true;
                veiculos.Add(u);
            }
            else
            {
                infantaria.Add(u);
            }
        }

        // Define espaçamento dinâmico: Se tiver mista (1 tanque e 10 soldados), exige grade larga pra evitar atrito
        float espacamentoReal = ehGrupoNaval ? 30.0f : (temVeiculo ? 7.0f : espacamento); 

        // 2. Ordena o pelotão: Tanques e suporte atrás, infantaria na linha de frente
        List<ControleUnidade> gridUnidades = new List<ControleUnidade>();
        gridUnidades.AddRange(veiculos);     // Índice Baixo da grade (Trás)
        gridUnidades.AddRange(infantaria);   // Índice Alto da grade (Frente)

        int totalGrade = gridUnidades.Count;
        if (totalGrade == 0) return;

        // 3. Calcula centro do grupo para direção tática de virada
        Vector3 centroGrupo = Vector3.zero;
        foreach (var u in gridUnidades) centroGrupo += u.transform.position;
        centroGrupo /= totalGrade;

        Vector3 direcaoMovimento = (destinoCentral - centroGrupo).normalized;
        if (direcaoMovimento == Vector3.zero) direcaoMovimento = Vector3.forward;
        Quaternion rotacaoFormacao = Quaternion.LookRotation(direcaoMovimento);

        // 4. Desenha Formação Geométrica
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(totalGrade));
        float larguraTotal = (colunas - 1) * espacamentoReal;
        float profundidadeTotal = (Mathf.CeilToInt((float)totalGrade / colunas) - 1) * espacamentoReal;
        Vector3 offsetCentral = new Vector3(-larguraTotal / 2f, 0, -profundidadeTotal / 2f);

        for (int i = 0; i < totalGrade; i++)
        {
            ControleUnidade alvoCtrl = gridUnidades[i];

            int x = i % colunas;
            int z = i / colunas; // z é a linha (z alto = frente da base)

            Vector3 posLocalGrade = offsetCentral + new Vector3(x * espacamentoReal, 0, z * espacamentoReal);
            Vector3 offsetRodado = rotacaoFormacao * posLocalGrade;

            // O ponto alvo final individual
            Vector3 posAlvo = destinoCentral + offsetRodado;

            // BLOQUEIO MANUAL
            LancadorNaval lancador = alvoCtrl.GetComponent<LancadorNaval>();
            if (lancador != null && lancador.modoAtual == LancadorNaval.ModoOperacao.Manual)
                continue; 

            // NAVMESH PREDICTION: Ajuda navios em terreno acidentado / margens
            if (ehGrupoNaval)
            {
                 UnityEngine.AI.NavMeshHit hit;
                 if (UnityEngine.AI.NavMesh.SamplePosition(posAlvo, out hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                 {
                     posAlvo = hit.position;
                 }
            }

            // Envia Comando
            alvoCtrl.MoverParaPonto(posAlvo);
        }
    }

    private struct SlotFormacao
    {
        public ControleUnidade unidade;
        public float largura;
        public float profundidade;
    }

    // Formacao considerando tamanho real (BoxCollider/NavMeshAgent)
    void MoverUnidadesEmGrupo(Vector3 destinoCentral, TorreDeControle torreDestino = null)
    {
        unidadesSelecionadas.RemoveAll(u => u == null);
        if (unidadesSelecionadas.Count == 0) return;

        bool ehGrupoNaval = false;
        bool temVeiculo = false;

        List<ControleUnidade> infantaria = new List<ControleUnidade>();
        List<ControleUnidade> veiculos = new List<ControleUnidade>();

        foreach (var unidade in unidadesSelecionadas)
        {
            if (unidade == null) continue;

            ControleAviao aviao = unidade.GetComponent<ControleAviao>();
            Helicoptero heli = unidade.GetComponent<Helicoptero>();

            if (aviao != null)
            {
                if (torreDestino != null)
                {
                    aviao.ComandoRetornarBase();
                    Debug.Log($"[GerenteSelecao] Selecionou Retornar pra Base via RMB! ({unidade.name})");
                }
                else
                {
                    unidade.MoverParaPonto(destinoCentral);
                }
                continue;
            }

            if (heli != null)
            {
                Vector3 deslocHeli = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                heli.Decolar(destinoCentral + deslocHeli);
                continue;
            }

            if (unidade.GetComponent<IdentidadeNaval>() != null ||
                unidade.GetComponent<ControleSubmarino>() != null ||
                unidade.GetComponent<NavegacaoInteligenteNaval>() != null)
            {
                ehGrupoNaval = true;
            }

            string nome = unidade.name.ToLower();
            bool eVeiculo = nome.Contains("tank") || nome.Contains("tanque") || nome.Contains("blindado") ||
                            nome.Contains("hammer") || nome.Contains("humvee") || nome.Contains("lancador");

            if (eVeiculo)
            {
                temVeiculo = true;
                veiculos.Add(unidade);
            }
            else
            {
                infantaria.Add(unidade);
            }
        }

        List<ControleUnidade> ordemFormacao = new List<ControleUnidade>();
        ordemFormacao.AddRange(veiculos);   // Traseira
        ordemFormacao.AddRange(infantaria); // Frente

        int total = ordemFormacao.Count;
        if (total == 0) return;

        Vector3 centroGrupo = Vector3.zero;
        foreach (var u in ordemFormacao) centroGrupo += u.transform.position;
        centroGrupo /= total;

        Vector3 direcaoMovimento = (destinoCentral - centroGrupo).normalized;
        if (direcaoMovimento == Vector3.zero) direcaoMovimento = Vector3.forward;
        Quaternion rotacaoFormacao = Quaternion.LookRotation(direcaoMovimento);

        int colunas = Mathf.CeilToInt(Mathf.Sqrt(total));
        if (temVeiculo) colunas = Mathf.Clamp(colunas, 2, 6);
        int linhas = Mathf.CeilToInt((float)total / colunas);

        List<SlotFormacao> slots = new List<SlotFormacao>(total);
        float somaLargura = 0f;
        float somaProfundidade = 0f;

        for (int i = 0; i < total; i++)
        {
            float largura;
            float profundidade;
            ObterPegadaUnidade(ordemFormacao[i], out largura, out profundidade);

            if (ehGrupoNaval)
            {
                largura *= 1.25f;
                profundidade *= 1.25f;
            }

            slots.Add(new SlotFormacao
            {
                unidade = ordemFormacao[i],
                largura = largura,
                profundidade = profundidade
            });

            somaLargura += largura;
            somaProfundidade += profundidade;
        }

        float mediaLargura = Mathf.Max(1f, somaLargura / total);
        float mediaProfundidade = Mathf.Max(1f, somaProfundidade / total);

        float gapX = ehGrupoNaval ? Mathf.Max(6f, mediaLargura * 0.30f) : Mathf.Max(temVeiculo ? 1.7f : 1.0f, mediaLargura * 0.18f);
        float gapZ = ehGrupoNaval ? Mathf.Max(8f, mediaProfundidade * 0.35f) : Mathf.Max(temVeiculo ? 2.2f : 1.2f, mediaProfundidade * 0.22f);

        float[] larguraColuna = new float[colunas];
        float[] profundidadeLinha = new float[linhas];

        for (int i = 0; i < slots.Count; i++)
        {
            int coluna = i % colunas;
            int linha = i / colunas;
            larguraColuna[coluna] = Mathf.Max(larguraColuna[coluna], slots[i].largura);
            profundidadeLinha[linha] = Mathf.Max(profundidadeLinha[linha], slots[i].profundidade);
        }

        float larguraTotal = 0f;
        for (int c = 0; c < colunas; c++) larguraTotal += larguraColuna[c];
        larguraTotal += Mathf.Max(0, colunas - 1) * gapX;

        float profundidadeTotal = 0f;
        for (int l = 0; l < linhas; l++) profundidadeTotal += profundidadeLinha[l];
        profundidadeTotal += Mathf.Max(0, linhas - 1) * gapZ;

        float[] centroColuna = new float[colunas];
        float[] centroLinha = new float[linhas];

        float cursorX = -larguraTotal * 0.5f;
        for (int c = 0; c < colunas; c++)
        {
            centroColuna[c] = cursorX + (larguraColuna[c] * 0.5f);
            cursorX += larguraColuna[c] + gapX;
        }

        float cursorZ = -profundidadeTotal * 0.5f;
        for (int l = 0; l < linhas; l++)
        {
            centroLinha[l] = cursorZ + (profundidadeLinha[l] * 0.5f);
            cursorZ += profundidadeLinha[l] + gapZ;
        }

        float raioAmostraNavMesh = ehGrupoNaval ? 20f : (temVeiculo ? 8f : 4f);

        for (int i = 0; i < slots.Count; i++)
        {
            ControleUnidade alvoCtrl = slots[i].unidade;
            if (alvoCtrl == null) continue;

            LancadorNaval lancador = alvoCtrl.GetComponent<LancadorNaval>();
            if (lancador != null && lancador.modoAtual == LancadorNaval.ModoOperacao.Manual)
                continue;

            int coluna = i % colunas;
            int linha = i / colunas;

            Vector3 posLocal = new Vector3(centroColuna[coluna], 0f, centroLinha[linha]);
            Vector3 posAlvo = destinoCentral + (rotacaoFormacao * posLocal);

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(posAlvo, out hit, raioAmostraNavMesh, UnityEngine.AI.NavMesh.AllAreas))
            {
                posAlvo = hit.position;
            }

            alvoCtrl.MoverParaPonto(posAlvo);
        }
    }

    void ObterPegadaUnidade(ControleUnidade unidade, out float largura, out float profundidade)
    {
        float minimo = Mathf.Max(1.0f, espacamento * 0.55f);
        largura = minimo;
        profundidade = minimo;

        if (unidade == null) return;

        bool temBounds = false;
        Bounds bounds = new Bounds(unidade.transform.position, Vector3.zero);

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
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
            largura = Mathf.Max(largura, bounds.size.x);
            profundidade = Mathf.Max(profundidade, bounds.size.z);
        }
        else
        {
            Renderer[] renderers = unidade.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;

                if (!temBounds)
                {
                    bounds = r.bounds;
                    temBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (temBounds)
            {
                largura = Mathf.Max(largura, bounds.size.x * 0.8f);
                profundidade = Mathf.Max(profundidade, bounds.size.z * 0.8f);
            }
        }

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            float diametro = Mathf.Max(minimo, agent.radius * 2f);
            largura = Mathf.Max(largura, diametro);
            profundidade = Mathf.Max(profundidade, diametro);
        }

        largura = Mathf.Clamp(largura, minimo, 45f);
        profundidade = Mathf.Clamp(profundidade, minimo, 45f);
    }

    void AtualizarDesenhoCaixa()
    {
        if (canvasRect == null || caixaSelecaoVisual == null) return;

        Vector2 mouseAtualScreen = Input.mousePosition;

        // --- TRADUÇÃO MOUSE -> CANVAS ---
        Vector2 localInicio;
        Vector2 localAtual;

        // Converte o ponto inicial e o atual para dentro do Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, inicioMouseScreen, null, out localInicio);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouseAtualScreen, null, out localAtual);

        // Calcula o tamanho e posição no Canvas
        Vector2 tamanho = localAtual - localInicio;
        
        caixaSelecaoVisual.sizeDelta = new Vector2(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y));
        
        // Ajusta a posição para que a caixa cresça para qualquer lado (cima/baixo/esq/dir)
        float posX = (tamanho.x < 0) ? localAtual.x : localInicio.x;
        float posY = (tamanho.y < 0) ? localAtual.y : localInicio.y;

        caixaSelecaoVisual.anchoredPosition = new Vector2(posX, posY);
    }

    void SelecionarUnidadesMatematica()
    {
        // Aqui usamos a posição REAL da tela, ignorando o desenho da caixa
        Vector2 mouseFinal = Input.mousePosition;

        float minX = Mathf.Min(inicioMouseScreen.x, mouseFinal.x);
        float maxX = Mathf.Max(inicioMouseScreen.x, mouseFinal.x);
        float minY = Mathf.Min(inicioMouseScreen.y, mouseFinal.y);
        float maxY = Mathf.Max(inicioMouseScreen.y, mouseFinal.y);

        var todasUnidades = FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);

        foreach (var unidade in todasUnidades)
        {
            if (unidade == null || !unidade.enabled) continue; // Ignora unidades desativadas (como soldados dentro de caminhões)

            // Onde o tanque está na tela?
            if (cameraPrincipal == null) cameraPrincipal = Camera.main;
            if (cameraPrincipal == null) continue;
            Vector3 posTela = cameraPrincipal.WorldToScreenPoint(unidade.transform.position);

            if (posTela.x > minX && posTela.x < maxX && 
                posTela.y > minY && posTela.y < maxY)
            {
                AdicionarSelecao(unidade);
            }
        }
    }

    void CliqueSimples()
    {
        // Se usar ~0 (Tudo), pega até triggers que não deveria.
        // Vamos tentar pegar tudo exceto a Ignore Raycast (2).
        int layerMask = ~(1 << 2); 

        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        if (cameraPrincipal == null) return;
        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        
        if (Physics.Raycast(raio, out toque, Mathf.Infinity, layerMask))
        {
            // === NOVO: VERIFICA SE O JOGADOR CLICOU NA FÁBRICA / CONSTRUTOR DE VEÍCULOS ===
            var fabrica = toque.transform.GetComponentInParent<Fabrica>();
            if (fabrica != null)
            {
                var id = fabrica.GetComponentInParent<IdentidadeUnidade>();
                // Certifica se a fábrica pertence ao jogador (TeamID 1)
                if (id == null || id.teamID == 1) 
                {
                    MenuConstrucao menu = Object.FindFirstObjectByType<MenuConstrucao>();
                    if (menu != null)
                    {
                        // Abre o menu na aba do Exército
                        if (!MenuConstrucao.EstaAberto) menu.AlternarMenu(true);
                        menu.FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Exercito);
                        Debug.Log("[GerenteSelecao] Selecionou a Fábrica! Abrindo a aba do Exército.");
                        
                        DeselecionarTudo(); // Solta as tropas se for clicar num prédio
                        return; // Paralisa o código para não selecionar a fábrica como "tropa"
                    }
                }
            }
            // ==============================================================================

            var unidade = toque.transform.GetComponentInParent<ControleUnidade>();
            if (unidade != null) 
            {
                // Se acertou num passageiro (visível mas não clicável), repassa pro caminhão (pai)
                if (!unidade.enabled)
                {
                    var transporte_pai = unidade.transform.parent?.GetComponentInParent<ControleUnidade>();
                    if (transporte_pai != null && transporte_pai.enabled) unidade = transporte_pai;
                    else return; // Se não tem pai ativo, ignora o clique
                }

                AdicionarSelecao(unidade);
                Debug.Log($"[GerenteSelecao] Selecionado: {unidade.name}");
            }
        }
    }

    void AdicionarSelecao(ControleUnidade unidade)
    {
        // VERIFICA SE É DO MEU TIME
        int teamIdRecuperado = -1;
        
        IdentidadeUnidade idU = unidade.GetComponent<IdentidadeUnidade>();
        if (idU != null) teamIdRecuperado = idU.teamID;
        else 
        {
            IdentidadeIA idIA = unidade.GetComponent<IdentidadeIA>();
            if (idIA != null) teamIdRecuperado = idIA.teamID;
        }
        
        if (teamIdRecuperado != -1)
        {
            // Tem uma identidade definida. Se não for 1, ignora.
            if (teamIdRecuperado != 1) return;
        }
        else
        {
            // --- CORREÇÃO AUTOMÁTICA (APENAS SE NÃO TIVER NENHUM SCRIPT DE IDENTIDADE) ---
            idU = unidade.gameObject.AddComponent<IdentidadeUnidade>();
            idU.teamID = 1; // Registra como Aliado
            idU.nomeDoPais = "Minha Nação";
            Debug.Log($"[Sistema] Identidade criada automaticamente para: {unidade.name}");
        }

        unidadesSelecionadas.Add(unidade);
        unidade.DefinirSelecao(true);
    }

    public void DeselecionarTudo()
    {
        foreach (var u in unidadesSelecionadas)
        {
            if (u)
            {
                u.DefinirSelecao(false);
            }
        }
        unidadesSelecionadas.Clear();
    }
}
