using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia recrutamento contínuo e combate ativo (Terra, Ar e MAR).
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição Desejada")]
    public int soldadosDesejados = 12;
    public int tanquesDesejados = 10;
    public int helicopterosDesejados = 4;
    public int naviosDesejados = 3; // Nova meta naval
    public int transportesDesejados = 2; // Meta de caminhões/Jeeps
    public int avioesDesejados = 5; // Limitação estrita do projeto (Max: 5 Tuks ou Caças)

    [Header("Agressividade")]
    public int minimoParaAtacar = 5;
    public float intervaloAtaque = 15f;

    // Listas de Controle
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoNavios = new List<GameObject>(); // Marinha
    private List<GameObject> grupoTransportes = new List<GameObject>(); // Transportes Terrestres
    private List<GameObject> grupoAvioes = new List<GameObject>(); // Caças!
    private List<GameObject> grupoOutros = new List<GameObject>();

    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();
    [SerializeField] private List<Estaleiro> meusEstaleiros = new List<Estaleiro>(); // Lista separada para Estaleiros se tiver script específico
    [SerializeField] private List<Heliporto> meusHeliportos = new List<Heliporto>();
    [SerializeField] private List<GerenciadorAeroporto> meusAeroportos = new List<GerenciadorAeroporto>();

    private float _timerRecrutamento;
    private float _timerAtaque;
    private float _timerReorganizar;
    private float _timerTransporte;
    private string _ultimoStatus = "Iniciando...";

    private bool jaAtacou = false;
    private Vector3 ultimoAlvoPosicao;

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        if (chefe == null) 
        {
             // Tenta achar na cena
             chefe = FindFirstObjectByType<IA_Comandante>();
        }
        if (chefe != null && chefe.cerebroGeneral == null) chefe.cerebroGeneral = this;
    }

    void Update()
    {
        if (chefe == null) return;

        // 1. Recrutamento
        _timerRecrutamento += Time.deltaTime;
        if (_timerRecrutamento >= 2.5f) // DELAY DE SAIDA: O general agora para pra respirar por 2.5s a cada spawn!
        {
            _timerRecrutamento = 0;
            TentarRecrutar();
        }

        // 2. Reorganizar
        _timerReorganizar += Time.deltaTime;
        if (_timerReorganizar >= 5.0f)
        {
            _timerReorganizar = 0;
            LimparMortos();
            if (!jaAtacou) MoverTropasParaPontoDeEncontro();
        }

        // 3. Combate
        _timerAtaque += Time.deltaTime;
        if (_timerAtaque >= intervaloAtaque)
        {
            _timerAtaque = 0;
            AvaliarCombate();
        }

        // 4. Transporte Inteligente
        _timerTransporte += Time.deltaTime;
        if (_timerTransporte >= 3.0f)
        {
            _timerTransporte = 0;
            GerenciarTransportes();
        }
    }

    void GerenciarTransportes()
    {
        Transform alvo = BuscarAlvo();
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        // HELICOPTEROS (Dropship com Flanqueamento Lateral Inteligente - GPS)
        foreach(var hObj in grupoHelis)
        {
            if (hObj == null) continue;
            Helicoptero heli = hObj.GetComponent<Helicoptero>();
            if (heli && !heli.modoCombateAtivo)
            {
                if (heli.TemSoldados())
                {
                    // SEGREDO DE MESTRE: O Helicóptero de transporte SÓ vai pro ataque se tiver CHEIO!
                    // Isso corta o comportamento deles puxarem um ou dois soldados e já sumirem no mapa,
                    // mantendo-os na base agrupando um esquadrão de elite completo.
                    // Adicionamos escape se ele já estiver muito longe da base, nesse caso ok ele ir pro front.
                    bool decolarParaAtaque = heli.TemEspaco() <= 0 || Vector3.Distance(heli.transform.position, centro) > 150f;

                    if (decolarParaAtaque)
                    {
                        if (alvo != null)
                        {
                            Vector3 eixoCentralZ = (alvo.position - centro).normalized;
                            Vector3 direitaMapa = Vector3.Cross(Vector3.up, eixoCentralZ);
                            float direcaoPinça = (heli.GetInstanceID() % 2 == 0) ? 1f : -1f;
                            Random.InitState(heli.GetInstanceID());
                            Vector3 espalhamentoGPS = new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f));
                            
                            // Desembarque seguro: Pousa ANTES de chegar no inimigo (evita linha de fogo direta)
                            Vector3 pontoDeDesembarque = alvo.position - (eixoCentralZ * 120f) + (direitaMapa * 60f * direcaoPinça) + espalhamentoGPS;

                            float distParaAlvo = Vector3.Distance(heli.transform.position, alvo.position);
                            float distParaDestino = Vector2.Distance(
                                new Vector2(heli.transform.position.x, heli.transform.position.z), 
                                new Vector2(pontoDeDesembarque.x, pontoDeDesembarque.z)
                            );

                            if (heli.estaVoando)
                            {
                                if (distParaDestino <= 20f || distParaAlvo <= 90f)
                                {
                                    // Chegou. Pousar e liberar tropas!
                                    heli.OrdemPousoOuDesembarque();
                                }
                                else
                                {
                                    // Continua a viagem e atualiza destino para não errar caso alvo se mova
                                    heli.destino = pontoDeDesembarque;
                                }
                            }
                            else
                            {
                                if (distParaDestino > 20f && distParaAlvo > 90f)
                                {
                                    heli.Decolar(pontoDeDesembarque);
                                }
                                else
                                {
                                    // Tá no chão e perto do destino. Garantir que as tropas saiam!
                                    heli.OrdemPousoOuDesembarque();
                                }
                            }
                        }
                    }
                    else
                    {
                        // Tem alguns soldados, mas não tá cheio, E o general não atacou ainda. Espera e recruta mais!
                        if (!heli.estaVoando && Vector3.Distance(heli.transform.position, centro) < 250f)
                        {
                             heli.ChamarReforcos();
                        }
                    }
                }
                else
                {
                    // Vazio! Retorna direto para a base para buscar mais soldados
                    float distBase = Vector2.Distance(
                        new Vector2(heli.transform.position.x, heli.transform.position.z), 
                        new Vector2(centro.x, centro.z)
                    );

                    if (heli.estaVoando)
                    {
                        if (distBase <= 40f)
                        {
                            // Chegou na base, pousa!
                            heli.OrdemPousoOuDesembarque();
                        }
                        else
                        {
                            // Continua o retorno para a base
                            heli.destino = centro;
                        }
                    }
                    else
                    {
                        if (distBase > 40f)
                        {
                            // No chão após desembarque na guerra -> volta pra base com leve variação de GPS
                            Random.InitState(heli.GetInstanceID() + 2);
                            Vector3 offsetBase = new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f));
                            heli.Decolar(centro + offsetBase);
                        }
                        else
                        {
                            // Na base, no chão -> recruta mais!
                            if (heli.TemEspaco() > 0) heli.ChamarReforcos();
                        }
                    }
                }
            }
        }
        
        // TRANSPORTES NAVAIS
        foreach(var nObj in grupoNavios)
        {
            if (nObj == null) continue;
            HovercraftTransporte hover = nObj.GetComponent<HovercraftTransporte>();
            if (hover)
            {
                if (alvo != null && Vector3.Distance(hover.transform.position, alvo.position) < 90f)
                {
                    if (hover.TemCarga()) hover.IniciarDesembarque();
                }
                else if (Vector3.Distance(hover.transform.position, centro) < 150f)
                {
                    if (hover.TemEspacoLivre() && !jaAtacou) hover.IniciarEmbarque();
                }
            }
        }

        // TRANSPORTES TERRESTRES (CAMINHÕES E JEEPS)
        foreach(var tObj in grupoTransportes)
        {
            if (tObj == null) continue;
            TransporteTerrestre transp = tObj.GetComponent<TransporteTerrestre>();
            if (transp)
            {
                if (transp.TemPassageiros)
                {
                    if (alvo != null && Vector3.Distance(transp.transform.position, alvo.position) < 60f)
                    {
                        // Chegou na zona de guerra, solta a galera
                        transp.DesembarcarTudo();
                    }
                }
                else
                {
                    if (Vector3.Distance(transp.transform.position, centro) > 150f)
                    {
                        // Vazio no front. Volta pra base!
                        transp.GetComponent<ControleUnidade>()?.MoverParaPonto(centro);
                    }
                    else if (!transp.EstaCheio()) 
                    {
                        // Na base. Recolhendo
                        transp.TentarEmbarcar();
                    }
                }
            }
        }
    }

    // =============================================
    // RECRUTAMENTO
    // =============================================
    void TentarRecrutar()
    {
        // LIMITADOR CORTA-GIRO E FILA: Impede a IA de chamar mais gente antes de dar 2.5 segundos do último que saiu!
        // Impede aglomeração e fila engavetando soldados saindo do quartel ou tanques colidindo.
        if (Time.time - _ultimoRecrutamentoReal < 2.5f) return;

        if (chefe.dinheiro < 100) 
        {
            _ultimoStatus = "💰 Sem dinheiro ($" + (int)chefe.dinheiro + ")";
            return; 
        }

        LimparMortos(); // Importante limpar nulos antes de contar

        bool temQuartel = minhasFabricas.Any(f => f.ehQuartel);
        bool temFabricaVeiculos = minhasFabricas.Any(f => !f.ehQuartel && !EhNaval(f));
        bool temEstaleiro = meusEstaleiros.Count > 0 || minhasFabricas.Any(f => EhNaval(f));

        // 1. SOBREVIVÊNCIA BÁSICA (Garante mínimo de defesas)
        if (grupoSoldados.Count < 4 && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado"; return; }
        }
        
        // 2. FORÇA DE CHOQUE BÁSICA (Pelo menos 1 blindado)
        if (grupoTanques.Count < 2)
        {
             if (temFabricaVeiculos)
             {
                 if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU")) { _ultimoStatus = "🚜 Recrutando Tanque Prioritário"; return; }
             }
             else if (grupoTanques.Count == 0)
             {
                 _ultimoStatus = "⚠️ Esperando Fábrica de Veículos!";
             }
        }

        // 3. TRANSPORTES TERRESTRES (Caminhão/Hamer)
        if (grupoTransportes.Count < transportesDesejados && temFabricaVeiculos && chefe.dinheiro > 100)
        {
             if (ComprarUnidade(false, false, false, false, "Caminhao", "Hamer", "Jeep", "Transporte")) { _ultimoStatus = "🚚 Recrutando Transporte"; return; }
        }

        // 4. PREENCHER TROPAS (O resto dos soldados)
        if (grupoSoldados.Count < soldadosDesejados && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado Extra"; return; }
        }

        // 5. PREENCHER TANQUES (O resto dos veículos pesados)
        if (grupoTanques.Count < tanquesDesejados && temFabricaVeiculos)
        {
             if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU", "Panzer")) { _ultimoStatus = "🚜 Recrutando Tanque Secundário"; return; }
        }

        // 6. HELICÓPTEROS
        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 600)
        {
             if (ComprarUnidade(false, false, true, false, "Heli", "Apache", "Cobra", "Falcon", "Ray", "Guincho")) { _ultimoStatus = "🚁 Recrutando Aéreo"; return; }
        }

        // 7. NAVIOS
        if (grupoNavios.Count < naviosDesejados && chefe.dinheiro > 800)
        {
            if (temEstaleiro)
            {
                bool temCarrier = grupoNavios.Any(n => n.name.ToLower().Contains("liberty") || n.name.ToLower().Contains("carrier") || n.name.ToLower().Contains("transporte"));
                if (chefe.dinheiro > 1800 && !temCarrier)
                {
                    if (ComprarUnidade(false, true, false, false, "Liberty", "Prime", "Carrier", "Transporte")) 
                    { _ultimoStatus = "⚓ Construindo Porta-Aviões (Liberty)..."; return; }
                }

                if (ComprarUnidade(false, true, false, false, "Fragata", "Corveta", "Submarino", "Navio", "Barco", "Hovercraft")) 
                { _ultimoStatus = "⚓ Construindo Navio..."; return; }
            }
        }

        // 8. ESQUADRÃO DE CAÇAS (Ataque Aéreo Direto de Aeroporto)
        if (grupoAvioes.Count < avioesDesejados && chefe.dinheiro > 1200 && meusAeroportos.Count > 0)
        {
             if (ComprarUnidade(false, false, false, true, "Caca", "Tuk", "Super", "Jet", "Aviao", "Bombard")) 
             { _ultimoStatus = "✈️ Requisitando Caça Tático de Ataque"; return; }
        }

        // 9. SOBRA DE DINHEIRO (Reforços Aleatórios e Inifinitos)
        if (chefe.dinheiro > 2000)
        {
             if (Random.value > 0.4f && temFabricaVeiculos) ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Blindado");
             else if (temQuartel) ComprarUnidade(true, false, false, false, "Soldado", "Infantaria");
        }
    }

    private float _ultimoRecrutamentoReal = 0f;

    // =============================================
    // RECRUTAMENTO DE CIVIS (MÉTODO PÚBLICO)
    // =============================================
    public void RecrutarTurista()
    {
        // Tenta comprar algo civil
        if (ComprarUnidade(false, false, false, false, "civil", "turista", "caminhonete", "kombi", "onibus"))
        {
            _ultimoStatus = "🤝 Despachando Transporte Civil/Turistas para a fronteira.";
        }
    }

    bool ComprarUnidade(bool requerQuartel, bool ehNaval, bool ehHeli, bool ehAviao, params string[] keywords)
    {
        AtualizarListasDeFabricas(); 

        // Acha fábrica ou estaleiro
        Fabrica fabricaEscolhida = null;
        Estaleiro estaleiroEscolhido = null;
        Heliporto heliportoEscolhido = null;

        if (ehHeli)
        {
            // Pega um Heliporto aleatório ativado e não no 0 (para espalhar as decolagens)
            var helisValidos = meusHeliportos.Where(h => h != null).OrderBy(x => Random.value).ToList();
            if (helisValidos.Count > 0) heliportoEscolhido = helisValidos[0];
            else return false; 
        }
        else if (ehNaval)
        {
            var estsValidos = meusEstaleiros.Where(e => e != null).OrderBy(x => Random.value).ToList();
            if (estsValidos.Count > 0) estaleiroEscolhido = estsValidos[0]; 
            else 
            {
                 var fNav = minhasFabricas.Where(f => f != null && EhNaval(f)).OrderBy(x => Random.value).ToList();
                 if (fNav.Count > 0) fabricaEscolhida = fNav[0];
            }
        }
        else
        {
             // DISTRIBUIR O PESO GIGOSO NA HORA DO TANQUE/SOLDADO SAIR!
             // Achando vários hangares de Veiculo, sorteia UM deles para a porta se abrir.
             var fabsValidas = minhasFabricas.Where(f => f != null && f.ehQuartel == requerQuartel && !EhNaval(f)).OrderBy(x => Random.value).ToList();
             if (fabsValidas.Count > 0) fabricaEscolhida = fabsValidas[0];
        }

        if (fabricaEscolhida == null && estaleiroEscolhido == null && heliportoEscolhido == null) return false;

        // Acha Prefab no Catálogo
        if (MenuConstrucao.catalogoGlobal == null) return false;

        DadosConstrucao itemEscolhido = null;
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item == null || item.prefabDaUnidade == null) continue;
            if (item.preco > chefe.dinheiro) continue;

            string nome = item.nomeItem.ToLower();
            bool itemEhNaval = nome.Contains("navio") || nome.Contains("barco") || nome.Contains("sub") || 
                               nome.Contains("carrier") || nome.Contains("liberty") || nome.Contains("transporte") ||
                               nome.Contains("hovercraft") || nome.Contains("estaleiro") == false && item.categoria == DadosConstrucao.CategoriaItem.Marinha;

            // Se eu quero naval, o item TEM que ser naval. 
            // Se eu quero terrestre (não naval), o item NÃO PODE ser naval.
            if (ehNaval != itemEhNaval) continue;

            foreach (var k in keywords)
            {
                if (nome.Contains(k.ToLower())) 
                {
                    itemEscolhido = item;
                    break;
                }
            }
            if (itemEscolhido != null) break;
        }

        if (itemEscolhido == null) return false;

        // Tenta Produzir
        if (chefe.GastarDinheiro(itemEscolhido.preco))
        {
            GameObject novo = null;
            if (ehHeli && heliportoEscolhido != null)
            {
                // NOVO: GPS Anti-Colisão de Spawn! Se houver outros helis, empurra pro lado
                Vector3 posPouso = heliportoEscolhido.ObterPontoDePousoMundial();
                int helisPerto = Physics.OverlapSphere(posPouso, 5f).Count(c => c.GetComponentInParent<Helicoptero>() != null);
                if (helisPerto > 0) 
                {
                     posPouso += new Vector3(Random.Range(-18f, 18f), 0, Random.Range(-18f, 18f));
                }
                
                posPouso += Vector3.up * 1.5f; // Garante que não clipe
                novo = Instantiate(itemEscolhido.prefabDaUnidade, posPouso, heliportoEscolhido.transform.rotation);
                
                // Dá o RG pro helicóptero ser do time da IA
                IdentidadeUnidade id = novo.GetComponent<IdentidadeUnidade>();
                if(id == null) id = novo.AddComponent<IdentidadeUnidade>();
                id.teamID = chefe.identidade.teamID;
                if (!string.IsNullOrEmpty(chefe.identidade.nomeComandante)) id.nomeDoPais = chefe.identidade.nomeComandante;

                RegistrarUnidade(novo);
                MoverUnidade(novo, CalcularPontoDeEncontro());
                _ultimoStatus = $"🚁 Desdobrando: {itemEscolhido.nomeItem}";
                _ultimoRecrutamentoReal = Time.time;
                return true;
            }
            else if (estaleiroEscolhido != null)
            {
                if (estaleiroEscolhido.ConstruirUnidade(itemEscolhido.prefabDaUnidade))
                {
                    _ultimoStatus = $"⚓ Ordem dada ao Estaleiro: {itemEscolhido.nomeItem}";
                    _ultimoRecrutamentoReal = Time.time;
                    // O estaleiro vai instanciar depois
                    return true;
                }
            }
            else if (fabricaEscolhida != null)
            {
                novo = fabricaEscolhida.ProduzirUnidade(itemEscolhido.prefabDaUnidade);
                if (novo != null)
                {
                    RegistrarUnidade(novo);
                    if(!ehNaval) MoverUnidade(novo, CalcularPontoDeEncontro());
                    _ultimoRecrutamentoReal = Time.time;
                    return true;
                }
            }

            // Devolução se falhar
            chefe.AdicionarDinheiro(itemEscolhido.preco);
        }
        return false;
    }

    private float _ultimoScanGlobal = -100f; // Cooldown para performance

    void AtualizarListasDeFabricas()
    {
        // Limpa listas para evitar nulls (rápido e não custa muito processamento)
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);
        meusAeroportos.RemoveAll(a => a == null);

        // OTIMIZAÇÃO: Evita fazer o Scan Global caríssimo da Unity se acabamos de fazer
        // (Especialmente num loop onde a IA compra 4 coisas na mesma fração de segundo)
        if (Time.time - _ultimoScanGlobal < 5.0f) return;
        _ultimoScanGlobal = Time.time;

        // Re-scan global
        var fabs = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach(var f in fabs) RegistrarFabrica(f);
        
        var ests = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach(var e in ests) RegistrarEstaleiro(e);
        
        var helis = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
        foreach(var h in helis) RegistrarHeliporto(h);

        var aerops = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach(var a in aerops) RegistrarAeroporto(a);
    }

    // =============================================
    // COMBATE
    // =============================================
    void AvaliarCombate()
    {
        if (TotalUnidades() < minimoParaAtacar) return;
        
        Transform alvo = BuscarAlvo();
        if (alvo == null) return;

        _ultimoStatus = "⚔️ ATAQUE TOTAL!";
        jaAtacou = true;
        
        // Terra/Ar (Helis Blindados e Soldados)
        LancarAtaqueCoordenado(alvo.position);
        
        // Mar (Ataque independente naval)
        if (grupoNavios.Count > 0)
        {
             Vector3 ataqueNaval = alvo.position;
             ataqueNaval.y = 0; // Nível do mar
             MoverGrupo(grupoNavios, ataqueNaval);
        }

        // --- NOVO: BOMBRAMDEIO TÁTICO DOS CAÇAS (Apoio Aéreo Aproximado) ---
        if (grupoAvioes.Count > 0)
        {
            foreach (var av in grupoAvioes)
            {
                if (av == null) continue;
                ControleAviao jet = av.GetComponent<ControleAviao>();
                if (jet != null)
                {
                    if (jet.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                    {
                        // O Caça está na base com a turbina desligada! Acionar Missão!
                        Debug.Log($"[IA Suprema] Enviando Suporte Aéreo: {av.name} decolando para ataque.");
                        jet.IniciarMissaoCompleta(alvo.position);
                    }
                    else if (jet.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                    {
                        // Se os rebeldes matarem o primeiro alvo e o general achar outro, nós redirecionamos o caça no ar!
                        Debug.Log($"[IA Suprema] Ajustando radar do {av.name} para novo alvo de ataque ao vivo!");
                        jet.centroDaPatrulha = alvo.position;
                        jet.alvoGPSVoo = alvo.position;
                    }
                }
            }
        }
    }

    private int contagemEsquadrao = 1;

    void LancarAtaqueCoordenado(Vector3 destino)
    {
        // 1. Extração de Tropas Livres
        var tropasLivres = grupoSoldados.Where(s => s != null && s.activeInHierarchy && !Helicoptero.SoldadoEstaEmbarcando(s)).ToList();
        var tanquesLivres = grupoTanques.Where(t => t != null && t.activeInHierarchy).ToList();
        
        int forcaMilitar = tropasLivres.Count + tanquesLivres.Count;

        if (forcaMilitar >= minimoParaAtacar)
        {
            // 2. CRIA O CÉREBRO DO PELOTÃO (A Mente Tática da Fase 1)
            GameObject novoPelotao = new GameObject($"Cerebro_Pelotao_{contagemEsquadrao}");
            novoPelotao.transform.SetParent(this.transform);
            IA_CerebroPelotao cerebro = novoPelotao.AddComponent<IA_CerebroPelotao>();
            
            // Ponto de encontro: Frente da Base
            Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
            // 50 Metros para frente pra garantir que saiam do muro das fábricas!
            Vector3 stagingArea = centro + chefe.transform.forward * 50f; 
            
            cerebro.Inicializar(stagingArea, BuscarAlvo(), forcaMilitar, $"Omega-{contagemEsquadrao}", chefe.identidade.teamID);
            contagemEsquadrao++;

            // 3. Transfere os Soldados e Tanques do General para o Cérebro do Pelotão
            foreach(var t in tanquesLivres)
            {
                 cerebro.AdicionarMembro(t);
                 grupoTanques.Remove(t); // Libertamos do General, assim a Fábrica volta a fabricar mais!
            }
            foreach(var s in tropasLivres)
            {
                 cerebro.AdicionarMembro(s);
                 grupoSoldados.Remove(s); // Começa a fabricar novos soldados para a onda 2
            }
            
            _ultimoStatus = $"⚔️ Pelotão Tático {cerebro.nomeDoPelotao} Enviado ao Front!";
        }

        // 4. Manutenção Externa (Helicópteros ainda não usam o script tático terrestre)
        Vector3 dir = (destino - chefe.transform.position).normalized;
        if(dir == Vector3.zero) dir = Vector3.forward;

        var helisAtaqueBlindagem = grupoHelis.Where(h => 
             h != null && h.GetComponent<Helicoptero>() != null && h.GetComponent<Helicoptero>().modoCombateAtivo
        ).ToList();
        
        MoverEmFormacao(helisAtaqueBlindagem, destino, dir, 15f);
    }

    // =============================================
    // REGISTROS E UTIL
    // =============================================
    
    public void RegistrarUnidade(GameObject u)
    {
        if (u == null) return;
        if(grupoTanques.Contains(u) || grupoSoldados.Contains(u) || grupoNavios.Contains(u) || grupoHelis.Contains(u) || grupoTransportes.Contains(u) || chefe.meusCivis.Contains(u)) return;

        string n = u.name.ToLower();

        // Checa se é civil
        if (n.Contains("civil") || n.Contains("turista") || n.Contains("onibus") || n.Contains("kombi"))
        {
            chefe.meusCivis.Add(u);
            
            // Mandá-lo viajar até a prefeitura do jogador:
            if (chefe.alvoAtaquePrincipal != null)
            {
                MoverUnidade(u, chefe.alvoAtaquePrincipal.position);
            }
            return; // Civis não vão para os grupos táticos
        }
        
        if(u.GetComponent<TransporteTerrestre>() != null) grupoTransportes.Add(u);
        else if(n.Contains("navio") || n.Contains("fragata") || n.Contains("corveta") || n.Contains("sub") || n.Contains("carrier") || n.Contains("liberty") || n.Contains("transporte naval") || n.Contains("hovercraft")) grupoNavios.Add(u);
        else if(n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado") || n.Contains("leonc") || n.Contains("hack") || n.Contains("ubu")) grupoTanques.Add(u);
        else if(n.Contains("heli") || n.Contains("apache") || n.Contains("cobra") || n.Contains("falcon") || n.Contains("ray")) grupoHelis.Add(u);
        else if(n.Contains("caca") || n.Contains("tuk") || n.Contains("jet") || n.Contains("aviao") || n.Contains("super")) grupoAvioes.Add(u);
        else grupoSoldados.Add(u);
    }

    public void RegistrarSoldado(GameObject u)
    {
        RegistrarUnidade(u);
    }

    public void RegistrarFabrica(Fabrica f)
    {
         if(f == null) return;
         var id = f.GetComponent<IdentidadeUnidade>();
         // Se não tiver identidade, assume que é meu se estiver perto?? Não, melhor exigir identidade
         if(id != null && id.teamID == chefe.identidade.teamID && !minhasFabricas.Contains(f))
            minhasFabricas.Add(f);
    }
    
    public void RegistrarEstaleiro(Estaleiro e)
    {
        if(e == null) return;
        
        var id = e.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; // Default Inimigo = 2

        // Fallback: se estaleiro nao tem identidade, assume meu temporariamente se estiver < 100m
        if(id == null) 
        {
             // Cuidado para não roubar estaleiro do player (TeamID 1)
             // Só assume se não tiver ID nenhum E estiver perto
             if(Vector3.Distance(transform.position, e.transform.position) < 150) 
             {
                 id = e.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0) // Neutro?
        {
             if(Vector3.Distance(transform.position, e.transform.position) < 150) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusEstaleiros.Contains(e))
            meusEstaleiros.Add(e);
    }

    public void RegistrarHeliporto(Heliporto h)
    {
        if(h == null) return;
        
        var id = h.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             if(Vector3.Distance(transform.position, h.transform.position) < 150) 
             {
                 id = h.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0) // Neutro?
        {
             if(Vector3.Distance(transform.position, h.transform.position) < 150) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusHeliportos.Contains(h))
            meusHeliportos.Add(h);
    }

    public void RegistrarAeroporto(GerenciadorAeroporto a)
    {
        if(a == null) return;
        
        var id = a.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             if(Vector3.Distance(transform.position, a.transform.position) < 150) 
             {
                 id = a.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0) // Neutro?
        {
             if(Vector3.Distance(transform.position, a.transform.position) < 150) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusAeroportos.Contains(a))
            meusAeroportos.Add(a);
    }

    bool EhNaval(MonoBehaviour b) 
    {
        if(b == null) return false;
        string nomes = b.name.ToLower();
        return nomes.Contains("naval") || nomes.Contains("pier") || nomes.Contains("estaleiro");
    }

    void LimparMortos()
    {
        grupoTanques.RemoveAll(u => u == null);
        grupoSoldados.RemoveAll(u => u == null);
        grupoTransportes.RemoveAll(u => u == null);
        grupoNavios.RemoveAll(u => u == null);
        grupoHelis.RemoveAll(u => u == null);
        grupoAvioes.RemoveAll(u => u == null);
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);
    }

    // ... (Métodos auxiliares)
    Vector3 CalcularPontoDeEncontro()
    {
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
        return centro + Vector3.forward * 30f;
    }
        
    void MoverEmFormacao(List<GameObject> grupo, Vector3 centro, Vector3 direcaoFrente, float espacamento)
    {
        if (grupo.Count == 0) return;
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(grupo.Count)); 
        Vector3 direita = Vector3.Cross(Vector3.up, direcaoFrente).normalized;

        for (int i = 0; i < grupo.Count; i++)
        {
            if (grupo[i] == null) continue;
            int linha = i / colunas;
            int col = i % colunas;
            Vector3 pos = centro + (direita * (col - colunas/2f) * espacamento) - (direcaoFrente * linha * espacamento);
            MoverUnidade(grupo[i], pos);
        }
    }

    void MoverGrupo(List<GameObject> grupo, Vector3 destino) { foreach(var u in grupo) MoverUnidade(u, destino); }

    void MoverUnidade(GameObject u, Vector3 destino)
    {
        if (u == null) return;

        var heli = u.GetComponent<Helicoptero>();
        if (heli != null) 
        { 
            heli.Decolar(destino); 
            return; 
        }

        // NÃO interrompe o soldado se ele já estiver correndo para embarcar num helicóptero!
        if (Helicoptero.SoldadoEstaEmbarcando(u)) return;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) { ctrl.MoverParaPonto(destino); return; }

        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }

    public Transform BuscarAlvo()
    {
        var alvos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        if (alvos.Count == 0) return null;

        // 1. Prefeitura (Alvo Primário Supremo)
        var prefeitura = alvos.FirstOrDefault(a => a.name.ToLower().Contains("prefeitura") || a.GetComponent("ComplexoGovernamental") != null);
        if (prefeitura != null) return prefeitura.transform;

        // 2. Quartel General (Alvo Secundário Militar)
        var quartel = alvos.FirstOrDefault(a => 
        {
            var f = a.GetComponent<Fabrica>();
            return (f != null && f.ehQuartel) || a.name.ToLower().Contains("quartel");
        });
        if (quartel != null) return quartel.transform;

        // 3. Outras Fábricas e Estruturas Estratégicas
        var predio = alvos.FirstOrDefault(a => a.GetComponent<Fabrica>() != null || a.GetComponent<Estaleiro>() != null || a.GetComponent<Heliporto>() != null);
        if (predio != null) return predio.transform;

        // 4. Se não achou nenhum prédio, ataca a primeira unidade civil/militar que vir pela frente
        return alvos[0].transform;
    }
    
    void MoverTropasParaPontoDeEncontro()
    {
        if(chefe.basePrincipal == null) return;
        MoverEmFormacao(grupoTransportes, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 5f, chefe.basePrincipal.forward, 8f);
        MoverEmFormacao(grupoTanques, CalcularPontoDeEncontro(), chefe.basePrincipal.forward, 6f);
        MoverEmFormacao(grupoSoldados, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 10f, chefe.basePrincipal.forward, 3f);
    }
    
    int TotalUnidades() => grupoSoldados.Count + grupoTanques.Count + grupoHelis.Count + grupoNavios.Count + grupoTransportes.Count;

    // OnGUI de debug removido — poluía a tela sobrepondo o HUD de recursos.
    // Para ver status da IA, use o Console (Debug.Log) ou o Inspector.
}
