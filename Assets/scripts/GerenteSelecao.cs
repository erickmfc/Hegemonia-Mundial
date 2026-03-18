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

    private Vector2 inicioMouseScreen; // Posição pura do mouse na tela
    private bool arrastando = false;

    void Start()
    {
        // Começa desligado e zerado
        if (caixaSelecaoVisual != null)
        {
            caixaSelecaoVisual.gameObject.SetActive(false);
            caixaSelecaoVisual.sizeDelta = Vector2.zero;
        }
    }

    void Update()
    {
        // Se clicar em cima de botões da UI, não faz nada
        if (EventSystem.current.IsPointerOverGameObject())
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
            DesenharLinhasOrdem desenhador = FindFirstObjectByType<DesenharLinhasOrdem>();
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

                Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
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
    void MoverUnidadesEmGrupo(Vector3 destinoCentral, TorreDeControle torreDestino = null)
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
            Vector3 posTela = Camera.main.WorldToScreenPoint(unidade.transform.position);

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

        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        
        if (Physics.Raycast(raio, out toque, Mathf.Infinity, layerMask))
        {
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
        IdentidadeUnidade id = unidade.GetComponent<IdentidadeUnidade>();
        
        if (id != null)
        {
            // Se tem identidade, respeita o time.
            // Team ID 1 = Jogador. Se for diferente, ignora.
            if (id.teamID != 1) return; 
        }
        else
        {
            // --- CORREÇÃO AUTOMÁTICA ---
            // Se a unidade não tem identidade (ex: Hamer recém colocado),
            // assumimos que é do jogador e colocamos o RG nela agora.
            id = unidade.gameObject.AddComponent<IdentidadeUnidade>();
            id.teamID = 1; // Registra como Aliado
            id.nomeDoPais = "Minha Nação";
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
