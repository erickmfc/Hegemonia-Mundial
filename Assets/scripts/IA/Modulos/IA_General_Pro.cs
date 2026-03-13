using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia recrutamento contínuo e combate ativo.
/// ATUALIZADO: Caças priorizam economia (Minas, Refinarias) para enfraquecer o jogador.
/// ATUALIZADO (NOVO): Espionagem Tática. O General aprende a composição do exército inimigo e se adapta.
/// FIX: Unidades não giram mais no próprio eixo ao se agrupar (Anti-Spin).
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição Desejada (Dinâmica)")]
    public int soldadosDesejados = 20; 
    public int tanquesDesejados = 15;  
    public int helicopterosDesejados = 5;
    public int naviosDesejados = 4; 
    public int transportesDesejados = 3; 
    public int avioesDesejados = 4; 

    [Header("Agressividade")]
    public int minimoParaAtacar = 5;
    public float intervaloAtaque = 15f;

    [Header("Espionagem Tática (Machine Learning)")]
    public bool usarInteligenciaAdaptativa = true;
    public string relatorioInimigo = "Coletando informações...";
    private float _timerEspionagem;
    private bool _inimigoForteEmDefesa = false;

    // Listas de Controle
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoNavios = new List<GameObject>(); 
    private List<GameObject> grupoTransportes = new List<GameObject>(); 
    private List<GameObject> grupoAvioes = new List<GameObject>(); 

    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();
    [SerializeField] private List<Estaleiro> meusEstaleiros = new List<Estaleiro>(); 
    [SerializeField] private List<Heliporto> meusHeliportos = new List<Heliporto>();
    [SerializeField] private List<GerenciadorAeroporto> meusAeroportos = new List<GerenciadorAeroporto>();

    private float _timerRecrutamento;
    private float _timerAtaque;
    private float _timerReorganizar;
    private float _timerTransporte;
    private float _timerCompraAviao = 0f;   
    private float _timerRayTransporte = 0f; 
    private float _timerDespachoAviao = 30f; 
    private bool jaAtacou = false;

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        if (chefe == null) chefe = FindFirstObjectByType<IA_Comandante>();
        if (chefe != null && chefe.cerebroGeneral == null) chefe.cerebroGeneral = this;
    }

    void Update()
    {
        if (chefe == null) return;

        if (usarInteligenciaAdaptativa)
        {
            _timerEspionagem += Time.deltaTime;
            if (_timerEspionagem >= 20f) 
            {
                _timerEspionagem = 0;
                EscanearEAdaptarAoInimigo();
            }
        }

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

        _timerCompraAviao += Time.deltaTime;
        if (_timerCompraAviao >= 300f)
        {
            _timerCompraAviao = 0;
            TentarComprarAviaoPeriodicamentre();
        }

        _timerRayTransporte += Time.deltaTime;
        if (_timerRayTransporte >= 45f)
        {
            _timerRayTransporte = 0;
            GerenciarTransporteRay();
        }

        _timerDespachoAviao += Time.deltaTime;
        if (_timerDespachoAviao >= 60f)
        {
            _timerDespachoAviao = 0;
            DespacharAvioesParaMissao();
        }
    }

    void EscanearEAdaptarAoInimigo()
    {
        var unidadesInimigas = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(u => u.teamID == 1).ToList();
        
        int infantariaInimiga = 0;
        int tanquesInimigos = 0;
        int aereoInimigo = 0;
        int defesasInimigas = 0;

        foreach(var ini in unidadesInimigas)
        {
            string n = ini.name.ToLower();
            if (n.Contains("soldado") || n.Contains("infantaria") || n.Contains("person")) infantariaInimiga++;
            else if (n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado")) tanquesInimigos++;
            else if (n.Contains("heli") || n.Contains("caca") || n.Contains("aviao") || n.Contains("jet")) aereoInimigo++;
            else if (n.Contains("torreta") || n.Contains("bunker") || n.Contains("missil") || n.Contains("anti") || n.Contains("defesa")) defesasInimigas++;
        }

        if (tanquesInimigos > (tanquesDesejados / 2)) 
        {
            helicopterosDesejados = Mathf.Max(5, helicopterosDesejados + 1);
            avioesDesejados = Mathf.Max(4, avioesDesejados + 1);
            tanquesDesejados = Mathf.Max(5, tanquesDesejados - 1); 
            relatorioInimigo = "Inimigo prefere Blindados. Focando na supremacia aérea.";
        }
        else if (infantariaInimiga > (soldadosDesejados / 2))
        {
            tanquesDesejados = Mathf.Max(15, tanquesDesejados + 2);
            soldadosDesejados = Mathf.Max(10, soldadosDesejados - 2); 
            relatorioInimigo = "Inimigo recruta muita Infantaria. Produzindo força de blindados anti-pessoal.";
        }
        else if (aereoInimigo > 3)
        {
            avioesDesejados = Mathf.Max(4, avioesDesejados + 2);
            relatorioInimigo = "Inimigo está dominando os céus. Requisitando mais caças interceptadores.";
        }

        if (defesasInimigas >= 4)
        {
            _inimigoForteEmDefesa = true;
            minimoParaAtacar = 15; 
            relatorioInimigo += " | Base inimiga fortificada. Preprando ataque em flanco.";
        }
        else
        {
            _inimigoForteEmDefesa = false;
            minimoParaAtacar = 6; 
        }
    }

    void GerenciarTransportes()
    {
        Transform alvo = BuscarAlvo();
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

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
                            float direcaoPinca = (heli.GetInstanceID() % 2 == 0) ? 1f : -1f;
                            Vector3 pontoDeDesembarque = alvo.position - (eixoCentralZ * 120f) + (direitaMapa * 60f * direcaoPinca);

                            if (heli.estaVoando)
                            {
                                if (Vector3.Distance(heli.transform.position, pontoDeDesembarque) <= 30f) heli.OrdemPousoOuDesembarque();
                                else heli.destino = pontoDeDesembarque;
                            }
                            else
                            {
                                if (Vector3.Distance(heli.transform.position, pontoDeDesembarque) > 30f) heli.Decolar(pontoDeDesembarque);
                                else heli.OrdemPousoOuDesembarque();
                            }
                        }
                    }
                    else if (!heli.estaVoando && Vector3.Distance(heli.transform.position, centro) < 80f)
                    {
                        if (heli.TemEspaco() > 0) heli.ChamarReforcos();
                    }
                }
                else
                {
                    if (heli.estaVoando)
                    {
                        if (Vector3.Distance(heli.transform.position, centro) <= 50f) heli.OrdemPousoOuDesembarque();
                        else heli.destino = centro;
                    }
                    else
                    {
                        if (Vector3.Distance(heli.transform.position, centro) > 80f) 
                            heli.Decolar(centro);
                        else if (heli.TemEspaco() > 0 && Vector3.Distance(heli.transform.position, centro) < 80f) 
                            heli.ChamarReforcos();
                    }
                }
            }
        }
        
        foreach(var tObj in grupoTransportes)
        {
            if (tObj == null) continue;
            TransporteTerrestre transp = tObj.GetComponent<TransporteTerrestre>();
            if (transp)
            {
                if (transp.EstaCheio())
                {
                    if (alvo != null)
                    {
                        if (Vector3.Distance(transp.transform.position, alvo.position) < 80f)
                            transp.DesembarcarTudo();
                        else
                            transp.GetComponent<ControleUnidade>()?.MoverParaPonto(alvo.position);
                    }
                }
                else if (transp.TemPassageiros && alvo != null && Vector3.Distance(transp.transform.position, alvo.position) < 80f)
                {
                    transp.DesembarcarTudo();
                }
                else if (!transp.TemPassageiros && Vector3.Distance(transp.transform.position, centro) > 150f)
                {
                    transp.GetComponent<ControleUnidade>()?.MoverParaPonto(centro);
                }
                else if (!transp.EstaCheio() && !transp.EmCooldown() && Vector3.Distance(transp.transform.position, centro) < 100f)
                {
                    transp.TentarEmbarcar();
                }
            }
        }
    }

    void GerenciarTransporteRay()
    {
        Transform alvoInimigo = BuscarAlvo();
        if (alvoInimigo == null) return;

        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        foreach (var hObj in grupoHelis)
        {
            if (hObj == null) continue;

            bool ehRay = hObj.name.ToLower().Contains("ray") || hObj.name.ToLower().Contains("guincho");
            if (!ehRay) continue;

            Helicoptero heli = hObj.GetComponent<Helicoptero>();
            if (heli == null || heli.modoCombateAtivo) continue;

            if (heli.TemSoldados())
            {
                Vector3 dirAtaque = (alvoInimigo.position - centro).normalized;
                Vector3 pontoDesembarque = alvoInimigo.position - (dirAtaque * 150f);
                if (Terrain.activeTerrain != null)
                    pontoDesembarque.y = Terrain.activeTerrain.SampleHeight(pontoDesembarque);

                if (heli.estaVoando)
                {
                    if (Vector3.Distance(heli.transform.position, pontoDesembarque) <= 40f)
                    {
                        heli.OrdemPousoOuDesembarque(); 
                    }
                    else
                    {
                        heli.destino = pontoDesembarque;
                    }
                }
                else
                {
                    heli.Decolar(pontoDesembarque);
                }
            }
            else
            {
                if (heli.estaVoando)
                {
                    if (Vector3.Distance(heli.transform.position, centro) <= 60f)
                    {
                        heli.OrdemPousoOuDesembarque(); 
                    }
                    else
                    {
                        heli.destino = centro; 
                    }
                }
                else
                {
                    if (Vector3.Distance(heli.transform.position, centro) > 80f)
                    {
                        heli.Decolar(centro); 
                    }
                    else if (heli.TemEspaco() > 0)
                    {
                        heli.ChamarReforcos(); 
                    }
                }
            }
        }
    }

    void DespacharAvioesParaMissao()
    {
        if (meusAeroportos.Count == 0) return;

        Vector3 alvoPos = Vector3.zero;
        bool temAlvo = false;

        var inimigos = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var id in inimigos)
        {
            if (id == null || id.teamID != 1) continue;
            alvoPos = id.transform.position;
            temAlvo = true;
            break;
        }

        if (!temAlvo) return; 

        foreach (var aero in meusAeroportos)
        {
            if (aero == null) continue;

            var avioesNoPatio = new System.Collections.Generic.List<ControleAviao>(aero.avioesNoPatio);
            foreach (var aviao in avioesNoPatio)
            {
                if (aviao == null) continue;
                if (aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) continue;
                if (aviao.aguardandoCliqueRadar) continue; 

                aviao.IniciarMissaoCompleta(alvoPos);
            }
        }
    }

    void TentarRecrutar()
    {
        if (Time.time - _ultimoRecrutamentoReal < 2.5f) return;
        if (chefe.dinheiro < 100) return; 

        LimparMortos(); 

        bool temQuartel = minhasFabricas.Any(f =>
            f != null && (
                f.ehQuartel ||
                f.name.ToLower().Contains("quartel") ||
                f.name.ToLower().Contains("tenda") ||
                f.name.ToLower().Contains("barrack") ||
                f.name.ToLower().Contains("militar")
            )
        );
        bool temFabricaVeiculos = minhasFabricas.Any(f => f != null && !f.ehQuartel && !EhNaval(f));
        bool temEstaleiro = meusEstaleiros.Count > 0 || minhasFabricas.Any(f => EhNaval(f));

        if (grupoSoldados.Count < 4 && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Fuzileiro")) return;
        }

        if (grupoSoldados.Count < 4 && !temQuartel && minhasFabricas.Count > 0)
        {
            var fabricaFallback = minhasFabricas.FirstOrDefault(f => f != null);
            if (fabricaFallback != null)
            {
                DadosConstrucao itemSoldado = MenuConstrucao.catalogoGlobal?.FirstOrDefault(i =>
                    i != null && i.prefabDaUnidade != null &&
                    (i.nomeItem.ToLower().Contains("soldado") || i.nomeItem.ToLower().Contains("rifle") || i.nomeItem.ToLower().Contains("infantaria")) &&
                    i.preco <= chefe.dinheiro
                );
                if (itemSoldado != null && chefe.GastarDinheiro(itemSoldado.preco))
                {
                    var novo = fabricaFallback.ProduzirUnidade(itemSoldado.prefabDaUnidade);
                    if (novo != null) { RegistrarUnidade(novo); _ultimoRecrutamentoReal = Time.time; return; }
                    else chefe.AdicionarDinheiro(itemSoldado.preco); 
                }
            }
        }
        
        if (grupoTanques.Count < 3 && temFabricaVeiculos)
        {
             if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "South", "Ubu", "Gravity", "Anti")) return;
        }

        if (grupoTransportes.Count < transportesDesejados && temFabricaVeiculos && chefe.dinheiro > 100)
        {
             if (ComprarUnidade(false, false, false, false, "Caminhao", "Hamer", "Jeep", "Truck")) return;
        }

        if (grupoSoldados.Count < soldadosDesejados && temQuartel)
        {
            if (ComprarUnidade(true, false, false, false, "Soldado", "Rifle", "Infantaria", "Fuzileiro")) return;
        }

        if (grupoTanques.Count < tanquesDesejados && temFabricaVeiculos)
        {
             if (ComprarUnidade(false, false, false, false, "Tank", "Tanque", "Leopard", "Blindado", "South", "Ubu", "Gravity", "Anti")) return;
        }

        if (grupoAvioes.Count < avioesDesejados && meusAeroportos.Count > 0 && chefe.dinheiro > 600)
        {
             if (ComprarUnidade(false, false, false, true, "Caca", "Tuk", "F22", "F-22", "Super", "Jet", "Aviao", "Caoc")) return;
        }

        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 600)
        {
             if (ComprarUnidade(false, false, true, false, "Heli", "Apache", "Cobra", "Falcon", "Ray", "Guincho")) return;
        }

        if (chefe.dinheiro > 2000)
        {
             if (Random.value > 0.4f && temFabricaVeiculos) ComprarUnidade(false, false, false, false, "Tank", "South", "Ubu", "Gravity");
             else if (temQuartel) ComprarUnidade(true, false, false, false, "Soldado", "Infantaria");
        }
    }

    void TentarComprarAviaoPeriodicamentre()
    {
        if (meusAeroportos.Count == 0) return;
        if (grupoAvioes.Count >= 5) return; 
        if (chefe.dinheiro < 600) return;   
        if (MenuConstrucao.catalogoGlobal == null) return;

        DadosConstrucao itemAviao = MenuConstrucao.catalogoGlobal.FirstOrDefault(i =>
            i != null && i.prefabDaUnidade != null &&
            i.preco <= chefe.dinheiro &&
            (i.nomeItem.ToLower().Contains("caca") || i.nomeItem.ToLower().Contains("tuk") ||
             i.nomeItem.ToLower().Contains("f22") || i.nomeItem.ToLower().Contains("jet") ||
             i.nomeItem.ToLower().Contains("aviao") || i.nomeItem.ToLower().Contains("super") ||
             i.nomeItem.ToLower().Contains("caoc"))
        );

        if (itemAviao == null) return;

        GerenciadorAeroporto aeroEscolhido = meusAeroportos[Random.Range(0, meusAeroportos.Count)];
        if (aeroEscolhido == null) return;

        if (chefe.GastarDinheiro(itemAviao.preco))
        {
            aeroEscolhido.ComprarAviao(itemAviao.prefabDaUnidade);
        }
    }

    private float _ultimoRecrutamentoReal = 0f;

    public void RecrutarTurista()
    {
        ComprarUnidade(false, false, false, false, "civil", "turista", "caminhonete", "kombi", "onibus");
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
            else return false; 
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
                               nome.Contains("carrier") || nome.Contains("liberty") || nome.Contains("fragata") || 
                               nome.Contains("corveta") || nome.Contains("destroier") || nome.Contains("hovercraft") || 
                               nome.Contains("estaleiro") || nome.Contains("pier") || 
                               item.categoria == DadosConstrucao.CategoriaItem.Marinha;

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
                 _ultimoRecrutamentoReal = Time.time;
                 return true;
            }
            else if (ehHeli && heliportoEscolhido != null)
            {
                Vector3 posPouso = heliportoEscolhido.ObterPontoDePousoMundial() + Vector3.up * 1.5f;
                novo = Instantiate(itemEscolhido.prefabDaUnidade, posPouso, heliportoEscolhido.transform.rotation);
                RegistrarUnidade(novo);
                _ultimoRecrutamentoReal = Time.time;
                return true;
            }
            else if (estaleiroEscolhido != null)
            {
                if (estaleiroEscolhido.ConstruirUnidade(itemEscolhido.prefabDaUnidade))
                {
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

    void AtualizarListasDeFabricas()
    {
        minhasFabricas.RemoveAll(f => f == null);
        meusEstaleiros.RemoveAll(e => e == null);
        meusHeliportos.RemoveAll(h => h == null);
        meusAeroportos.RemoveAll(a => a == null);

        if (Time.time - _ultimoScanGlobal < 5.0f) return;
        _ultimoScanGlobal = Time.time;

        var fabs = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach(var f in fabs) RegistrarFabrica(f);
        
        var ests = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach(var e in ests) RegistrarEstaleiro(e, 2000f);
        
        var helis = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
        foreach(var h in helis) RegistrarHeliporto(h, 2000f);

        var aerops = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach(var a in aerops) RegistrarAeroporto(a, 3000f);
    }

    void AvaliarCombate()
    {
        Transform alvo = BuscarAlvo();
        if (alvo == null) return;

        if (grupoAvioes.Count > 0)
        {
            Transform alvoDefesa = BuscarAlvoEstrategicoParaAvioes();
            Vector3 pontoAtaqueAereo = alvoDefesa != null ? alvoDefesa.position : alvo.position;

            foreach (var av in grupoAvioes)
            {
                if (av == null) continue;
                ControleAviao jet = av.GetComponent<ControleAviao>();
                if (jet != null) jet.IniciarMissaoCompleta(pontoAtaqueAereo);
            }
        }

        if (TotalUnidades() < minimoParaAtacar) return;
        
        jaAtacou = true;
        LancarAtaqueCoordenado(alvo.position);
    }

    public Transform BuscarAlvoEstrategicoParaAvioes()
    {
        var alvosInimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        
        var economia = alvosInimigos.Where(a => 
            a.name.ToLower().Contains("refinaria") || 
            a.name.ToLower().Contains("mina") || 
            a.name.ToLower().Contains("usina") ||
            a.name.ToLower().Contains("petroleo") ||
            a.name.ToLower().Contains("gerador") ||
            a.name.ToLower().Contains("armazem")
        ).ToList();

        if (economia.Count > 0) return economia[Random.Range(0, economia.Count)].transform;

        var defesas = alvosInimigos.Where(a => 
            a.name.ToLower().Contains("torreta") || 
            a.name.ToLower().Contains("anti") || 
            a.name.ToLower().Contains("bunker") ||
            a.name.ToLower().Contains("missil") ||
            a.name.ToLower().Contains("defesa") ||
            a.name.ToLower().Contains("ares")
        ).ToList();

        if (defesas.Count > 0) return defesas[Random.Range(0, defesas.Count)].transform;

        return BuscarAlvo();
    }

    private int contagemEsquadrao = 1;

    void LancarAtaqueCoordenado(Vector3 destino)
    {
        var tropasLivres = grupoSoldados.Where(s => s != null && s.activeInHierarchy && !Helicoptero.SoldadoEstaEmbarcando(s)).ToList();
        var tanquesLivres = grupoTanques.Where(t => t != null && t.activeInHierarchy && !t.name.ToLower().Contains("ares")).ToList();
        
        int forcaMilitar = tropasLivres.Count + tanquesLivres.Count;

        if (forcaMilitar >= minimoParaAtacar)
        {
            GameObject novoPelotao = new GameObject($"Cerebro_Pelotao_{contagemEsquadrao}");
            novoPelotao.transform.SetParent(this.transform);
            IA_CerebroPelotao cerebro = novoPelotao.AddComponent<IA_CerebroPelotao>();
            
            Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
            Vector3 dirParaAlvo = (destino - centro).normalized;
            if (dirParaAlvo == Vector3.zero) dirParaAlvo = chefe.transform.forward;

            Vector3 esquerdaLateral = Vector3.Cross(Vector3.up, dirParaAlvo).normalized;
            float distanciaTotal = Vector3.Distance(centro, destino);
            float avancoFrontal = Mathf.Min(600f, distanciaTotal * 0.7f);

            Vector3 offsetFlanco = Vector3.zero;
            int taticaDeFlanco = contagemEsquadrao % 3; 

            if (_inimigoForteEmDefesa && taticaDeFlanco == 0)
            {
                taticaDeFlanco = Random.Range(1, 3); 
            }

            if (taticaDeFlanco == 1) offsetFlanco = esquerdaLateral * 750f; 
            else if (taticaDeFlanco == 2) offsetFlanco = -esquerdaLateral * 750f; 

            Vector3 stagingArea = centro + (dirParaAlvo * avancoFrontal) + offsetFlanco;
            if (Terrain.activeTerrain != null) stagingArea.y = Terrain.activeTerrain.SampleHeight(stagingArea);

            cerebro.Inicializar(stagingArea, BuscarAlvo(), forcaMilitar, $"Omega-{contagemEsquadrao}", chefe.identidade.teamID);
            contagemEsquadrao++;

            foreach(var t in tanquesLivres) { cerebro.AdicionarMembro(t); grupoTanques.Remove(t); }
            foreach(var s in tropasLivres) { cerebro.AdicionarMembro(s); grupoSoldados.Remove(s); }
        }

        Vector3 dir = (destino - chefe.transform.position).normalized;
        if(dir == Vector3.zero) dir = Vector3.forward;

        var helisAtaque = grupoHelis.Where(h => h != null && h.GetComponent<Helicoptero>() != null && h.GetComponent<Helicoptero>().modoCombateAtivo).ToList();
        MoverEmFormacao(helisAtaque, destino, dir, 30f);
    }
    
    public void RegistrarUnidade(GameObject u)
    {
        if (u == null) return;
        if(grupoTanques.Contains(u) || grupoSoldados.Contains(u) || grupoNavios.Contains(u) || grupoHelis.Contains(u) || grupoTransportes.Contains(u) || chefe.meusCivis.Contains(u)) return;
        if (grupoAvioes.Contains(u)) return;

        string n = u.name.ToLower();

        if (n.Contains("civil") || n.Contains("turista") || n.Contains("onibus") || n.Contains("kombi")) chefe.meusCivis.Add(u);
        else if(n.Contains("caca") || n.Contains("tuk") || n.Contains("jet") || n.Contains("aviao") || n.Contains("super") || n.Contains("f22") || n.Contains("caoc")) grupoAvioes.Add(u);
        else if(n.Contains("heli") || n.Contains("apache") || n.Contains("cobra") || n.Contains("falcon") || n.Contains("ray")) grupoHelis.Add(u);
        else if(n.Contains("navio") || n.Contains("fragata") || n.Contains("corveta") || n.Contains("sub") || n.Contains("carrier") || n.Contains("liberty") || n.Contains("hovercraft")) grupoNavios.Add(u);
        else if(n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado") || n.Contains("south") || n.Contains("ubu") || n.Contains("gravity") || n.Contains("ares")) grupoTanques.Add(u);
        else if(u.GetComponent<TransporteTerrestre>() != null) grupoTransportes.Add(u);
        else grupoSoldados.Add(u);
    }

    public void RegistrarSoldado(GameObject u) { RegistrarUnidade(u); }
    public void RegistrarFabrica(Fabrica f)
    {
         if(f != null && f.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID && !minhasFabricas.Contains(f)) minhasFabricas.Add(f);
    }
    public void RegistrarEstaleiro(Estaleiro e, float alcance)
    {
        if(e != null && e.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID && !meusEstaleiros.Contains(e)) meusEstaleiros.Add(e);
    }
    public void RegistrarHeliporto(Heliporto h, float alcance)
    {
        if(h != null && h.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID && !meusHeliportos.Contains(h)) meusHeliportos.Add(h);
    }
    public void RegistrarAeroporto(GerenciadorAeroporto a, float alcance)
    {
        if(a != null && a.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID && !meusAeroportos.Contains(a)) meusAeroportos.Add(a);
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
    }

    Vector3 CalcularPontoDeEncontro()
    {
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
        Vector3 ponto = centro + chefe.transform.forward * 50f;

        ponto = EvitarAeroporto(ponto, centro);
        return ponto;
    }

    Vector3 EvitarAeroporto(Vector3 ponto, Vector3 centroBase)
    {
        var aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach (var aero in aeroportos)
        {
            if (aero == null) continue;
            Bounds b = new Bounds(aero.transform.position, Vector3.zero);
            Renderer[] rends = aero.GetComponentsInChildren<Renderer>();
            foreach (var r in rends) { if (r != null) b.Encapsulate(r.bounds); }
            
            // Aumentei massivamente a zona de repulsão de unidades do aeroporto
            b.Expand(120f);

            if (b.Contains(new Vector3(ponto.x, b.center.y, ponto.z)))
            {
                Vector3 fuga = (centroBase - aero.transform.position).normalized;
                if (fuga.sqrMagnitude < 0.01f) fuga = Vector3.forward;
                fuga.y = 0;
                ponto = b.center + fuga * (b.extents.magnitude + 100f); // Empurra elas com força pra longe

                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(ponto, out hit, 30f, UnityEngine.AI.NavMesh.AllAreas))
                    ponto = hit.position;
            }
        }
        return ponto;
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
        
        // =========================================================
        // FIX: ANTI-SPINNING (Se a unidade já está a 4 metros ou menos do destino, ignora a ordem de andar)
        // Impede que NavMeshAgent fique mandando milímetros e rodando a unidade.
        // =========================================================
        if (Vector3.Distance(u.transform.position, destino) < 4.5f) return;

        var heli = u.GetComponent<Helicoptero>();
        if (heli != null) { heli.Decolar(destino); return; }
        if (Helicoptero.SoldadoEstaEmbarcando(u)) return;

        Vector3 centro = (chefe != null && chefe.basePrincipal != null) ? chefe.basePrincipal.position : u.transform.position;
        destino = EvitarAeroporto(destino, centro);

        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(destino, out navHit, 20f, UnityEngine.AI.NavMesh.AllAreas))
            destino = navHit.position;

        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) { ctrl.MoverParaPonto(destino); return; }

        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }

    public Transform BuscarAlvo()
    {
        var alvos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(a => a.teamID == 1).ToList();
        if (alvos.Count == 0) return null;

        var prefeitura = alvos.FirstOrDefault(a => a.name.ToLower().Contains("prefeitura") || a.GetComponent("ComplexoGovernamental") != null);
        if (prefeitura != null) return prefeitura.transform;

        return alvos[0].transform;
    }
    
    void MoverTropasParaPontoDeEncontro()
    {
        if(chefe.basePrincipal == null) return;
        MoverEmFormacao(grupoTransportes, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 20f, chefe.basePrincipal.forward, 30f);
        MoverEmFormacao(grupoTanques, CalcularPontoDeEncontro(), chefe.basePrincipal.forward, 25f);
        MoverEmFormacao(grupoSoldados, CalcularPontoDeEncontro() - chefe.basePrincipal.forward * 30f, chefe.basePrincipal.forward, 15f);
    }
    
    int TotalUnidades() => grupoSoldados.Count + grupoTanques.Count + grupoHelis.Count + grupoNavios.Count + grupoTransportes.Count;
}