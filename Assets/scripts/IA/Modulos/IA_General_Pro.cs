using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia recrutamento contínuo e combate ativo (Terra, Ar e MAR).
/// ATUALIZADO: Foco estratégico em destruir defesas com 3 caças (Super_Tuk).
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição Desejada")]
    public int soldadosDesejados = 12;
    public int tanquesDesejados = 10;
    public int helicopterosDesejados = 4;
    public int naviosDesejados = 3; 
    public int transportesDesejados = 2; 
    public int avioesDesejados = 3; // MUDANÇA: Limite de 3 Super_Tuk para ataque cirúrgico

    [Header("Agressividade")]
    public int minimoParaAtacar = 5;
    public float intervaloAtaque = 15f;

    // Listas de Controle
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoNavios = new List<GameObject>(); 
    private List<GameObject> grupoTransportes = new List<GameObject>(); 
    private List<GameObject> grupoAvioes = new List<GameObject>(); 
    private List<GameObject> grupoOutros = new List<GameObject>();

    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();
    [SerializeField] private List<Estaleiro> meusEstaleiros = new List<Estaleiro>(); 
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
             chefe = FindFirstObjectByType<IA_Comandante>();
        }
        if (chefe != null && chefe.cerebroGeneral == null) chefe.cerebroGeneral = this;
    }

    void Update()
    {
        if (chefe == null) return;

        _timerRecrutamento += Time.deltaTime;
        if (_timerRecrutamento >= 2.5f) 
        {
            _timerRecrutamento = 0;
            TentarRecrutar();
        }

        _timerReorganizar += Time.deltaTime;
        if (_timerReorganizar >= 5.0f)
        {
            _timerReorganizar = 0;
            LimparMortos();
            if (!jaAtacou) MoverTropasParaPontoDeEncontro();
        }

        _timerAtaque += Time.deltaTime;
        if (_timerAtaque >= intervaloAtaque)
        {
            _timerAtaque = 0;
            AvaliarCombate();
        }

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

        // AEROPORTOS: Sempre puxar aeronaves do Hangar pro Pátio para voar!
        if (meusAeroportos != null)
        {
            foreach (var aero in meusAeroportos)
            {
                if (aero != null) aero.LiberarTodosDoHangar();
            }
        }

        // HELICOPTEROS
        foreach(var hObj in grupoHelis)
        {
            if (hObj == null) continue;
            Helicoptero heli = hObj.GetComponent<Helicoptero>();
            if (heli && !heli.modoCombateAtivo)
            {
                if (heli.TemSoldados())
                {
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
                            
                            Vector3 pontoDeDesembarque = alvo.position - (eixoCentralZ * 120f) + (direitaMapa * 60f * direcaoPinça) + espalhamentoGPS;

                            float distParaAlvo = Vector3.Distance(heli.transform.position, alvo.position);
                            float distParaDestino = Vector2.Distance(
                                new Vector2(heli.transform.position.x, heli.transform.position.z), 
                                new Vector2(pontoDeDesembarque.x, pontoDeDesembarque.z)
                            );

                            if (heli.estaVoando)
                            {
                                if (distParaDestino <= 20f || distParaAlvo <= 90f) heli.OrdemPousoOuDesembarque();
                                else heli.destino = pontoDeDesembarque;
                            }
                            else
                            {
                                if (distParaDestino > 20f && distParaAlvo > 90f) heli.Decolar(pontoDeDesembarque);
                                else heli.OrdemPousoOuDesembarque();
                            }
                        }
                    }
                    else
                    {
                        if (!heli.estaVoando && Vector3.Distance(heli.transform.position, centro) < 250f)
                             heli.ChamarReforcos();
                    }
                }
                else
                {
                    float distBase = Vector2.Distance(
                        new Vector2(heli.transform.position.x, heli.transform.position.z), 
                        new Vector2(centro.x, centro.z)
                    );

                    if (heli.estaVoando)
                    {
                        if (distBase <= 40f) heli.OrdemPousoOuDesembarque();
                        else heli.destino = centro;
                    }
                    else
                    {
                        if (distBase > 40f)
                        {
                            Random.InitState(heli.GetInstanceID() + 2);
                            Vector3 offsetBase = new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f));
                            heli.Decolar(centro + offsetBase);
                        }
                        else
                        {
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

        // TRANSPORTES TERRESTRES
        foreach(var tObj in grupoTransportes)
        {
            if (tObj == null) continue;
            TransporteTerrestre transp = tObj.GetComponent<TransporteTerrestre>();
            if (transp)
            {
                if (transp.TemPassageiros)
                {
                    if (alvo != null && Vector3.Distance(transp.transform.position, alvo.position) < 60f) transp.DesembarcarTudo();
                }
                else
                {
                    if (Vector3.Distance(transp.transform.position, centro) > 150f) transp.GetComponent<ControleUnidade>()?.MoverParaPonto(centro);
                    else if (!transp.EstaCheio()) transp.TentarEmbarcar();
                }
            }
        }
    }

    void TentarRecrutar()
    {
        if (Time.time - _ultimoRecrutamentoReal < 2.5f) return;

        if (chefe.dinheiro < 100) 
        {
            _ultimoStatus = "💰 Sem dinheiro ($" + (int)chefe.dinheiro + ")";
            return; 
        }

        LimparMortos(); 

        bool temQuartel = minhasFabricas.Any(f => f.ehQuartel);
        bool temFabricaVeiculos = minhasFabricas.Any(f => !f.ehQuartel && !EhNaval(f));
        bool temEstaleiro = meusEstaleiros.Count > 0 || minhasFabricas.Any(f => EhNaval(f));

        if (grupoSoldados.Count < 4 && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado"; return; }
        }
        
        if (grupoTanques.Count < 2)
        {
             if (temFabricaVeiculos)
             {
                 if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU")) { _ultimoStatus = "🚜 Recrutando Tanque Prioritário"; return; }
             }
        }

        if (grupoTransportes.Count < transportesDesejados && temFabricaVeiculos && chefe.dinheiro > 100)
        {
             if (ComprarUnidade(false, false, false, false, "Caminhao", "Hamer", "Jeep", "Truck")) { _ultimoStatus = "🚚 Recrutando Transporte Terreste"; return; }
        }

        if (grupoSoldados.Count < soldadosDesejados && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Sniper", "Fuzileiro")) { _ultimoStatus = "🎖️ Recrutando Soldado Extra"; return; }
        }

        if (grupoTanques.Count < tanquesDesejados && temFabricaVeiculos)
        {
             if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "Leonc", "Hack", "UBU", "Panzer")) { _ultimoStatus = "🚜 Recrutando Tanque Secundário"; return; }
        }

        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 600)
        {
             if (ComprarUnidade(false, false, true, false, "Heli", "Apache", "Cobra", "Falcon", "Ray", "Guincho")) { _ultimoStatus = "🚁 Recrutando Aéreo"; return; }
        }

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

        if (grupoAvioes.Count < avioesDesejados && chefe.dinheiro > 1200 && meusAeroportos.Count > 0)
        {
             if (ComprarUnidade(false, false, false, true, "Caca", "Tuk", "Super", "Jet", "Aviao", "Bombard")) 
             { _ultimoStatus = "✈️ Requisitando Caça Tático de Ataque"; return; }
        }

        if (chefe.dinheiro > 2000)
        {
             if (Random.value > 0.4f && temFabricaVeiculos) ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Blindado");
             else if (temQuartel) ComprarUnidade(true, false, false, false, "Soldado", "Infantaria");
        }
    }

    private float _ultimoRecrutamentoReal = 0f;

    public void RecrutarTurista()
    {
        if (ComprarUnidade(false, false, false, false, "civil", "turista", "caminhonete", "kombi", "onibus"))
        {
            _ultimoStatus = "🤝 Despachando Transporte Civil/Turistas para a fronteira.";
        }
    }

    bool ComprarUnidade(bool requerQuartel, bool ehNaval, bool ehHeli, bool ehAviao, params string[] keywords)
    {
        AtualizarListasDeFabricas(); 

        Fabrica fabricaEscolhida = null;
        Estaleiro estaleiroEscolhido = null;
        Heliporto heliportoEscolhido = null;
        GerenciadorAeroporto aeroportoEscolhido = null;

        if (ehAviao)
        {
            var aerosValidos = meusAeroportos.Where(a => a != null).OrderBy(x => Random.value).ToList();
            if (aerosValidos.Count > 0) aeroportoEscolhido = aerosValidos[0];
            else return false;
        }
        else if (ehHeli)
        {
            var helisValidos = meusHeliportos.Where(h => h != null).OrderBy(x => Random.value).ToList();
            if (helisValidos.Count > 0) heliportoEscolhido = helisValidos[0];
            else return false; 
        }
        else if (ehNaval)
        {
            var estsValidos = meusEstaleiros.Where(e => e != null).OrderBy(x => Random.value).ToList();
            if (estsValidos.Count > 0) estaleiroEscolhido = estsValidos[0]; 
            else return false; // NUNCA constrói navio em fábrica de terra (Hangar)
        }
        else
        {
             var fabsValidas = minhasFabricas.Where(f => f != null && f.ehQuartel == requerQuartel && !EhNaval(f)).OrderBy(x => Random.value).ToList();
             if (fabsValidas.Count > 0) fabricaEscolhida = fabsValidas[0];
        }

        if (fabricaEscolhida == null && estaleiroEscolhido == null && heliportoEscolhido == null && aeroportoEscolhido == null) return false;

        if (MenuConstrucao.catalogoGlobal == null) return false;

        DadosConstrucao itemEscolhido = null;
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item == null || item.prefabDaUnidade == null) continue;
            if (item.preco > chefe.dinheiro) continue;

            string nome = item.nomeItem.ToLower();
            bool itemEhNaval = nome.Contains("navio") || nome.Contains("barco") || nome.Contains("sub") || 
                               nome.Contains("carrier") || nome.Contains("liberty") || nome.Contains("transporte") ||
                               nome.Contains("fragata") || nome.Contains("corveta") || nome.Contains("destroier") || nome.Contains("sam") ||
                               nome.Contains("hovercraft") || nome.Contains("estaleiro") == false && item.categoria == DadosConstrucao.CategoriaItem.Marinha;

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

        if (chefe.GastarDinheiro(itemEscolhido.preco))
        {
            GameObject novo = null;
            if (ehAviao && aeroportoEscolhido != null)
            {
                 aeroportoEscolhido.ComprarAviao(itemEscolhido.prefabDaUnidade);
                 _ultimoStatus = $"✈️ Aeronave Requisitada: {itemEscolhido.nomeItem}";
                 _ultimoRecrutamentoReal = Time.time;
                 return true;
            }
            else if (ehHeli && heliportoEscolhido != null)
            {
                Vector3 posPouso = heliportoEscolhido.ObterPontoDePousoMundial();
                int helisPerto = Physics.OverlapSphere(posPouso, 5f).Count(c => c.GetComponentInParent<Helicoptero>() != null);
                if (helisPerto > 0) posPouso += new Vector3(Random.Range(-18f, 18f), 0, Random.Range(-18f, 18f));
                
                posPouso += Vector3.up * 1.5f; 
                novo = Instantiate(itemEscolhido.prefabDaUnidade, posPouso, heliportoEscolhido.transform.rotation);
                
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
            chefe.AdicionarDinheiro(itemEscolhido.preco);
        }
        return false;
    }

    private float _ultimoScanGlobal = -100f; 

    float CalcularAlcanceRadar()
    {
        float tempo = Time.time;
        if (tempo % 60f < 5.1f) return 3000f;
        if (tempo % 30f < 5.1f) return 2000f;
        if (tempo % 10f < 5.1f) return 1000f;
        return 400f; // Padrão
    }

    void AtualizarListasDeFabricas()
    {
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);
        meusAeroportos.RemoveAll(a => a == null);

        if (Time.time - _ultimoScanGlobal < 5.0f) return;
        _ultimoScanGlobal = Time.time;

        float raioBusca = CalcularAlcanceRadar();

        var fabs = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach(var f in fabs) RegistrarFabrica(f);
        
        var ests = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach(var e in ests) RegistrarEstaleiro(e, raioBusca);
        
        var helis = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
        foreach(var h in helis) RegistrarHeliporto(h, raioBusca);

        var aerops = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach(var a in aerops) RegistrarAeroporto(a, raioBusca);
    }

    void AvaliarCombate()
    {
        Transform alvo = BuscarAlvo();
        if (alvo == null) return;

        // --- NOVA ESTRATÉGIA: CAÇAS FOCAM EM DEFESAS (ATAQUE INDEPENDENTE DAS TROPAS) ---
        if (grupoAvioes.Count > 0)
        {
            Transform alvoDefesa = BuscarAlvoEstrategicoParaAvioes();
            Vector3 pontoAtaqueAereo = alvoDefesa != null ? alvoDefesa.position : alvo.position;

            foreach (var av in grupoAvioes)
            {
                if (av == null) continue;
                ControleAviao jet = av.GetComponent<ControleAviao>();
                if (jet != null)
                {
                    if (jet.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                    {
                        Debug.Log($"[IA Suprema] Enviando Suporte Aéreo! Avião focando em: {(alvoDefesa != null ? alvoDefesa.name : "Base Principal")}");
                        jet.IniciarMissaoCompleta(pontoAtaqueAereo);
                    }
                    else if (jet.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                    {
                        jet.centroDaPatrulha = pontoAtaqueAereo;
                        jet.alvoGPSVoo = pontoAtaqueAereo;
                    }
                }
            }
        }

        if (TotalUnidades() < minimoParaAtacar) return;
        
        _ultimoStatus = "⚔️ ATAQUE TOTAL!";
        jaAtacou = true;
        
        LancarAtaqueCoordenado(alvo.position);
        
        if (grupoNavios.Count > 0)
        {
             Vector3 ataqueNaval = alvo.position;
             ataqueNaval.y = 0; 
             MoverGrupo(grupoNavios, ataqueNaval);
        }
    }

    // MUDANÇA: Novo método para a IA caçar as defesas inimigas antes de atacar
    public Transform BuscarAlvoEstrategicoParaAvioes()
    {
        var alvosInimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        
        // 1. Procura especificamente Torretas, Antiaéreas, Bunkers ou Lançadores de Míssil
        var defesas = alvosInimigos.Where(a => 
            a.name.ToLower().Contains("torreta") || 
            a.name.ToLower().Contains("anti") || 
            a.name.ToLower().Contains("bunker") ||
            a.name.ToLower().Contains("missil") ||
            a.name.ToLower().Contains("defesa")
        ).ToList();

        if (defesas.Count > 0)
        {
            // Retorna a defesa mais próxima do centro do mapa ou do general
            return defesas[0].transform;
        }

        return null; // Se não achar defesa, os aviões atacam o alvo normal
    }

    private int contagemEsquadrao = 1;

    void LancarAtaqueCoordenado(Vector3 destino)
    {
        var tropasLivres = grupoSoldados.Where(s => s != null && s.activeInHierarchy && !Helicoptero.SoldadoEstaEmbarcando(s)).ToList();
        var tanquesLivres = grupoTanques.Where(t => t != null && t.activeInHierarchy).ToList();
        
        int forcaMilitar = tropasLivres.Count + tanquesLivres.Count;

        if (forcaMilitar >= minimoParaAtacar)
        {
            GameObject novoPelotao = new GameObject($"Cerebro_Pelotao_{contagemEsquadrao}");
            novoPelotao.transform.SetParent(this.transform);
            IA_CerebroPelotao cerebro = novoPelotao.AddComponent<IA_CerebroPelotao>();
            
            Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;

            // --- NOVA LÓGICA DE CERCO E FLANQUEAMENTO ---
            // 1. Acha a direção reta até a base inimiga
            Vector3 dirParaAlvo = (destino - centro).normalized;
            if (dirParaAlvo == Vector3.zero) dirParaAlvo = chefe.transform.forward;

            // 2. Calcula o lado "Esquerdo" do mapa com base na nossa direção
            Vector3 esquerdaLateral = Vector3.Cross(Vector3.up, dirParaAlvo).normalized;

            // 3. Verifica a distância real até o inimigo para não passar do alvo sem querer
            float distanciaTotal = Vector3.Distance(centro, destino);

            // O Pelotão avança 600 metros para frente! (Ou 70% do caminho se o inimigo estiver muito perto)
            float avancoFrontal = Mathf.Min(600f, distanciaTotal * 0.7f);

            Vector3 offsetFlanco = Vector3.zero;
            int taticaDeFlanco = contagemEsquadrao % 3; // O segredo da divisão: 0 = Centro, 1 = Esquerda, 2 = Direita
            string nomeTatica = "Centro";

            if (taticaDeFlanco == 1)
            {
                offsetFlanco = esquerdaLateral * 250f; // Abre 250 metros para a ESQUERDA
                nomeTatica = "Esquerda";
            }
            else if (taticaDeFlanco == 2)
            {
                offsetFlanco = -esquerdaLateral * 250f; // Abre 250 metros para a DIREITA
                nomeTatica = "Direita";
            }

            // O Ponto de Encontro Tático final longe da fronteira
            Vector3 stagingArea = centro + (dirParaAlvo * avancoFrontal) + offsetFlanco;

            // Ajusta a altura para não ficar voando ou debaixo da terra
            if (Terrain.activeTerrain != null)
            {
                stagingArea.y = Terrain.activeTerrain.SampleHeight(stagingArea);
            }

            Debug.Log($"🗺️ [IA General] Pelotão marchando para Zona de Encontro: {nomeTatica} a {avancoFrontal} metros de distância!");

            // ------------------------------------------------------------------------
            
            cerebro.Inicializar(stagingArea, BuscarAlvo(), forcaMilitar, $"Omega-{contagemEsquadrao}-{nomeTatica}", chefe.identidade.teamID);
            contagemEsquadrao++;

            foreach(var t in tanquesLivres)
            {
                 cerebro.AdicionarMembro(t);
                 grupoTanques.Remove(t); 
            }
            foreach(var s in tropasLivres)
            {
                 cerebro.AdicionarMembro(s);
                 grupoSoldados.Remove(s); 
            }
            
            _ultimoStatus = $"⚔️ Pelotão Tático {cerebro.nomeDoPelotao} Enviado ao Front!";
        }

        Vector3 dir = (destino - chefe.transform.position).normalized;
        if(dir == Vector3.zero) dir = Vector3.forward;

        var helisAtaqueBlindagem = grupoHelis.Where(h => 
             h != null && h.GetComponent<Helicoptero>() != null && h.GetComponent<Helicoptero>().modoCombateAtivo
        ).ToList();
        
        MoverEmFormacao(helisAtaqueBlindagem, destino, dir, 15f);
    }
    
    public void RegistrarUnidade(GameObject u)
    {
        if (u == null) return;
        if(grupoTanques.Contains(u) || grupoSoldados.Contains(u) || grupoNavios.Contains(u) || grupoHelis.Contains(u) || grupoTransportes.Contains(u) || chefe.meusCivis.Contains(u)) return;

        string n = u.name.ToLower();

        if (n.Contains("civil") || n.Contains("turista") || n.Contains("onibus") || n.Contains("kombi"))
        {
            chefe.meusCivis.Add(u);
            if (chefe.alvoAtaquePrincipal != null) MoverUnidade(u, chefe.alvoAtaquePrincipal.position);
            return; 
        }
        
        if(u.GetComponent<TransporteTerrestre>() != null) grupoTransportes.Add(u);
        else if(n.Contains("navio") || n.Contains("fragata") || n.Contains("corveta") || n.Contains("sub") || n.Contains("carrier") || n.Contains("liberty") || n.Contains("transporte naval") || n.Contains("hovercraft")) grupoNavios.Add(u);
        else if(n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado") || n.Contains("leonc") || n.Contains("hack") || n.Contains("ubu")) grupoTanques.Add(u);
        else if(n.Contains("heli") || n.Contains("apache") || n.Contains("cobra") || n.Contains("falcon") || n.Contains("ray")) grupoHelis.Add(u);
        else if(n.Contains("caca") || n.Contains("tuk") || n.Contains("jet") || n.Contains("aviao") || n.Contains("super")) grupoAvioes.Add(u);
        else grupoSoldados.Add(u);
    }

    public void RegistrarSoldado(GameObject u) { RegistrarUnidade(u); }

    public void RegistrarFabrica(Fabrica f)
    {
         if(f == null) return;
         var id = f.GetComponent<IdentidadeUnidade>();
         if(id != null && id.teamID == chefe.identidade.teamID && !minhasFabricas.Contains(f))
            minhasFabricas.Add(f);
    }
    
    public void RegistrarEstaleiro(Estaleiro e, float alcanceDeBusca = 400f)
    {
        if(e == null) return;
        var id = e.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             // Se for neutro e estiver dentro do radar atual, reivindica
             if(Vector3.Distance(transform.position, e.transform.position) < alcanceDeBusca) 
             {
                 id = e.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0)
        {
             if(Vector3.Distance(transform.position, e.transform.position) < alcanceDeBusca) id.teamID = meuTime;
        }

        // REGISTRO: Se for o meu time, registra independente da distância! (Aeroporto/Estaleiro podem ser longe)
        if(id != null && id.teamID == meuTime && !meusEstaleiros.Contains(e)) meusEstaleiros.Add(e);
    }

    public void RegistrarHeliporto(Heliporto h, float alcanceDeBusca = 400f)
    {
        if(h == null) return;
        var id = h.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             if(Vector3.Distance(transform.position, h.transform.position) < alcanceDeBusca) 
             {
                 id = h.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0)
        {
             if(Vector3.Distance(transform.position, h.transform.position) < alcanceDeBusca) id.teamID = meuTime;
        }

        if(id != null && id.teamID == meuTime && !meusHeliportos.Contains(h)) meusHeliportos.Add(h);
    }

    public void RegistrarAeroporto(GerenciadorAeroporto a, float alcanceDeBusca = 1000f)
    {
        if(a == null) return;
        var id = a.GetComponent<IdentidadeUnidade>();
        int meuTime = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 

        if(id == null) 
        {
             // Aeroportos costumam ser construídos MUITO longe por segurança, usa o maior raio + 500m bônus intencional
             if(Vector3.Distance(transform.position, a.transform.position) < alcanceDeBusca + 500f) 
             {
                 id = a.gameObject.AddComponent<IdentidadeUnidade>();
                 id.teamID = meuTime;
             }
        }
        else if (id.teamID == 0)
        {
             if(Vector3.Distance(transform.position, a.transform.position) < alcanceDeBusca + 500f) id.teamID = meuTime;
        }

        // Se já tem meu ID, aceita independente da distância
        if(id != null && id.teamID == meuTime && !meusAeroportos.Contains(a)) meusAeroportos.Add(a);
    }

    bool EhNaval(MonoBehaviour b) 
    {
        if(b == null) return false;
        if(b is Estaleiro) return true;
        string nomes = b.name.ToLower();
        return (nomes.Contains("estaleiro") || nomes.Contains("pier")) && !nomes.Contains("hangar") && !nomes.Contains("fabrica");
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

        if (Helicoptero.SoldadoEstaEmbarcando(u)) return;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) { ctrl.MoverParaPonto(destino); return; }

        var aviao = u.GetComponent<ControleAviao>();
        if (aviao != null) { aviao.IniciarMissaoCompleta(destino); return; }

        var navIntel = u.GetComponent<NavegacaoInteligenteNaval>();
        if (navIntel != null) { navIntel.DefinirDestino(destino); return; }

        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }

    public Transform BuscarAlvo()
    {
        var alvos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        if (alvos.Count == 0) return null;

        var prefeitura = alvos.FirstOrDefault(a => a.name.ToLower().Contains("prefeitura") || a.GetComponent("ComplexoGovernamental") != null);
        if (prefeitura != null) return prefeitura.transform;

        var quartel = alvos.FirstOrDefault(a => 
        {
            var f = a.GetComponent<Fabrica>();
            return (f != null && f.ehQuartel) || a.name.ToLower().Contains("quartel");
        });
        if (quartel != null) return quartel.transform;

        var predio = alvos.FirstOrDefault(a => a.GetComponent<Fabrica>() != null || a.GetComponent<Estaleiro>() != null || a.GetComponent<Heliporto>() != null);
        if (predio != null) return predio.transform;

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
}