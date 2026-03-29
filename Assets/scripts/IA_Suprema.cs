using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA SUPREMA - PROJETO TITANIUM (V 34.0 - ACESSO AO NAVIO_WALL E TELEPORTE FORÇADO PARA A ÁGUA)
/// MENTOR: Adicionado reconhecimento do Navio_Wall e forçado o teleporte do navio para a água caso o Pier o crie na terra.
/// </summary>
public class IA_Suprema : MonoBehaviour
{
    // ==============================================================
    // ⚙️ CONFIGURAÇÕES DE IDENTIDADE E ECONOMIA
    // ==============================================================
    [Header("Identidade da Nação")]
    public int teamID = 2;
    public string nomeNacao = "Nova Federação Titã";
    public Color corNacao = Color.red;

    [Header("Economia e Inteligência")]
    public float dinheiroIA = 15000f; 
    public float rendaBase = 50f;
    [Range(1, 5)] public int nivelDificuldade = 3;

    [Header("Urbanismo e Altura")]
    public bool permitirMarinha = true; // MENTOR FIX: Agora vem ativado por padrão para já usar o Navio_Wall!
    public float nivelDoMar = 0f;
    public float isolamentoAeroporto = 650f;
    public float cooldownConstrucao = 10f;
    
    [Header("Configuração Naval")]
    [Tooltip("Distância (em metros) que a IA afasta o Pier e os Navios da base terrestre mais próxima.")]
    public float distanciaNavalDaCosta = 200f;

    [Header("REFERÊNCIAS MANUAIS DO COMANDANTE")]
    [Tooltip("Lidos automaticamente. Se quiser, pode arrastar os objetos 'agua' e 'terra' aqui.")]
    public Transform sinalizadorAgua;
    public Transform sinalizadorTerra;

    [Header("Logística de Guerra e Tempo")]
    [Tooltip("Tempo (segundos) de PAZ OBRIGATÓRIA enquanto a IA junta tropas (1 min = 60s)")]
    public float tempoDePazInicial = 60f; 
    private float momentoFimDaPaz = 0f;

    public float distanciaDesembarque = 150f;
    public int metaSoldados = 20;
    public int metaTanques = 10;
    public int metaAereo = 5;
    public int metaCacas = 4;
    public int metaNaval = 3;
    public int metaSubmarinos = 2;

    // ==============================================================
    // 🧠 MEMÓRIA E ESTADOS
    // ==============================================================
    public enum EstadoIA { Acordando, FundandoCapital, DesenvolvimentoUrbano, GuerraTotal, Reagrupamento, DefesaDesesperada }
    public EstadoIA estadoAtual = EstadoIA.Acordando;

    private Dictionary<string, List<GameObject>> biblioteca = new Dictionary<string, List<GameObject>>();
    private List<GameObject> meusPredios = new List<GameObject>();
    private List<GameObject> minhasTropas = new List<GameObject>();
    private List<GameObject> meusTransportes = new List<GameObject>();
    private List<GameObject> meusNavios = new List<GameObject>();
    
    private Transform alvoJogadorBase;
    private Transform alvoJogadorEconomia;
    private bool prefeituraPronta = false;
    private int forcaInimigaAerea = 0;

    // ==============================================================
    // 🚀 INICIALIZAÇÃO E BUSCA GLOBAL
    // ==============================================================
    void Start()
    {
        BuscarSinalizadoresGlobais();
        StartCoroutine(RotinaInicial());
    }

    void BuscarSinalizadoresGlobais()
    {
        if (sinalizadorAgua == null)
        {
            MarcadorSuperficieMapa marcadorAgua = RegistroSuperficieMapa.EncontrarPrimeiro(TipoSuperficieMapa.Agua);
            if (marcadorAgua != null)
            {
                sinalizadorAgua = marcadorAgua.transform;
                nivelDoMar = sinalizadorAgua.position.y;
                Debug.Log($"[IA Suprema] 🌍 SINALIZADOR GLOBAL DE ÁGUA ENCONTRADO EM {sinalizadorAgua.position}");
            }
            else
            {
            GameObject obj = GameObject.Find("agua");
            if (obj == null) obj = GameObject.Find("Agua");
            if (obj != null) 
            {
                sinalizadorAgua = obj.transform;
                nivelDoMar = sinalizadorAgua.position.y;
                Debug.Log($"[IA Suprema] 🌍 SINALIZADOR GLOBAL DE ÁGUA ENCONTRADO EM {sinalizadorAgua.position}");
            }
            }
        }
        
        if (sinalizadorTerra == null)
        {
            MarcadorSuperficieMapa marcadorTerra = RegistroSuperficieMapa.EncontrarPrimeiro(TipoSuperficieMapa.Chao);
            if (marcadorTerra != null)
            {
                sinalizadorTerra = marcadorTerra.transform;
                Debug.Log($"[IA Suprema] 🌍 SINALIZADOR GLOBAL DE TERRA ENCONTRADO EM {sinalizadorTerra.position}");
            }
            else
            {
            GameObject obj = GameObject.Find("terra");
            if (obj == null) obj = GameObject.Find("Terra");
            if (obj != null) 
            {
                sinalizadorTerra = obj.transform;
                Debug.Log($"[IA Suprema] 🌍 SINALIZADOR GLOBAL DE TERRA ENCONTRADO EM {sinalizadorTerra.position}");
            }
            }
        }
    }

    public void ReceberSinalizador(Vector3 posicao, bool ehAgua)
    {
        if (ehAgua && sinalizadorAgua == null) nivelDoMar = posicao.y;
    }

    IEnumerator RotinaInicial()
    {
        Debug.Log($"[IA Suprema] {nomeNacao} a iniciar... Tratado de paz ativado por {tempoDePazInicial}s.");
        estadoAtual = EstadoIA.Acordando;
        
        momentoFimDaPaz = Time.time + 5f + tempoDePazInicial; 

        yield return new WaitForSeconds(5f);

        RealizarScanDeArquivos();

        StartCoroutine(CicloEconomico());
        StartCoroutine(CicloLogistico());
        StartCoroutine(CicloTatico());
        StartCoroutine(CicloManutencao()); 
    }

    IEnumerator CicloEconomico()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            float ganho = rendaBase * (1f + (nivelDificuldade * 0.1f));
            int prediosVivos = meusPredios.Count(p => p != null);
            
            int geradoresRenda = Contar("refinaria") + Contar("plataforma");
            
            ganho += (prediosVivos * 5f) + (geradoresRenda * 20f); 
            dinheiroIA += ganho;
            
            if (dinheiroIA < 0) dinheiroIA = 0; 
        }
    }

    IEnumerator CicloManutencao()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            
            if (dinheiroIA > 1000f) 
            {
                foreach (var predio in meusPredios)
                {
                    if (predio == null) continue;
                    var dmg = predio.GetComponent<SistemaDeDanos>();
                    if (dmg != null && dmg.vidaAtual < dmg.vidaMaxima)
                    {
                        dinheiroIA -= 50f; 
                        dmg.vidaAtual += 150f; 
                        if (dmg.vidaAtual > dmg.vidaMaxima) dmg.vidaAtual = dmg.vidaMaxima;
                    }
                }
            }
        }
    }

    // ==============================================================
    // 🏗️ CÉREBRO LOGÍSTICO E REPULSÃO TERRESTRE (200M)
    // ==============================================================

    Vector3 ObterAncoraNavalSegura()
    {
        BuscarSinalizadoresGlobais();

        Vector3 ancoraNaval = transform.position; 
        if (sinalizadorAgua != null) 
        {
            ancoraNaval = sinalizadorAgua.position;
        }

        float menorDistancia = float.MaxValue;
        Vector3 predioMaisPerto = transform.position; 

        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            string pn = predio.name.ToLower();
            
            if (pn.Contains("estaleiro") || pn.Contains("pier") || pn.Contains("plataforma") || pn.Contains("navio")) continue;

            float dist = Vector3.Distance(ancoraNaval, predio.transform.position);
            if (dist < menorDistancia) 
            {
                menorDistancia = dist;
                predioMaisPerto = predio.transform.position;
            }
        }

        if (menorDistancia < distanciaNavalDaCosta && menorDistancia != float.MaxValue)
        {
            Vector3 dirFugaProMar = (ancoraNaval - predioMaisPerto).normalized;
            dirFugaProMar.y = 0;
            if (dirFugaProMar == Vector3.zero) dirFugaProMar = Vector3.forward;

            ancoraNaval += dirFugaProMar * ((distanciaNavalDaCosta - menorDistancia) + 20f); 
        }
        
        ancoraNaval.y = nivelDoMar;
        return ancoraNaval;
    }

    IEnumerator CicloLogistico()
    {
        while (true)
        {
            LimparMortos();
            AnalisarOponente();

            if (sinalizadorAgua != null) nivelDoMar = sinalizadorAgua.position.y;

            bool fezObra = false;

            if (!prefeituraPronta)
            {
                estadoAtual = EstadoIA.FundandoCapital;
                fezObra = FundarCapitalImediata();
            }
            else
            {
                if (estadoAtual != EstadoIA.DefesaDesesperada)
                {
                    fezObra = GerenciarExpansaoBase();
                }
            }

            if (fezObra)
            {
                Debug.Log($"[IA Suprema] Obra erguida! Pausa logística ({cooldownConstrucao}s).");
                yield return new WaitForSeconds(cooldownConstrucao);
            }
            else
            {
                yield return new WaitForSeconds(3f); 
            }
        }
    }

    bool FundarCapitalImediata()
    {
        if (!biblioteca.ContainsKey("prefeitura")) return false;

        Vector3 pos = transform.position;
        if (sinalizadorTerra != null) pos = sinalizadorTerra.position;

        pos.y = ObterAlturaSolo(pos); 

        SpawnarObjeto(ObterPrefab("prefeitura"), pos, "Prefeitura_Sede");
        prefeituraPronta = true;
        return true;
    }

    bool GerenciarExpansaoBase()
    {
        if (dinheiroIA < 300) return false;

        int quarteis = Contar("quartel");
        int fabricas = Contar("fabrica");
        int refinarias = Contar("refinaria");
        int aeroportos = Contar("aeroporto");
        int estaleiros = Contar("estaleiro");
        int piers = Contar("pier");
        int plataformas = Contar("plataforma");
        int defesas = Contar("torreta");
        int antiAereas = Contar("antiaerea");

        if (aeroportos > 0 && !permitirMarinha)
        {
            permitirMarinha = true;
            Debug.Log("[IA Suprema] O Aeroporto foi concluído! Marinha Liberada!");
        }

        if (refinarias == 0 && meusPredios.Count > 3 && biblioteca.ContainsKey("refinaria"))
        {
            if (TentarEdificar("refinaria", 500, 65f)) return true;
        }

        if (quarteis == 0 && biblioteca.ContainsKey("quartel")) { if (TentarEdificar("quartel", 300, 50f)) return true; }
        if (refinarias == 0 && biblioteca.ContainsKey("refinaria")) { if (TentarEdificar("refinaria", 500, 65f)) return true; }
        if (defesas < 1 && biblioteca.ContainsKey("torreta")) { if (TentarEdificar("torreta", 400, 80f)) return true; }

        if (antiAereas < 2 && dinheiroIA > 800 && biblioteca.ContainsKey("antiaerea"))
        {
            if (TentarEdificarIronDome("antiaerea", 800)) return true;
        }

        if (fabricas == 0 && biblioteca.ContainsKey("fabrica")) { if (TentarEdificar("fabrica", 800, 90f)) return true; }
        if (defesas < 3 && biblioteca.ContainsKey("torreta")) { if (TentarEdificar("torreta", 400, 110f)) return true; }

        if (permitirMarinha && dinheiroIA > 1000) 
        {
            if (estaleiros == 0 && biblioteca.ContainsKey("estaleiro")) { if (TentarEdificarNaAgua("estaleiro", 1500)) return true; }
            if (piers == 0 && biblioteca.ContainsKey("pier")) { if (TentarEdificarNaAgua("pier", 1000)) return true; }

            bool baseNavalPronta = (estaleiros > 0 || !biblioteca.ContainsKey("estaleiro")) && (piers > 0 || !biblioteca.ContainsKey("pier"));
            if (baseNavalPronta && plataformas == 0 && biblioteca.ContainsKey("plataforma"))
            {
                if (TentarEdificarNaAgua("plataforma", 2000)) return true;
            }
        }

        if (aeroportos == 0 && dinheiroIA > 2500 && biblioteca.ContainsKey("aeroporto")) 
        {
            if (TentarEdificar("aeroporto", 2500, isolamentoAeroporto)) return true;
        }

        if (antiAereas < 4 && (forcaInimigaAerea > 0 || dinheiroIA > 3000) && biblioteca.ContainsKey("antiaerea"))
        {
            if (TentarEdificarIronDome("antiaerea", 800)) return true;
        }

        if (refinarias < 2 && dinheiroIA > 2000 && biblioteca.ContainsKey("refinaria")) { if (TentarEdificar("refinaria", 600, 140f)) return true; }
        if (fabricas < 2 && dinheiroIA > 3000 && biblioteca.ContainsKey("fabrica")) { if (TentarEdificar("fabrica", 1000, 160f)) return true; }
        if (defesas < 6 && dinheiroIA > 1000 && biblioteca.ContainsKey("torreta")) { if (TentarEdificar("torreta", 400, 130f)) return true; }

        return false; 
    }

    bool TentarEdificar(string chave, float custo, float distanciaDaBase)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;

        GameObject prefab = ObterPrefab(chave);
        if (prefab == null) return false;
        float meuRaioDeProtecao = CalcularRaioSeguro(chave); 
        bool ehAeroporto = EhAeroporto(chave, prefab);

        Vector3 centroDaBusca = transform.position;
        if (sinalizadorTerra != null) 
        {
            centroDaBusca = sinalizadorTerra.position;
            if (!ehAeroporto) distanciaDaBase = 15f; 
        }

        for (float r = distanciaDaBase; r < distanciaDaBase + 400f; r += 35f)
        {
            int particoes = Mathf.Max(8, Mathf.CeilToInt(r / 15f)); 
            for (int i = 0; i < particoes; i++)
            {
                float ang = i * (360f / particoes) * Mathf.Deg2Rad;
                Vector3 posTeste = centroDaBusca + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                
                float alturaSolo;
                bool ehAgua;
                ObterInfoTerrenoFisico(posTeste, out alturaSolo, out ehAgua);

                if (ehAgua) continue; 

                posTeste.y = alturaSolo; 

                if (ehAeroporto)
                {
                    if (DistanciaParaImovelMaisProximo(posTeste) < 200f) continue;
                    if (!FootprintSecoValidoParaAeroporto(posTeste, meuRaioDeProtecao)) continue;
                }

                if (!LocalOcupado(posTeste, meuRaioDeProtecao))
                {
                    dinheiroIA -= custo;
                    SpawnarObjeto(prefab, posTeste, chave);
                    return true;
                }
            }
        }
        return false;
    }

    bool TentarEdificarIronDome(string chave, float custo)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;
        GameObject prefab = ObterPrefab(chave);
        
        float[] angulosPontas = { 45f, 135f, 225f, 315f };
        Vector3 centroDaBusca = sinalizadorTerra != null ? sinalizadorTerra.position : transform.position;
        
        for (float dist = 110f; dist <= 300f; dist += 40f)
        {
            foreach (float ang in angulosPontas)
            {
                Vector3 posTeste = centroDaBusca + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * dist, 0, Mathf.Sin(ang * Mathf.Deg2Rad) * dist);
                
                float alturaSolo;
                bool ehAgua;
                ObterInfoTerrenoFisico(posTeste, out alturaSolo, out ehAgua);
                if (ehAgua) continue; 
                
                posTeste.y = alturaSolo;
                
                if (!TemPredioNoRaio(posTeste, 95f)) 
                {
                    dinheiroIA -= custo;
                    SpawnarObjeto(prefab, posTeste, chave);
                    Debug.Log($"[IA Suprema] Construiu Sistema Antiaéreo (Ares) na posição {posTeste}");
                    return true;
                }
            }
        }
        return false;
    }

    bool TemPredioNoRaio(Vector3 p, float raio)
    {
        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            if (Vector3.Distance(p, predio.transform.position) < raio) return true;
        }
        return false;
    }

    float DistanciaParaImovelMaisProximo(Vector3 pos)
    {
        float menor = float.MaxValue;
        Imovel[] imoveis = FindObjectsByType<Imovel>(FindObjectsSortMode.None);
        for (int i = 0; i < imoveis.Length; i++)
        {
            Imovel imovel = imoveis[i];
            if (imovel == null) continue;

            float distancia = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(imovel.transform.position.x, 0f, imovel.transform.position.z));
            if (distancia < menor) menor = distancia;
        }

        return menor == float.MaxValue ? 9999f : menor;
    }

    bool FootprintSecoValidoParaAeroporto(Vector3 centro, float raio)
    {
        float amostra = Mathf.Max(20f, raio * 0.9f);
        Vector3[] offsets = new Vector3[]
        {
            Vector3.zero,
            new Vector3(amostra, 0f, 0f),
            new Vector3(-amostra, 0f, 0f),
            new Vector3(0f, 0f, amostra),
            new Vector3(0f, 0f, -amostra),
            new Vector3(amostra, 0f, amostra),
            new Vector3(amostra, 0f, -amostra),
            new Vector3(-amostra, 0f, amostra),
            new Vector3(-amostra, 0f, -amostra)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            float altura;
            bool ehAgua;
            ObterInfoTerrenoFisico(centro + offsets[i], out altura, out ehAgua);
            if (ehAgua) return false;
        }

        return true;
    }

    bool EhAeroporto(string chave, GameObject prefab)
    {
        string nomeNormalizado = (chave + " " + (prefab != null ? prefab.name : string.Empty)).ToLower();
        return nomeNormalizado.Contains("aeroporto")
               || nomeNormalizado.Contains("airport")
               || nomeNormalizado.Contains("pista")
               || (prefab != null && prefab.GetComponent<GerenciadorAeroporto>() != null);
    }

    bool TentarEdificarNaAgua(string chave, float custo)
    {
        if (!permitirMarinha || !biblioteca.ContainsKey(chave) || dinheiroIA < custo) return false;
        
        float meuRaio = CalcularRaioSeguro(chave);
        Vector3 ancoraNaval = ObterAncoraNavalSegura();
            
        for (int i = 0; i < 40; i++)
        {
            Vector3 offset = Vector3.zero;
            if (i > 0)
            {
                float raioEspiral = (i / 8f) * 25f; 
                float angulo = i * 45f * Mathf.Deg2Rad;
                offset = new Vector3(Mathf.Cos(angulo) * raioEspiral, 0, Mathf.Sin(angulo) * raioEspiral);
            }

            Vector3 posTesteAbsoluta = ancoraNaval + offset;
            
            float alturaTeste;
            bool ehAguaConfirmada;
            ObterInfoTerrenoFisico(posTesteAbsoluta, out alturaTeste, out ehAguaConfirmada);

            if (!ehAguaConfirmada) 
            {
                continue; 
            }

            posTesteAbsoluta.y = nivelDoMar; 

            if (!LocalOcupado(posTesteAbsoluta, meuRaio * 0.8f)) 
            {
                dinheiroIA -= custo;
                
                Vector3 dirParaBase = (transform.position - posTesteAbsoluta).normalized;
                dirParaBase.y = 0;
                if(dirParaBase == Vector3.zero) dirParaBase = Vector3.forward;
                
                SpawnarObjeto(ObterPrefab(chave), posTesteAbsoluta, chave, Quaternion.LookRotation(dirParaBase));
                Debug.Log($"[IA Suprema] ⚓ ORDEM CUMPRIDA: '{chave}' construído estritamente na ÁGUA a {distanciaNavalDaCosta}m da terra!");
                return true;
            }
        }
            
        Debug.LogWarning($"[IA Suprema] AVISO: A zona naval está LOTADA ou sem espaço na água! Não faremos nada.");
        return false; 
    }

    // ==============================================================
    // 🛡️ SENSOR DE TERRENO E DISTANCIAMENTO SOCIAL
    // ==============================================================
    float ObterAlturaSolo(Vector3 p)
    {
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryGetAltura(p, TipoSuperficieMapa.Chao, out alturaMarcada))
        {
            return alturaMarcada;
        }

        int mask = ~0; 
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 1000f, p.z), Vector3.down, 2000f, mask, QueryTriggerInteraction.Ignore);
        
        float maiorAlturaChao = -9999f;
        bool achouChao = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            MarcadorSuperficieMapa marcador = hit.collider.GetComponentInParent<MarcadorSuperficieMapa>();
            if (marcador != null)
            {
                if (marcador.TipoSuperficie == TipoSuperficieMapa.Agua)
                {
                    continue;
                }

                float alturaMarcador;
                if (marcador.TrySampleSurfaceHeight(p, out alturaMarcador) && alturaMarcador > maiorAlturaChao)
                {
                    maiorAlturaChao = alturaMarcador;
                    achouChao = true;
                }

                continue;
            }

            string n = hit.collider.gameObject.name.ToLower();
            int layerObj = hit.collider.gameObject.layer;
            
            if (n == "agua" || n.Contains("water") || layerObj == 4 || hit.collider.GetComponent("OceanAdvanced") != null) continue; 
            if (n.Contains("bip001") || n.Contains("bone") || hit.collider.GetComponentInParent<IdentidadeUnidade>() != null) continue;
            
            if (hit.point.y > maiorAlturaChao) 
            {
                maiorAlturaChao = hit.point.y;
                achouChao = true;
            }
        }
        
        return achouChao ? maiorAlturaChao : 0f;
    }

    void ObterInfoTerrenoFisico(Vector3 ponto, out float altura, out bool ehAgua)
    {
        ClassificacaoSuperficieMapa classificacaoMarcada;
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryClassify(ponto, out classificacaoMarcada, out alturaMarcada))
        {
            altura = alturaMarcada;
            ehAgua = classificacaoMarcada == ClassificacaoSuperficieMapa.Agua || classificacaoMarcada == ClassificacaoSuperficieMapa.Costa;
            if (ehAgua && sinalizadorAgua == null)
            {
                nivelDoMar = alturaMarcada;
            }
            return;
        }

        float alturaTerra = ObterAlturaSolo(ponto); 
        
        float alturaAgua = -9999f;
        bool achouAgua = false;

        int mask = ~0; 
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(ponto.x, 1000f, ponto.z), Vector3.down, 2000f, mask, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            MarcadorSuperficieMapa marcador = hit.collider.GetComponentInParent<MarcadorSuperficieMapa>();
            if (marcador != null)
            {
                float alturaMarcador;
                if (!marcador.TrySampleSurfaceHeight(ponto, out alturaMarcador))
                {
                    continue;
                }

                if (marcador.TipoSuperficie == TipoSuperficieMapa.Agua)
                {
                    if (alturaMarcador > alturaAgua)
                    {
                        alturaAgua = alturaMarcador;
                        achouAgua = true;
                    }
                }
                else
                {
                    if (alturaMarcador > alturaTerra)
                    {
                        alturaTerra = alturaMarcador;
                    }
                }

                continue;
            }

            string n = hit.collider.gameObject.name.ToLower();
            int layerObjeto = hit.collider.gameObject.layer;
            bool temScriptOceano = hit.collider.GetComponent("OceanAdvanced") != null;

            if (n == "agua" || n.Contains("water") || n.Contains("sea") || n.Contains("mar") || temScriptOceano || layerObjeto == 4)
            {
                if (hit.point.y > alturaAgua) 
                {
                    alturaAgua = hit.point.y;
                    achouAgua = true;
                }
            }
        }

        if (achouAgua && alturaAgua >= alturaTerra - 0.1f)
        {
            ehAgua = true;
            altura = alturaAgua;
            if(sinalizadorAgua == null) nivelDoMar = alturaAgua; 
        }
        else
        {
            ehAgua = false;
            altura = alturaTerra;
        }
    }

    float CalcularRaioSeguro(string nomeObjeto)
    {
        string n = nomeObjeto.ToLower();
        if (n.Contains("aeroporto") || n.Contains("hangar")) return 200f; 
        if (n.Contains("prefeitura") || n.Contains("complexo")) return 75f;
        if (n.Contains("estaleiro") || n.Contains("pier") || n.Contains("naval") || n.Contains("plataforma")) return 65f;
        if (n.Contains("fabrica") || n.Contains("construtor") || n.Contains("veiculo")) return 60f;
        if (n.Contains("heliporto") || n.Contains("helipad")) return 55f;
        if (n.Contains("quartel") || n.Contains("tenda") || n.Contains("infantaria")) return 45f;
        if (n.Contains("refinaria") || n.Contains("mina") || n.Contains("armazem")) return 45f;
        if (n.Contains("torreta") || n.Contains("defesa") || n.Contains("canhao")) return 25f;
        if (n.Contains("antiaerea") || n.Contains("ares")) return 30f;
        return 35f; 
    }

    bool LocalOcupado(Vector3 p, float raioNecessario)
    {
        foreach(var predio in meusPredios)
        {
            if (predio == null) continue;
            float raioDoOutro = CalcularRaioSeguro(predio.name);
            if (Vector3.Distance(p, predio.transform.position) < (raioNecessario + raioDoOutro)) return true; 
        }

        var outrasEstruturas = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach(var est in outrasEstruturas)
        {
            if (est == null || est.teamID == this.teamID) continue;
            var agent = est.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) continue; 

            float raioDoOutro = CalcularRaioSeguro(est.name);
            if (Vector3.Distance(p, est.transform.position) < (raioNecessario + raioDoOutro)) return true;
        }

        Collider[] hits = Physics.OverlapSphere(p, raioNecessario * 0.7f); 
        foreach (var h in hits) 
        { 
            string nomeDoCollider = h.gameObject.name.ToLower();
            
            if (nomeDoCollider == "agua" || nomeDoCollider == "terra" || h.GetComponent("SinalizadorIA") != null || h.GetComponent("OceanAdvanced") != null || h.GetComponentInParent<MarcadorSuperficieMapa>() != null) 
                continue;

            if (h.gameObject.layer != 4 && h.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast")) 
            {
                if (h.GetComponentInParent<IdentidadeUnidade>() != null || h.GetComponentInParent<NavMeshObstacle>() != null)
                    return true; 
            }
        }
        return false;
    }

    // ==============================================================
    // ⚔️ CÉREBRO TÁTICO E RECRUTAMENTO
    // ==============================================================
    IEnumerator CicloTatico()
    {
        while (true)
        {
            if (prefeituraPronta)
            {
                LimparMortos(); 
                DefinirPosturaGlobal();
                GerenciarProducaoTropas();
                GerenciarLogisticaTransporte();
                GerenciarTaticaNaval();
                
                if (estadoAtual == EstadoIA.GuerraTotal)
                {
                    LancarOfensivaMassa();
                }
                else if (estadoAtual == EstadoIA.Reagrupamento)
                {
                    PatrulharBordasDaBase(); 
                }
                else if (estadoAtual == EstadoIA.DefesaDesesperada)
                {
                    RecuarParaDefesa();
                }
            }
            yield return new WaitForSeconds(4f);
        }
    }

    // ==============================================================
    // ⚓ TÁTICA NAVAL - MANTIDA INTACTA
    // ==============================================================
    void GerenciarTaticaNaval()
    {
        meusNavios.RemoveAll(x => x == null);
        if (meusNavios.Count == 0) return;

        if (estadoAtual == EstadoIA.GuerraTotal && alvoJogadorBase != null)
        {
            Vector3 alvoCosteiro = EncontrarAguaPertoDoAlvo(alvoJogadorBase.position);
            if (alvoCosteiro != Vector3.zero)
            {
                int idx = 0;
                foreach (var navio in meusNavios)
                {
                    if (navio == null) continue;
                    float angForm = idx * 30f * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angForm) * 40f, 0, Mathf.Sin(angForm) * 40f);
                    Vector3 dest = alvoCosteiro + offset;
                    dest.y = nivelDoMar;
                    MoverNavio(navio, dest);
                    idx++;
                }
            }
        }
        else if (estadoAtual == EstadoIA.DefesaDesesperada)
        {
            Vector3 posDefesa = EncontrarPontoNaAgua();
            if (posDefesa != Vector3.zero)
            {
                int idx = 0;
                foreach (var navio in meusNavios)
                {
                    if (navio == null) continue;
                    float angDef = idx * 45f * Mathf.Deg2Rad;
                    Vector3 dest = posDefesa + new Vector3(Mathf.Cos(angDef) * 50f, 0, Mathf.Sin(angDef) * 50f);
                    dest.y = nivelDoMar;
                    MoverNavio(navio, dest);
                    idx++;
                }
            }
        }
        else
        {
            PatrulhaCosteira();
        }
    }

    void PatrulhaCosteira()
    {
        int idx = 0;
        foreach (var navio in meusNavios)
        {
            if (navio == null) continue;

            if (sinalizadorAgua != null)
            {
                Vector3 ancoraNaval = ObterAncoraNavalSegura();
                float angPatr = ((Time.time * 0.05f) + idx * 1.2f) % (2f * Mathf.PI);
                float raioPatr = 100f + idx * 40f; 
                Vector3 pontoPatr = ancoraNaval + new Vector3(Mathf.Cos(angPatr) * raioPatr, 0, Mathf.Sin(angPatr) * raioPatr);
                pontoPatr.y = nivelDoMar;
                
                if (Vector3.Distance(navio.transform.position, pontoPatr) > 30f)
                {
                    MoverNavio(navio, pontoPatr);
                }
                idx++;
                continue;
            }

            float angPatrVelho = ((Time.time * 0.05f) + idx * 1.2f) % (2f * Mathf.PI);
            float raioPatrVelho = 200f + idx * 60f;
            Vector3 pontoPatrVelho = transform.position + new Vector3(Mathf.Cos(angPatrVelho) * raioPatrVelho, 0, Mathf.Sin(angPatrVelho) * raioPatrVelho);

            float altP; bool naAgua;
            ObterInfoTerrenoFisico(pontoPatrVelho, out altP, out naAgua);
            if (naAgua)
            {
                Vector3 dirFundo = (pontoPatrVelho - transform.position).normalized;
                pontoPatrVelho += dirFundo * (distanciaNavalDaCosta * 0.5f);
                pontoPatrVelho.y = nivelDoMar;
                
                if (Vector3.Distance(navio.transform.position, pontoPatrVelho) > 30f)
                {
                    MoverNavio(navio, pontoPatrVelho);
                }
            }
            idx++;
        }
    }

    Vector3 EncontrarAguaPertoDoAlvo(Vector3 alvo)
    {
        if (sinalizadorAgua != null)
        {
            Vector3 ancoraNaval = ObterAncoraNavalSegura();
            Vector3 direcaoParaInimigo = (alvo - ancoraNaval).normalized;
            if (direcaoParaInimigo == Vector3.zero) direcaoParaInimigo = Vector3.forward;
            
            Vector3 posAtaque = ancoraNaval + (direcaoParaInimigo * 120f);
            posAtaque.y = nivelDoMar;
            return posAtaque;
        }

        float melhorDist = float.MaxValue;
        Vector3 melhorPos = Vector3.zero;

        List<Vector3> direcoesDeBusca = new List<Vector3>();
        for (int i = 0; i < 24; i++)
        {
            float ang = i * 15f * Mathf.Deg2Rad;
            direcoesDeBusca.Add(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)));
        }

        foreach (Vector3 dir in direcoesDeBusca)
        {
            for (float d = 50f; d < 1500f; d += 60f)
            {
                Vector3 teste = alvo + dir * d;
                float altT; bool naAgua;
                ObterInfoTerrenoFisico(teste, out altT, out naAgua);
                if (naAgua)
                {
                    Vector3 fundoSeguro = teste + (dir * distanciaNavalDaCosta);
                    
                    if (d < melhorDist)
                    {
                        melhorDist = d;
                        melhorPos = fundoSeguro;
                        melhorPos.y = nivelDoMar;
                    }
                    break; 
                }
            }
        }
        return melhorPos;
    }

    void MoverNavio(GameObject navio, Vector3 destino)
    {
        if (navio == null) return;
        destino.y = nivelDoMar;
        navio.SendMessage("MoverParaPonto", destino, SendMessageOptions.DontRequireReceiver);
        var nav = navio.GetComponent<NavMeshAgent>();
        if (nav != null && nav.isOnNavMesh)
        {
            nav.SetDestination(destino);
            nav.isStopped = false;
        }
    }

    void DefinirPosturaGlobal()
    {
        int soldadosVivos = Contar("soldado");
        int tanquesVivos = Contar("tanque");
        
        if (InimigoNoPortao())
        {
            momentoFimDaPaz = 0f; 
            estadoAtual = EstadoIA.DefesaDesesperada;
            return;
        }

        if (Time.time < momentoFimDaPaz)
        {
            estadoAtual = EstadoIA.Reagrupamento;
            return; 
        }

        int metaRealSoldados = biblioteca.ContainsKey("soldado") ? metaSoldados : 0;
        int metaRealTanques = biblioteca.ContainsKey("tanque") ? metaTanques : 0;

        if (metaRealSoldados == 0 && metaRealTanques == 0)
        {
            estadoAtual = EstadoIA.GuerraTotal;
            return;
        }

        if (soldadosVivos >= metaRealSoldados * 0.8f && tanquesVivos >= metaRealTanques * 0.8f)
        {
            estadoAtual = EstadoIA.GuerraTotal;
        }
        else
        {
            estadoAtual = EstadoIA.Reagrupamento; 
        }
    }

    bool InimigoNoPortao()
    {
        Collider[] invasoes = Physics.OverlapSphere(transform.position, 150f);
        foreach (var i in invasoes)
        {
            IdentidadeUnidade id = i.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.teamID == 1 && !i.name.ToLower().Contains("aviao")) return true; 
        }
        return false;
    }

    void GerenciarProducaoTropas()
    {
        int qtdTropasVivas = minhasTropas.Count(t => t != null);
        if (qtdTropasVivas > 120) return; 

        bool temQuartel = Contar("quartel") > 0 || !biblioteca.ContainsKey("quartel");
        bool temFabrica = Contar("fabrica") > 0 || !biblioteca.ContainsKey("fabrica");
        bool temAereo = Contar("aeroporto") > 0 || Contar("heliporto") > 0 || (!biblioteca.ContainsKey("aeroporto") && !biblioteca.ContainsKey("heliporto"));
        bool temPista = Contar("aeroporto") > 0;
        bool temNaval = Contar("estaleiro") > 0 || Contar("pier") > 0 || Contar("plataforma") > 0 || !biblioteca.ContainsKey("estaleiro");

        if (temQuartel && Contar("soldado") < metaSoldados) TreinarTropa("soldado", 150);
        if (temFabrica && Contar("tanque") < metaTanques) TreinarTropa("tanque", 600);
        
        if (temFabrica && Contar("transporte_aereo") < 2) TreinarTropa("transporte_aereo", 400, true);
        if (temFabrica && Contar("transporte") < 2) TreinarTropa("transporte", 400);

        if (temAereo && Contar("helicoptero") < metaAereo) TreinarTropa("helicoptero", 900, true);

        if (temPista && ContarAvioes() < metaCacas)
        {
            TreinarAviao("caca", 1200);
        }

        if (permitirMarinha && temNaval && ContarNavios() < metaNaval) 
            TreinarTropa("navio", 1500, false, true);

        if (permitirMarinha && temNaval && biblioteca.ContainsKey("submarino") && Contar("submarino") < metaSubmarinos)
            TreinarTropa("submarino", 2000, false, true);
    }

    void TreinarAviao(string chave, float custo)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return;

        var aero = meusPredios.FirstOrDefault(p => p != null && p.name.Contains("aeroporto"));
        if (aero != null)
        {
            var scriptAero = aero.GetComponent<GerenciadorAeroporto>();
            if (scriptAero != null)
            {
                dinheiroIA -= custo;
                scriptAero.ComprarAviao(ObterPrefab(chave));
            }
        }
    }

    int ContarAvioes()
    {
        int count = 0;
        var avioes = FindObjectsByType<ControleAviao>(FindObjectsSortMode.None);
        foreach(var a in avioes)
        {
            var id = a.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == this.teamID) count++;
        }
        return count;
    }

    void TreinarTropa(string chave, float custo, bool voa = false, bool naval = false)
    {
        if (!biblioteca.ContainsKey(chave) || dinheiroIA < custo) return;

        if (naval)
        {
            // MENTOR FIX: Antes mesmo do Pier agir, já achamos um ponto perfeito e molhado!
            Vector3 posAguaGarantida = EncontrarPontoNaAgua();

            GameObject pNaval = null;
            foreach (var p in meusPredios)
            {
                if (p == null) continue;
                string pn = p.name.ToLower();
                if (!(pn.Contains("estaleiro") || pn.Contains("pier") || pn.Contains("plataforma"))) continue;
                
                if (sinalizadorAgua != null) 
                { 
                    pNaval = p; 
                    break; 
                }

                float altP; bool naAgua;
                ObterInfoTerrenoFisico(p.transform.position, out altP, out naAgua);
                if (naAgua) { pNaval = p; break; }
            }

            if (pNaval != null)
            {
                dinheiroIA -= custo;
                
                var fabricaNaval = pNaval.GetComponent<Fabrica>();
                if (fabricaNaval != null)
                {
                     GameObject novoNavio = fabricaNaval.ProduzirUnidade(ObterPrefab(chave));
                     if (novoNavio != null) 
                     {
                         // MENTOR FIX: Se a Fábrica colocou ele na terra por acidente, puxa para a água na marra!
                         if (posAguaGarantida != Vector3.zero)
                         {
                             novoNavio.transform.position = posAguaGarantida;
                             var navAg = novoNavio.GetComponent<NavMeshAgent>();
                             if(navAg != null) navAg.Warp(posAguaGarantida);
                         }
                         meusNavios.Add(novoNavio);
                     }
                }
                else
                {
                    pNaval.SendMessage("ConstruirNavio", ObterPrefab(chave), SendMessageOptions.DontRequireReceiver);
                    pNaval.SendMessage("ConstruirUnidade", ObterPrefab(chave), SendMessageOptions.DontRequireReceiver);
                    StartCoroutine(RegistrarNaviosNovos());
                }
            }
            else
            {
                if (posAguaGarantida != Vector3.zero)
                {
                    dinheiroIA -= custo;
                    GameObject navio = Instantiate(ObterPrefab(chave), posAguaGarantida, Quaternion.identity);
                    navio.name = chave;
                    ConfigurarObjeto(navio, false);
                    meusNavios.Add(navio);
                }
            }
            return;
        }

        Transform spawnPoint = transform;
        Fabrica fabricaComponente = null;
        if (meusPredios.Count > 0)
        {
            var pMilitar = meusPredios.FirstOrDefault(p => p != null && (p.name.ToLower().Contains("fabrica") || p.name.ToLower().Contains("quartel") || p.name.ToLower().Contains("hangar") || p.name.ToLower().Contains("veiculo")));
            if (pMilitar != null) 
            {
                spawnPoint = pMilitar.transform;
                fabricaComponente = pMilitar.GetComponent<Fabrica>();
            }
        }

        Vector3 spawn = spawnPoint.position + spawnPoint.forward * 40f + new Vector3(Random.Range(-15,15), 0, Random.Range(-15,15));
        spawn.y = ObterAlturaSolo(spawn);
        if (voa) spawn.y += 20f;

        if (!voa && !naval)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(spawn, out navHit, 20f, NavMesh.AllAreas))
            {
                spawn = navHit.position;
            }
        }

        dinheiroIA -= custo;
        GameObject nova = null;
        
        if (fabricaComponente != null)
        {
            nova = fabricaComponente.ProduzirUnidade(ObterPrefab(chave));
        }
        else
        {
            nova = Instantiate(ObterPrefab(chave), spawn, Quaternion.identity);
            nova.name = chave;
            ConfigurarObjeto(nova, false);
        }
        
        if (nova == null) return;

        if (chave == "transporte" || chave == "transporte_aereo") meusTransportes.Add(nova);
        else minhasTropas.Add(nova);

        Vector3[] pontosFronteira = ObterPontosDeFronteira();
        Vector3 rallyPoint = pontosFronteira[Random.Range(0, 3)] + new Vector3(Random.Range(-15,15), 0, Random.Range(-15,15));
        rallyPoint.y = ObterAlturaSolo(rallyPoint) + (voa ? 20f : 0f);
        
        if (fabricaComponente != null) StartCoroutine(MoverIAComAtraso(nova, rallyPoint, 2.0f));
        else Mover(nova, rallyPoint);
    }

    IEnumerator MoverIAComAtraso(GameObject unidade, Vector3 destino, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (unidade != null) Mover(unidade, destino);
    }

    // ==============================================================
    // 🚧 SISTEMA DE 3 FRONTEIRAS (Esquerda, Centro, Direita a 110m)
    // ==============================================================
    Vector3[] ObterPontosDeFronteira()
    {
        Vector3[] pontos = new Vector3[3];
        Vector3 centro = transform.position;
        if (sinalizadorTerra != null) centro = sinalizadorTerra.position;
        
        Vector3 frente = transform.forward;
        if (alvoJogadorBase != null)
        {
            frente = (alvoJogadorBase.position - centro).normalized;
            frente.y = 0;
        }
        if (frente == Vector3.zero) frente = Vector3.forward;

        float distFronteira = 110f; 

        pontos[0] = centro + Quaternion.Euler(0, -45f, 0) * frente * distFronteira; 
        pontos[1] = centro + frente * distFronteira; 
        pontos[2] = centro + Quaternion.Euler(0, 45f, 0) * frente * distFronteira; 

        for(int i=0; i<3; i++) 
        {
            pontos[i].y = ObterAlturaSolo(pontos[i]);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pontos[i], out hit, 30f, NavMesh.AllAreas))
            {
                pontos[i] = hit.position;
            }
        }

        return pontos;
    }

    // ==============================================================
    // 🚁 LOGÍSTICA DE TRANSPORTE
    // ==============================================================
    void GerenciarLogisticaTransporte()
    {
        if (alvoJogadorBase == null) return;
        Vector3 centroBase = transform.position;

        foreach (var veiculo in meusTransportes)
        {
            if (veiculo == null) continue;

            bool voa = veiculo.name == "transporte_aereo";

            float distAlvo = Vector3.Distance(veiculo.transform.position, alvoJogadorBase.position);
            float distBase = Vector3.Distance(veiculo.transform.position, centroBase);

            Vector3 direcaoParaAlvo = (alvoJogadorBase.position - centroBase).normalized;
            Vector3 pontoDeEjeccao = alvoJogadorBase.position - (direcaoParaAlvo * distanciaDesembarque); 
            pontoDeEjeccao.y = ObterAlturaSolo(pontoDeEjeccao) + (voa ? 20f : 0f);

            bool emMissaoOfensiva = (estadoAtual == EstadoIA.GuerraTotal);
            
            int passageiros = 0;
            int capacidade = 4; 
            if (veiculo.name.Contains("helicoptero") || veiculo.name.Contains("transporte_aereo"))
            {
                var h = veiculo.GetComponent<Helicoptero>();
                if (h != null) { passageiros = h.soldadosEmbarcados.Count; capacidade = h.capacidadeMaxima; }
            }
            else
            {
                var t = veiculo.GetComponent<TransporteTerrestre>();
                if (t != null) { passageiros = t.QuantidadePassageiros; capacidade = t.capacidadeMaxima; }
            }

            bool prontoParaGuerra = (passageiros >= capacidade * 0.7f) || (distBase > 150f && passageiros > 0);

            if (emMissaoOfensiva && prontoParaGuerra)
            {
                if (distAlvo > distanciaDesembarque + 10f)
                {
                    Mover(veiculo, pontoDeEjeccao);
                }
                else if (distAlvo <= distanciaDesembarque + 15f)
                {
                    bool isHeli = veiculo.name.Contains("helicoptero") || veiculo.name.Contains("transporte_aereo");
                    if (isHeli)
                    {
                        veiculo.SendMessage("OrdemPousoOuDesembarque", SendMessageOptions.DontRequireReceiver);
                    }
                    else
                    {
                        veiculo.SendMessage("DesembarcarTudo", SendMessageOptions.DontRequireReceiver);
                        veiculo.SendMessage("OrdemPousoOuDesembarque", SendMessageOptions.DontRequireReceiver);
                        Mover(veiculo, centroBase); 
                    }
                }
            }
            else if (!emMissaoOfensiva || passageiros == 0)
            {
                if (distBase > 80f)
                {
                    Mover(veiculo, centroBase + new Vector3(Random.Range(-30, 30), 0, Random.Range(-30, 30)));
                }
                else
                {
                    bool isHeli = veiculo.name.Contains("helicoptero") || veiculo.name.Contains("transporte_aereo");
                    if (isHeli)
                    {
                        veiculo.SendMessage("OrdemPousoOuDesembarque", SendMessageOptions.DontRequireReceiver);
                    }
                    
                    veiculo.SendMessage("ChamarReforcos", SendMessageOptions.DontRequireReceiver);
                    veiculo.SendMessage("TentarEmbarcar", SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    void LancarOfensivaMassa()
    {
        if (alvoJogadorBase == null) return;

        foreach (var t in minhasTropas)
        {
            if (t == null) continue;
            if (t.transform.parent != null) continue; 

            bool isHeli = t.name == "helicoptero";
            Vector3 alvoTropa = alvoJogadorBase.position;
            
            if (isHeli && alvoJogadorEconomia != null) 
            {
                alvoTropa = alvoJogadorEconomia.position; 
            }
            
            Vector3 alvoLocal = alvoTropa + new Vector3(Random.Range(-120f, 120f), 0, Random.Range(-120f, 120f));
            Mover(t, alvoLocal);
        }

        foreach (var predio in meusPredios)
        {
            if (predio == null || !predio.name.Contains("aeroporto")) continue;
            
            var aero = predio.GetComponent<GerenciadorAeroporto>();
            if (aero != null)
            {
                Vector3 alvoAereo = (alvoJogadorEconomia != null) ? alvoJogadorEconomia.position : alvoJogadorBase.position;
                foreach(var aviao in aero.avioesNoPatio)
                {
                    if (aviao != null && aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                    {
                        aviao.IniciarMissaoCompleta(alvoAereo);
                    }
                }
            }
        }
    }

    void PatrulharBordasDaBase()
    {
        Vector3[] fronteiras = ObterPontosDeFronteira();
        int i = 0;

        foreach (var tropa in minhasTropas)
        {
            if (tropa == null || tropa.transform.parent != null) continue;

            Vector3 pontoBase = fronteiras[i % 3]; 

            Vector3 posRonda = pontoBase + new Vector3(Mathf.Cos(Time.time + i) * 15f, 0, Mathf.Sin(Time.time + i) * 15f);
            
            bool voa = tropa.name == "helicoptero";
            posRonda.y = ObterAlturaSolo(posRonda) + (voa ? 20f : 0f);
            
            if (Vector3.Distance(tropa.transform.position, posRonda) > 20f)
            {
                Mover(tropa, posRonda);
            }
            
            i++;
        }
    }

    void RecuarParaDefesa()
    {
        foreach (var t in minhasTropas)
        {
            if (t == null || t.transform.parent != null) continue;
            bool voa = t.name == "helicoptero";
            Vector3 fuga = transform.position + new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-30f, 30f));
            fuga.y = ObterAlturaSolo(fuga) + (voa ? 20f : 0f);
            Mover(t, fuga);
        }
    }

    // ==============================================================
    // 🔍 SCANNER GLOBAL E IDENTIFICAÇÃO
    // ==============================================================
    void AnalisarOponente()
    {
        var unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(u => u.teamID == 1).ToList();
        if (unidades.Count == 0) return;

        alvoJogadorEconomia = null; 

        foreach (var u in unidades)
        {
            if (u == null) continue;
            string n = u.name.ToLower();
            if (n.Contains("prefeitura") || n.Contains("complexo")) alvoJogadorBase = u.transform;
            
            if (n.Contains("refinaria") || n.Contains("mina") || n.Contains("petroleo") || n.Contains("armazem"))
            {
                alvoJogadorEconomia = u.transform;
            }
        }
        
        if (alvoJogadorBase == null && unidades.Count > 0) alvoJogadorBase = unidades[0].transform;
    }

    void SpawnarObjeto(GameObject prefab, Vector3 pos, string nome, Quaternion rot = default)
    {
        if (rot == default) rot = Quaternion.identity;

        if (EhAeroporto(nome, prefab))
        {
            if (DistanciaParaImovelMaisProximo(pos) < 200f) return;
            if (!FootprintSecoValidoParaAeroporto(pos, CalcularRaioSeguro(nome))) return;
        }

        string nLower = nome.ToLower();
        bool ehNavalItem = nLower.Contains("estaleiro") || nLower.Contains("pier") || nLower.Contains("naval") || nLower.Contains("plataforma");
        if (ehNavalItem)
        {
            if (sinalizadorAgua != null)
            {
                pos.y = nivelDoMar;
            }
            else
            {
                float altCheck; bool naAguaCheck;
                ObterInfoTerrenoFisico(pos, out altCheck, out naAguaCheck);
                if (!naAguaCheck)
                {
                    Vector3 posAgua = EncontrarPontoNaAgua();
                    if (posAgua == Vector3.zero) return;
                    pos = posAgua;
                    rot = Quaternion.identity;
                }
                pos.y = nivelDoMar;
            }
        }

        GameObject novo = Instantiate(prefab, pos, rot);
        novo.name = nome;
        ConfigurarObjeto(novo, true);
        meusPredios.Add(novo);
    }

    Vector3 EncontrarPontoNaAgua()
    {
        if (sinalizadorAgua != null)
        {
            Vector3 ancoraNaval = ObterAncoraNavalSegura();
            Vector3 pontoSeguro = ancoraNaval + new Vector3(Random.Range(-20f, 20f), 0, Random.Range(-20f, 20f));
            
            float testAlt; bool isAgua;
            ObterInfoTerrenoFisico(pontoSeguro, out testAlt, out isAgua);
            if(isAgua)
            {
                pontoSeguro.y = nivelDoMar;
                return pontoSeguro;
            }
        }

        List<Vector3> direcoesDeBusca = new List<Vector3>();
        for (int i = 0; i < 24; i++) 
        {
            float ang = i * 15f * Mathf.Deg2Rad;
            direcoesDeBusca.Add(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)));
        }

        foreach (Vector3 dir in direcoesDeBusca)
        {
            for (float d = 50f; d < 3000f; d += 80f)
            {
                Vector3 teste = transform.position + dir * d;
                float altAgua; bool ehAgua;
                ObterInfoTerrenoFisico(teste, out altAgua, out ehAgua);
                if (ehAgua)
                {
                    Vector3 fundoSeguro = teste + (dir * distanciaNavalDaCosta);
                    
                    float checkAlt; bool checkAgua;
                    ObterInfoTerrenoFisico(fundoSeguro, out checkAlt, out checkAgua);
                    if(checkAgua)
                    {
                        fundoSeguro.y = nivelDoMar;
                        return fundoSeguro;
                    }
                }
            }
        }
        return Vector3.zero;
    }

    void ConfigurarObjeto(GameObject obj, bool ehPredio)
    {
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (!id) id = obj.AddComponent<IdentidadeUnidade>();
        id.teamID = teamID; id.nomeDoPais = nomeNacao;

        BoxCollider[] boxes = obj.GetComponentsInChildren<BoxCollider>(true);
        foreach(var box in boxes)
        {
            Vector3 s = box.transform.lossyScale;
            if (s.x < 0 || s.y < 0 || s.z < 0)
            {
                GameObject g = box.gameObject;
                DestroyImmediate(box);
                g.AddComponent<MeshCollider>().convex = true;
            }
        }

        if (obj.GetComponent<Collider>() == null)
        {
            Vector3 s = obj.transform.lossyScale;
            if (s.x < 0 || s.y < 0 || s.z < 0) obj.AddComponent<MeshCollider>().convex = true;
            else obj.AddComponent<BoxCollider>();
        }

        var raycasters = obj.GetComponentsInChildren<GraphicRaycaster>(true);
        foreach (var gr in raycasters) Destroy(gr);

        var dmg = obj.GetComponent<SistemaDeDanos>();
        if (!dmg) { dmg = obj.AddComponent<SistemaDeDanos>(); dmg.vidaMaxima = 1500; dmg.vidaAtual = 1500; }

        var nav = obj.GetComponent<NavMeshAgent>();
        var obs = obj.GetComponent<NavMeshObstacle>();
        if (ehPredio) { if(nav) nav.enabled = false; if(obs) obs.enabled = true; }
        else { if(obs) obs.enabled = false; if(nav) nav.enabled = true; }
    }

    void RealizarScanDeArquivos()
    {
        if (MenuConstrucao.catalogoGlobal != null)
        {
            foreach (var item in MenuConstrucao.catalogoGlobal)
            {
                if (item == null || item.prefabDaUnidade == null) continue;
                string n = (item.nomeItem + " " + item.prefabDaUnidade.name).ToLower();
                Mapear(n, item.prefabDaUnidade);
            }
        }
    }

    void Mapear(string n, GameObject obj)
    {
        if (n.Contains("prefeitura") || n.Contains("complexo") || n.Contains("governo")) AddLib("prefeitura", obj);
        else if (n.Contains("quartel") || n.Contains("tenda") || n.Contains("barraca")) AddLib("quartel", obj);
        else if (n.Contains("fabrica") || n.Contains("construtor") || n.Contains("hangar")) AddLib("fabrica", obj);
        else if (n.Contains("plataforma") || n.Contains("platform")) AddLib("plataforma", obj); 
        else if (n.Contains("refinaria") || n.Contains("petroleo") || n.Contains("mina")) AddLib("refinaria", obj);
        else if (n.Contains("antiaerea") || n.Contains("ares") || n.Contains("sam") || n.Contains("missil")) AddLib("antiaerea", obj); 
        else if (n.Contains("torreta") || n.Contains("defesa") || n.Contains("canhao")) AddLib("torreta", obj);
        else if (n.Contains("aeroporto") || n.Contains("pista")) AddLib("aeroporto", obj);
        else if (n.Contains("estaleiro") || n.Contains("naval")) AddLib("estaleiro", obj); 
        else if (n.Contains("pier") || n.Contains("porto")) AddLib("pier", obj); 
        else if (n.Contains("soldado") || n.Contains("infantaria") || n.Contains("fuzileiro") || n.Contains("person")) AddLib("soldado", obj);
        else if (n.Contains("tanque") || n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado")) AddLib("tanque", obj);
        else if (n.Contains("ray") || n.Contains("guincho")) AddLib("transporte_aereo", obj);
        else if (n.Contains("heli") || n.Contains("apache") || n.Contains("cobra")) AddLib("helicoptero", obj);
        else if (n.Contains("transporte") || n.Contains("caminhao") || n.Contains("truck")) AddLib("transporte", obj);
        else if (n.Contains("caca") || n.Contains("aviao") || n.Contains("jet") || n.Contains("tuk") || n.Contains("super") || n.Contains("g15")) AddLib("caca", obj);
        else if (n.Contains("submarino") || n.Contains("submarine")) AddLib("submarino", obj);
        // MENTOR FIX: Inclusão explícita da palavra 'wall' para garantir que seu Navio_Wall seja reconhecido!
        else if (n.Contains("navio") || n.Contains("wall") || n.Contains("corveta") || n.Contains("fragata") || n.Contains("barco") || n.Contains("lancha") || n.Contains("marinha") || n.Contains("hovercraft") || n.Contains("hover")) AddLib("navio", obj); 
    }

    void AddLib(string k, GameObject o) 
    { 
        if (!biblioteca.ContainsKey(k)) biblioteca.Add(k, new List<GameObject>()); 
        if (!biblioteca[k].Contains(o)) biblioteca[k].Add(o);
    }
    
    GameObject ObterPrefab(string k)
    {
        if (biblioteca.ContainsKey(k) && biblioteca[k].Count > 0)
        {
            return biblioteca[k][Random.Range(0, biblioteca[k].Count)];
        }
        return null;
    }
    
    void LimparMortos() 
    { 
        meusPredios.RemoveAll(x => x == null); 
        minhasTropas.RemoveAll(x => x == null); 
        meusTransportes.RemoveAll(x => x == null);
        meusNavios.RemoveAll(x => x == null);
    }
    
    int Contar(string k) => 
        (meusPredios.Count(x => x != null && x.name == k)) + 
        (minhasTropas.Count(x => x != null && x.name == k)) + 
        (meusTransportes.Count(x => x != null && x.name == k)) +
        (meusNavios.Count(x => x != null && x.name == k));

    int ContarNavios()
    {
        meusNavios.RemoveAll(x => x == null);
        return meusNavios.Count;
    }

    IEnumerator RegistrarNaviosNovos()
    {
        yield return new WaitForSeconds(3f);
        var todas = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todas)
        {
            if (u == null || u.teamID != this.teamID) continue;
            string n = u.name.ToLower();
            bool ehNavio = n.Contains("navio") || n.Contains("corveta") || n.Contains("fragata") || 
                           n.Contains("barco") || n.Contains("lancha") || n.Contains("sub") || 
                           n.Contains("hovercraft") || n.Contains("hover") || n.Contains("marinha");
            if (!ehNavio) continue;
            if (meusNavios.Contains(u.gameObject)) continue;
            meusNavios.Add(u.gameObject);
        }
    }
    
    // ==============================================================
    // 🧠 SISTEMA DE INTELIGÊNCIA ESPACIAL (ANTI-STUCK)
    // ==============================================================
    void Mover(GameObject u, Vector3 d) 
    { 
        if (u == null) return;

        bool isAereo = u.name == "helicoptero" || u.name == "transporte_aereo" || u.name.Contains("caca") || u.name.Contains("aviao");

        if (isAereo)
        {
            if (d.y <= ObterAlturaSolo(d) + 5f) 
            {
                d.y = ObterAlturaSolo(d) + 20f;
            }
            
            u.SendMessage("Decolar", d, SendMessageOptions.DontRequireReceiver);
            u.SendMessage("MoverParaPonto", d, SendMessageOptions.DontRequireReceiver); 
            return;
        }
 
        d = DesviarDePrediosAliados(d);

        var nav = u.GetComponent<NavMeshAgent>(); 
        if (nav && nav.isOnNavMesh) 
        {
            nav.SetDestination(d); 
            nav.isStopped = false;
        } 
        else 
        {
            u.SendMessage("MoverParaPonto", d, SendMessageOptions.DontRequireReceiver);
        }
    }

    Vector3 DesviarDePrediosAliados(Vector3 destino)
    {
        foreach (var predio in meusPredios)
        {
            if (predio == null) continue;
            
            float raioOcupacao = CalcularRaioSeguro(predio.name) * 0.8f; 
            float dist = Vector3.Distance(destino, predio.transform.position);
            
            if (dist < raioOcupacao)
            {
                Vector3 direcaoFuga = (destino - predio.transform.position).normalized;
                if (direcaoFuga == Vector3.zero) direcaoFuga = Vector3.forward;
                
                destino = predio.transform.position + (direcaoFuga * (raioOcupacao + 10f));
                destino.y = ObterAlturaSolo(destino);
            }
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(destino, out hit, 15f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return destino;
    }
}
