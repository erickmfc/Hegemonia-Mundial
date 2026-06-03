using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;

/// <summary>
/// IA Arquiteto Pro: Responsável por urbanismo militar e economia.
/// FIX: Zona de Colisão massivamente aumentada para evitar que hangares "grudem" no aeroporto.
/// </summary>
public class IA_Arquiteto_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    private bool baseIniciada = false;
    private float tempoUltimaBandeira = 0f;

    public float nivelDoMar = 0f; 

    [Header("Machine Learning Urbano")]
    public bool aprenderComJogador = true;
    private Dictionary<string, float> distanciasAprendidas = new Dictionary<string, float>();

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        
        // 🧠 Inicia o novo Cérebro com a sua Regra dos 10 Segundos!
        StartCoroutine(RotinaDePensamentoMilitar());
        
        if (aprenderComJogador)
        {
            InvokeRepeating("EspionarUrbanismoInimigo", 20.0f, 60.0f);
        }
    }

    // ==========================================
    // 🧠 O NOVO CÉREBRO DO ARQUITETO
    // ==========================================
    System.Collections.IEnumerator RotinaDePensamentoMilitar()
    {
        // REGRA 1: Os 10 Primeiros Segundos (Suspensão Profunda)
        // Não faz ABSOLUTAMENTE NADA por 10 segundos reais.
        yield return new WaitForSeconds(10f);

        while (true)
        {
            // REGRA 4: Proteção de Intrusão
            // Se você já está com o menu aberto, ela recusa-se a acordar.
            while (MenuConstrucao.EstaAberto || Input.GetKey(KeyCode.C))
            {
                yield return null; // Fica congelada esperando 1 frame
            }

            // REGRA 2: A "Janela de Ouro" (Exatos 2 Segundos de pensamento espaçado)
            bool intrusaoDetectada = false;

            // --- Ação 1 (Início da Janela) ---
            if (!baseIniciada) PlanejarBaseMilitar();
            else ConstruirAeroportoCedo();
            
            yield return new WaitForSeconds(0.5f); // Descansa 0.5s para não engasgar
            
            // Checa se você abriu o menu nesse meio tempo
            if (MenuConstrucao.EstaAberto || Input.GetKey(KeyCode.C)) intrusaoDetectada = true;

            // --- Ação 2 (Meio da Janela) ---
            if (!intrusaoDetectada)
            {
                GarantirEExpandirFrotaAerea();
                yield return new WaitForSeconds(0.5f);
                if (MenuConstrucao.EstaAberto || Input.GetKey(KeyCode.C)) intrusaoDetectada = true;
            }

            // --- Ação 3 (Fim da Janela) ---
            if (!intrusaoDetectada)
            {
                VerificarIntegridadeEExpandir();
                yield return new WaitForSeconds(1.0f); // Fecha a matemática dos 2 segundos
            }

            // REGRA 3: Standby Imediato (Cooldown de 10s)
            // Não importa se ela fez tudo ou se você assustou ela abrindo o menu... ela desliga por 10s.
            yield return new WaitForSeconds(10f);
        }
    }

    void EspionarUrbanismoInimigo()
    {
        var unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        var prefeituraInimiga = unidades.FirstOrDefault(u => u.teamID == 1 && (u.name.ToLower().Contains("prefeitura") || u.GetComponent("ComplexoGovernamental") != null));
        if (prefeituraInimiga == null) return;

        Vector3 centroInimigo = prefeituraInimiga.transform.position;

        foreach (var u in unidades)
        {
            if (u.teamID == 1) 
            {
                string nome = u.name.ToLower();
                string categoria = "";

                if (nome.Contains("aeroporto") || u.GetComponent<GerenciadorAeroporto>() != null) categoria = "Aeroporto";
                else if (nome.Contains("torreta") || nome.Contains("defesa")) categoria = "Torreta";
                else if (nome.Contains("ares") || nome.Contains("anti")) categoria = "Ares";
                else if (nome.Contains("veiculo") || nome.Contains("hangar") || nome.Contains("fabrica")) categoria = "Veiculos";
                else if (nome.Contains("tenda") || nome.Contains("quartel")) categoria = "Tenda";
                else if (nome.Contains("muro") || nome.Contains("cerca") || nome.Contains("wall")) categoria = "Muro";

                if (!string.IsNullOrEmpty(categoria))
                {
                    float dist = Vector3.Distance(centroInimigo, u.transform.position);
                    
                    if (!distanciasAprendidas.ContainsKey(categoria)) 
                        distanciasAprendidas[categoria] = dist;
                    else 
                        distanciasAprendidas[categoria] = Mathf.Lerp(distanciasAprendidas[categoria], dist, 0.5f); 
                }
            }
        }
    }

    void ConstruirAeroportoCedo()
    {
        if (chefe == null) return;
        if (ExistePredio("Aeroporto")) return; 

        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        ConstruirNaTerra("Aeroporto", centro, 0); 
    }

    void GarantirEExpandirFrotaAerea()
    {
        if (chefe == null || chefe.dinheiro < 1500f || !ExistePredio("Aeroporto")) return;

        GameObject cacaPrefab = BuscarNoCatalogo("Fighter");
        if (cacaPrefab == null) cacaPrefab = BuscarNoCatalogo("Caca");
        if (cacaPrefab == null) cacaPrefab = BuscarNoCatalogo("Avia");

        if (cacaPrefab != null)
        {
            var aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            foreach (var aero in aeroportos)
            {
                if (aero == null) continue;
                var id = aero.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == chefe.identidade.teamID)
                {
                    // Verifica se o aeroporto tem vagas no pátio
                    int vagasTotais = aero.waypointsPatio.Count;
                    int ocupados = aero.avioesNoPatio.Count + aero.avioesNoHangar.Count; // Considera hangar também para evitar sobrecarga excessiva

                    if (ocupados < vagasTotais)
                    {
                        chefe.GastarDinheiro(1000f);
                        aero.ComprarAviao(cacaPrefab);
                        break; // Compra um por vez para distribuir nos ciclos
                    }
                }
            }
        }
    }

    void GarantirFrotaNavalCosteira(Vector3 centroBase)
    {
        if (chefe == null || chefe.dinheiro < 2500f) return;

        GameObject navioPrefab = BuscarNoCatalogo("NavalPatrol");
        if (navioPrefab == null) return;

        var estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        foreach (var est in estaleiros)
        {
            if (est == null) continue;
            var id = est.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == chefe.identidade.teamID)
            {
                chefe.GastarDinheiro(2000f);
                
                // Encontra um ponto aleatório de água afastado para defender a costa
                Vector3 direcaoAleatoria = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                Vector3 pontoPatrulha = est.transform.position + (direcaoAleatoria * Random.Range(150f, 400f));
                pontoPatrulha.y = nivelDoMar;

                // Verifica se o ponto é água de fato
                if (Terrain.activeTerrain != null && Terrain.activeTerrain.SampleHeight(pontoPatrulha) <= nivelDoMar - 0.1f)
                {
                    GameObject novoNavio = Instantiate(navioPrefab, est.transform.position + (est.transform.forward * 40f), Quaternion.LookRotation(est.transform.forward));
                    ConfigurarIdentidade(novoNavio);
                    
                    var compMover = novoNavio.GetComponent<ControleNavioRealista>();
                    if (compMover != null)
                    {
                        compMover.DefinirDestino(pontoPatrulha);
                    }
                }
                break; // Compra um navio por vez
            }
        }
    }

    void VerificarIntegridadeEExpandir()
    {
        if (!baseIniciada || chefe == null) return;

        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        if (Time.time - tempoUltimaBandeira > 120f && chefe.dinheiro > 300f) 
        {
            Vector3 direcaoInimigo = chefe.transform.forward;
            Vector3 posBandeira = centro + (direcaoInimigo * Random.Range(150f, 300f));
            ConstruirNaTerra("Bandeira", posBandeira, 100);
            tempoUltimaBandeira = Time.time;
            return; // Espera o próximo ciclo de 10s se já gastou comprando bandeira
        }

        if (chefe.dinheiro > 400 && Random.value > 0.4f) 
        {
            ConstruirPorCategoria(DadosConstrucao.CategoriaItem.Infraestrutura, centro);
        }

        if (!ExistePredio("Quartel") && !ExistePredio("Tenda"))
        {
            if (ConstruirNaTerra("Tenda", centro, 500)) return; 
        }
        
        if (!ExistePredio("Veiculos") && !ExistePredio("Hangar")) 
        {
            if (ConstruirNaTerra("Veiculos", centro, 800)) return; 
        }
        else if (ContarPredios("Veiculos") < 3 && chefe.dinheiro > 1800f)
        {
            if (ConstruirNaTerra("Veiculos", centro, 1000)) return; 
        }

        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 500f)
        {
            if (ConstruirNaTerra("Aeroporto", centro, 500)) return; 
        }
        else if (!ExistePredio("Heliporto") && chefe.dinheiro >= 3000f)
        {
            if (ConstruirNaTerra("Heliporto", centro, 3000)) return; 
        }

        // --- EXPANSÃO DE ECONOMIA E UTILIDADES ---
        if (chefe.dinheiro > 600f)
        {
            int qtdUsinas = ContarPredios("Energia") + ContarPredios("Usina");
            if (qtdUsinas < 3) 
            {
                if (ConstruirNaTerra("Energia", centro, 500)) return;
            }

            int qtdFazendas = ContarPredios("Fazenda") + ContarPredios("Comida");
            if (qtdFazendas < 2) 
            {
                if (ConstruirNaTerra("Fazenda", centro, 400)) return;
            }

            if (!ExistePredio("Comercial")) 
            {
                if (ConstruirNaTerra("Comercial", centro, 300)) return;
            }

            if (!ExistePredio("Residencial") && !ExistePredio("Casa")) 
            {
                if (ConstruirNaTerra("Residencial", centro, 300)) return;
            }
        }
        
        if (chefe.dinheiro > 1500 && !ExistePredio("Estaleiro") && !ExistePredio("Pier") && !ExistePredio("Naval"))
        {
            Vector3 posAgua = EncontrarAgua(centro, 50f, 600f);
            if (posAgua != Vector3.zero)
            {
                if (GerenteDeTerritorio.Instancia != null && GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posAgua) == chefe.identidade.teamID)
                {
                    Vector3 dirMar = (posAgua - centro).normalized;
                    ConstruirNaAgua("Estaleiro", posAgua, dirMar);
                    ConstruirNaAgua("Pier", posAgua, dirMar); 
                }
            }
        }
        else if ((ExistePredio("Estaleiro") || ExistePredio("Pier")) && chefe.dinheiro > 2500)
        {
            GarantirFrotaNavalCosteira(centro);
        }

        if (chefe.dinheiro > 1000)
        {
            int qtdAA = ContarPredios("Antiaerea") + ContarPredios("Aerea") + ContarPredios("AA") + ContarPredios("Ares");
            int qtdSolo = ContarPredios("Torreta") + ContarPredios("Defesa"); 
            qtdSolo -= qtdAA; 
            if (qtdSolo < 0) qtdSolo = 0;

            if (qtdAA < 4 && chefe.dinheiro >= 800) ConstruirAresEstrategico(centro, 800);
            else if (qtdSolo < 6 && chefe.dinheiro >= 500) ConstruirDefesaInteligente("Torreta", centro, 500);
        }
        
        if (chefe.dinheiro > 600)
        {
            int qtdMuros = ContarPredios("Muro") + ContarPredios("Cerca") + ContarPredios("Wall");
            if (qtdMuros < 15 && chefe.dinheiro >= 200) ConstruirDefesaInteligente("Muro", centro, 100);
        }
    }

    int ContarPredios(string nomeParcial)
    {
        if (chefe == null || chefe.identidade == null) return 0;
        int count = 0;

        Fabrica[] fabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach (var f in fabricas)
        {
            if (f == null) continue;
            var id = f.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == chefe.identidade.teamID) 
            {
                string nomeLimpo = f.name.ToLower();
                if (nomeLimpo.Contains(nomeParcial.ToLower())) count++;
                else if (nomeParcial == "Veiculos" && (nomeLimpo.Contains("hangar") || nomeLimpo.Contains("fabrica") || nomeLimpo.Contains("veiculo") || nomeLimpo.Contains("construtor"))) count++;
                else if (nomeParcial == "Hangar" && (nomeLimpo.Contains("veiculo") || nomeLimpo.Contains("hangar") || nomeLimpo.Contains("construtor"))) count++;
                else if (nomeParcial == "Tenda" && (nomeLimpo.Contains("quartel") || nomeLimpo.Contains("tenda") || nomeLimpo.Contains("infantaria"))) count++;
                else if (nomeParcial == "Antiaerea" && nomeLimpo.Contains("ares")) count++; 
            }
        }
        
        if (nomeParcial == "Muro" || nomeParcial == "Cerca" || nomeParcial == "Wall")
        {
            IdentidadeUnidade[] todasIdentidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            foreach(var id in todasIdentidades)
            {
                if (id.teamID == chefe.identidade.teamID && (id.name.ToLower().Contains("muro") || id.name.ToLower().Contains("cerca") || id.name.ToLower().Contains("wall"))) count++;
            }
        }
        
        if (nomeParcial.Contains("Estaleiro") || nomeParcial.Contains("Pier")) count += FindObjectsByType<Estaleiro>(FindObjectsSortMode.None).Count(e => e != null && e.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID);
        if (nomeParcial.Contains("Heliporto")) count += FindObjectsByType<Heliporto>(FindObjectsSortMode.None).Count(h => h != null && h.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID);
        if (nomeParcial.ToLower().Contains("aeroporto")) count += FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None).Count(a => a != null && a.GetComponent<IdentidadeUnidade>()?.teamID == chefe.identidade.teamID);

        return count;
    }

    bool ExistePredio(string nomeParcial) => ContarPredios(nomeParcial) > 0;

    void PlanejarBaseMilitar()
    {
        if (baseIniciada) return;

        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0 || chefe == null)
        {
             chefe = GetComponent<IA_Comandante>();
             Invoke("PlanejarBaseMilitar", 2.0f);
             return;
        }

        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        if (!ExistePredio("Prefeitura") && !ExistePredio("Complexo")) 
        {
            ConstruirNaTerra("Prefeitura", centro, 0);
            
            // Força a criação de uma Usina Nuclear a ~150m de distância ao lado
            Vector3 posUsina = centro + (chefe.transform.right * 150f) + (chefe.transform.forward * 50f);
            ConstruirNaTerra("Nuclear", posUsina, 0);
        }
        if (!ExistePredio("Bandeira") && !ExistePredio("Flag")) ConstruirNaTerra("Bandeira", centro, 0);
        if (!ExistePredio("Tenda")) ConstruirNaTerra("Tenda", centro, 0);
        if (!ExistePredio("Veiculos")) ConstruirNaTerra("Veiculos", centro, 500); 
        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 100f) ConstruirNaTerra("Aeroporto", centro, 100); 

        baseIniciada = true;
    }

    float CalcularRaioDoPrefab(GameObject prefab)
    {
        if (prefab == null) return 10f; 
        if (prefab.name.ToLower().Contains("aeroporto") || prefab.GetComponent<GerenciadorAeroporto>() != null) return 260f; // Força margem absurda para a base toda do aeroporto e pista
        
        BoxCollider box = prefab.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            float maxSize = Mathf.Max(box.size.x * box.transform.localScale.x, box.size.z * box.transform.localScale.z);
            return maxSize / 2f; 
        }
        return 15f; 
    }

    Vector3 EncontrarVagaNoTerreno(Vector3 centro, float raioDaConstrucao, float distanciaPreferencial = -1f)
    {
        float margemDeSeguranca = 40f; 
        float raioTotalNecessario = raioDaConstrucao + margemDeSeguranca;
        
        float raioInicial = Mathf.Max(raioTotalNecessario, 60f);
        if (distanciaPreferencial > 0)
        {
            raioInicial = Mathf.Max(raioTotalNecessario, distanciaPreferencial); 
        }

        for (float raioBusca = raioInicial; raioBusca < raioInicial + 600f; raioBusca += 18f)
        {
            int passosNoCirculo = Mathf.CeilToInt((2 * Mathf.PI * raioBusca) / 14f); 
            for (int i = 0; i < passosNoCirculo; i++)
            {
                float angulo = (i * 360f / passosNoCirculo) * Mathf.Deg2Rad;
                Vector3 posSugerida = centro + new Vector3(Mathf.Cos(angulo) * raioBusca, 0, Mathf.Sin(angulo) * raioBusca);
                
                if (Terrain.activeTerrain != null) 
                    posSugerida.y = Terrain.activeTerrain.SampleHeight(posSugerida);

                if (DentroDeAeroporto(posSugerida)) continue;
                if (posSugerida.y < nivelDoMar - 0.5f) continue;
                if (!TemPredioProximo(posSugerida, raioTotalNecessario))
                    return posSugerida;
            }
        }
        
        return centro + (chefe.transform.forward * 300f) + (chefe.transform.right * Random.Range(-100f, 100f));
    }

    bool ConstruirNaTerra(string nomeChave, Vector3 centro, int custoMinimo)
    {
        string nomeBaixo = nomeChave.ToLower();
        if (nomeBaixo.Contains("estaleiro") || nomeBaixo.Contains("pier") || nomeBaixo.Contains("naval") || nomeBaixo.Contains("navio")) return false; 

        if (chefe == null || chefe.dinheiro < custoMinimo) return false;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return false;

        float raioIdeal = -1f;
        if (nomeBaixo.Contains("aeroporto")) 
        {
            raioIdeal = distanciasAprendidas.ContainsKey("Aeroporto") ? distanciasAprendidas["Aeroporto"] : 650f;
            if (raioIdeal < 650f) raioIdeal = 650f; 
        }
        else if (nomeBaixo.Contains("veiculo") && distanciasAprendidas.ContainsKey("Veiculos")) raioIdeal = distanciasAprendidas["Veiculos"];
        else if (nomeBaixo.Contains("tenda") && distanciasAprendidas.ContainsKey("Tenda")) raioIdeal = distanciasAprendidas["Tenda"];

        float raioExato = CalcularRaioDoPrefab(prefab);
        Vector3 posVaga = EncontrarVagaNoTerreno(centro, raioExato, raioIdeal);

        if (GerenteDeTerritorio.Instancia != null && !nomeBaixo.Contains("bandeira"))
        {
            int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posVaga);
            if (dono != chefe.identidade.teamID && dono != 0) return false; 
        }

        SpawnarPredio(prefab, posVaga, Quaternion.identity);
        return true;
    }

    void ConstruirPorCategoria(DadosConstrucao.CategoriaItem categoriaDesejada, Vector3 centro)
    {
        if (MenuConstrucao.catalogoGlobal == null || chefe == null) return;

        var itensPossiveis = MenuConstrucao.catalogoGlobal
            .Where(i => i.categoria == categoriaDesejada && i.preco <= chefe.dinheiro)
            .Where(i => { 
                string nm = i.nomeItem.ToLower();
                bool ehNaval = nm.Contains("estaleiro") || nm.Contains("pier") || nm.Contains("naval") 
                    || nm.Contains("navio") || nm.Contains("corveta") || nm.Contains("fragata") 
                    || nm.Contains("barco") || i.categoria == DadosConstrucao.CategoriaItem.Marinha;
                return !ehNaval;
            })
            .ToList();

        if (itensPossiveis.Count > 0)
        {
            DadosConstrucao item = itensPossiveis[Random.Range(0, itensPossiveis.Count)];
            float raio = CalcularRaioDoPrefab(item.prefabDaUnidade);
            Vector3 pos = EncontrarVagaNoTerreno(centro, raio);

            SpawnarPredio(item.prefabDaUnidade, pos, Quaternion.identity);
        }
    }

    void ConstruirAresEstrategico(Vector3 centro, int custoMinimo)
    {
        if (chefe == null || chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo("Ares");
        if (prefab == null) prefab = BuscarNoCatalogo("Antiaerea");
        if (prefab == null) return;

        var alvosInimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None)
            .Where(u => u != null && u.teamID == 1)
            .ToList();

        Vector3 dirParaInimigo = chefe.transform.forward; 
        if (alvosInimigos.Count > 0)
        {
            Vector3 centroInimigo = Vector3.zero;
            foreach (var u in alvosInimigos) centroInimigo += u.transform.position;
            centroInimigo /= alvosInimigos.Count;
            dirParaInimigo = (centroInimigo - centro).normalized;
        }

        float raioDaTorre = CalcularRaioDoPrefab(prefab);

        float distIdeal = distanciasAprendidas.ContainsKey("Ares") ? distanciasAprendidas["Ares"] : 150f;
        
        float[] distancias = { distIdeal, distIdeal + 30f, distIdeal - 30f, 210f, 80f, 250f };
        float[] angulos    = { 0f, 20f, -20f, 35f, -35f, 10f, -10f };

        foreach (float dist in distancias)
        {
            foreach (float angDelta in angulos)
            {
                Quaternion rotacao = Quaternion.Euler(0, angDelta, 0);
                Vector3 dirAjustada = rotacao * dirParaInimigo;

                Vector3 posSugerida = centro + dirAjustada * dist;
                if (Terrain.activeTerrain != null)
                    posSugerida.y = Terrain.activeTerrain.SampleHeight(posSugerida);

                if (GerenteDeTerritorio.Instancia != null)
                {
                    int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posSugerida);
                    if (dono != chefe.identidade.teamID && dono != 0) continue;
                }

                bool muitoPerto = false;
                foreach (var unidade in chefe.minhasUnidades)
                {
                    if (unidade == null) continue;
                    if (Vector3.Distance(unidade.transform.position, posSugerida) < 100f)
                    {
                        muitoPerto = true;
                        break;
                    }
                }
                if (muitoPerto) continue;

                if (TemPredioProximo(posSugerida, raioDaTorre + 8f)) continue;

                SpawnarPredio(prefab, posSugerida, Quaternion.LookRotation(dirParaInimigo));
                return;
            }
        }

        ConstruirDefesaInteligente("Antiaerea", centro, custoMinimo);
    }

    void ConstruirDefesaInteligente(string nomeChave, Vector3 centro, int custoMinimo)
    {
        if (chefe == null || chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        bool ehMuro = nomeChave.ToLower().Contains("muro") || nomeChave.ToLower().Contains("cerca");

        if (ehMuro)
        {
            Vector3 dirFrente = chefe.transform.forward;
            Vector3 dirLado = chefe.transform.right;
            
            float larguraDoMuro = CalcularRaioDoPrefab(prefab) * 2f; 
            if (larguraDoMuro < 5f) larguraDoMuro = 15f; 
            
            float distIdeal = distanciasAprendidas.ContainsKey("Muro") ? distanciasAprendidas["Muro"] : 150f;

            Vector3 centroMuralha = centro + (dirFrente * distIdeal);
            if (Terrain.activeTerrain != null) centroMuralha.y = Terrain.activeTerrain.SampleHeight(centroMuralha);

            for (int i = -4; i <= 4; i++)
            {
                Vector3 posMuro = centroMuralha + (dirLado * (i * larguraDoMuro)); 
                if (Terrain.activeTerrain != null) posMuro.y = Terrain.activeTerrain.SampleHeight(posMuro);

                if (!TemPredioProximo(posMuro, larguraDoMuro * 0.4f)) 
                {
                    SpawnarPredio(prefab, posMuro, Quaternion.LookRotation(dirFrente));
                    chefe.GastarDinheiro(100);
                }
            }
            return;
        }

        float raioDaTorre = CalcularRaioDoPrefab(prefab);
        for (int i = 0; i < 16; i++)
        {
             float ang = (360f / 16f) * i * Mathf.Deg2Rad;
             
             float distanciaBorda = distanciasAprendidas.ContainsKey("Torreta") ? distanciasAprendidas["Torreta"] : 100f; 
             
             if (nomeChave.ToLower().Contains("anti") || nomeChave.ToLower().Contains("aerea") || nomeChave.ToLower().Contains("ares"))
             {
                 distanciaBorda = distanciasAprendidas.ContainsKey("Ares") ? distanciasAprendidas["Ares"] : Random.Range(100f, 200f); 
             }

             Vector3 dirExt = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
             Vector3 posSugerida = centro + (dirExt * distanciaBorda);
             
             if (Terrain.activeTerrain != null) posSugerida.y = Terrain.activeTerrain.SampleHeight(posSugerida);

             if (!TemPredioProximo(posSugerida, raioDaTorre + 5f))
             {
                 SpawnarPredio(prefab, posSugerida, Quaternion.LookRotation(dirExt));
                 return; 
             }
        }
    }

    void ConstruirNaAgua(string nomeChave, Vector3 posicaoCosta, Vector3 direcaoMar)
    {
        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        Vector3 posFinal = posicaoCosta + (direcaoMar.normalized * 50f); 
        posFinal.y = nivelDoMar; 

        float alturaTerreno = 0f;
        if (Terrain.activeTerrain != null)
            alturaTerreno = Terrain.activeTerrain.SampleHeight(posFinal);

        if (alturaTerreno > nivelDoMar - 0.1f)
        {
            for (float extra = 60f; extra <= 200f; extra += 20f)
            {
                posFinal = posicaoCosta + (direcaoMar.normalized * extra);
                posFinal.y = nivelDoMar;
                if (Terrain.activeTerrain != null)
                    alturaTerreno = Terrain.activeTerrain.SampleHeight(posFinal);
                if (alturaTerreno <= nivelDoMar - 0.1f)
                    break; 
            }

            if (Terrain.activeTerrain != null)
                alturaTerreno = Terrain.activeTerrain.SampleHeight(posFinal);
            if (alturaTerreno > nivelDoMar - 0.1f)
            {
                Debug.Log($"[IA Arquiteto] CANCELADO: {nomeChave} não construído — posição não é água!");
                return;
            }
        }

        SpawnarPredio(prefab, posFinal, Quaternion.LookRotation(direcaoMar));
    }

    Vector3 EncontrarAgua(Vector3 centro, float raioMin, float raioMax)
    {
        int tentativas = 36; 
        float raioLimite = Mathf.Max(raioMax, 400f); 
        for (int i = 0; i < tentativas; i++)
        {
            float angulo = (360f / tentativas) * i * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo));
            bool estavaNaTerra = true;

            for (float dist = raioMin; dist < raioLimite; dist += 10f) 
            {
                Vector3 pontoTeste = centro + dir * dist;
                float altura = 0f;
                if (Terrain.activeTerrain != null) altura = Terrain.activeTerrain.SampleHeight(pontoTeste);

                bool estaNaAgua = (altura <= nivelDoMar - 0.1f); 

                if (estavaNaTerra && estaNaAgua)
                {
                    Vector3 pontoNaAgua = pontoTeste + (dir * 30f);
                    float alturaVerificacao = 0f;
                    if (Terrain.activeTerrain != null)
                        alturaVerificacao = Terrain.activeTerrain.SampleHeight(pontoNaAgua);
                    
                    if (alturaVerificacao <= nivelDoMar - 0.1f)
                        return pontoNaAgua;
                    else
                        return pontoTeste; 
                }
                estavaNaTerra = !estaNaAgua;
            }
        }
        return Vector3.zero;
    }

    void SpawnarPredio(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        GameObject novo = Instantiate(prefab, pos, rot);
        ConfigurarIdentidade(novo);
        if(chefe != null) chefe.GastarDinheiro(200); 
    }

    bool DentroDeAeroporto(Vector3 posicao)
    {
        var aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach (var aero in aeroportos)
        {
            if (aero == null) continue;

            if (Vector3.Distance(posicao, aero.transform.position) < 340f) return true;
        }
        return false;
    }

    bool TemPredioProximo(Vector3 posicao, float raioMinimo)
    {
        float raioEfetivo = Mathf.Max(raioMinimo, 45f);
        Collider[] vizinhos = Physics.OverlapSphere(posicao, raioEfetivo);
        foreach (var col in vizinhos)
        {
            if (col == null || col is TerrainCollider) continue;
            
            if (col.GetComponentInParent<GerenciadorAeroporto>() != null) return true;
            if (col.GetComponentInParent<IdentidadeUnidade>() != null) return true;
            if (col.GetComponentInParent<Fabrica>() != null) return true;
            if (col.GetComponentInParent<Estaleiro>() != null) return true;
            if (col.GetComponentInParent<Heliporto>() != null) return true;
            if (col.GetComponentInParent<SistemaDeDanos>()?.ehEstrutura == true) return true;
        }
        return false;
    }

    void ConfigurarIdentidade(GameObject obj)
    {
        if (obj == null) return;
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.AddComponent<IdentidadeUnidade>();
        id.teamID = (chefe != null && chefe.identidade != null) ? chefe.identidade.teamID : 2; 
        
        var fab = obj.GetComponent<Fabrica>();
        if (fab != null && chefe?.cerebroGeneral != null) chefe.cerebroGeneral.RegistrarFabrica(fab);
    }

    GameObject BuscarNoCatalogo(string nomeChave)
    {
        GameObject prefabAchado = null;
        if (MenuConstrucao.catalogoGlobal != null)
        {
            foreach (var item in MenuConstrucao.catalogoGlobal)
            {
                 string nmBusca = item.nomeItem.ToLower();
                 bool ehNavalBusca = nmBusca.Contains("navio") || nmBusca.Contains("corveta") || nmBusca.Contains("fragata") || nmBusca.Contains("sub") || nmBusca.Contains("barco") || item.categoria == DadosConstrucao.CategoriaItem.Marinha;
                 if (ehNavalBusca && nomeChave != "Estaleiro" && nomeChave != "Pier" && nomeChave != "Naval") continue;

                 if (nmBusca.Contains(nomeChave.ToLower())) { prefabAchado = item.prefabDaUnidade; break; }
            }

            if (prefabAchado == null)
            {
                foreach (var item in MenuConstrucao.catalogoGlobal) 
                {
                    string nm = item.nomeItem.ToLower();
                    bool ehNaval = nm.Contains("navio") || nm.Contains("corveta") || nm.Contains("fragata") || nm.Contains("sub") || nm.Contains("barco") || item.categoria == DadosConstrucao.CategoriaItem.Marinha;
                    if (ehNaval && nomeChave != "Estaleiro" && nomeChave != "Pier" && nomeChave != "Naval") continue;

                    if (nomeChave == "Veiculos" && (nm.Contains("hangar") || nm.Contains("fabrica") || nm.Contains("factory") || nm.Contains("veiculos") || nm.Contains("construtor"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Tenda" && (nm.Contains("quartel") || nm.Contains("barraca") || nm.Contains("infantaria") || nm.Contains("tenda"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Antiaerea" && (nm.Contains("anti") || nm.Contains("aérea") || nm.Contains("aerea") || nm.Contains("patriot") || nm.Contains("sam") || nm.Contains("missil") || nm.Contains("ares"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Torreta" && (nm.Contains("torreta") || nm.Contains("defesa") || nm.Contains("bunker") || nm.Contains("canhao") || nm.Contains("metralhadora"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Muro" && (nm.Contains("muro") || nm.Contains("cerca") || nm.Contains("wall") || nm.Contains("barricada"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Aeroporto" && (nm.Contains("aeroporto") || nm.Contains("base aerea") || nm.Contains("pista") || nm.Contains("airport") || nm.Contains("hangar") && (nm.Contains("voo") || nm.Contains("aviao") || nm.Contains("aereo")))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Pier" && nm.Contains("pier")) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Energia" && (nm.Contains("usina") || nm.Contains("energia") || nm.Contains("solar") || nm.Contains("nuclear") || nm.Contains("power"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Fazenda" && (nm.Contains("fazenda") || nm.Contains("comida") || nm.Contains("farm") || nm.Contains("agricola"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Comercial" && (nm.Contains("comercial") || nm.Contains("loja") || nm.Contains("shopping"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Residencial" && (nm.Contains("residencial") || nm.Contains("casa") || nm.Contains("predio") || nm.Contains("house"))) prefabAchado = item.prefabDaUnidade;
                }
            }
        }

        if (prefabAchado != null) return prefabAchado;
        if (nomeChave.ToLower().Contains("estaleiro") || nomeChave.ToLower().Contains("pier") || nomeChave.ToLower().Contains("naval")) return null;
        
        if (nomeChave == "Aeroporto")
        {
            var aeroNaCena = Object.FindFirstObjectByType<GerenciadorAeroporto>();
            if (aeroNaCena != null) return aeroNaCena.gameObject;
        }

        GameObject[] todosRecursos = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in todosRecursos)
        {
            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;
            if (obj.name.ToLower().Contains(nomeChave.ToLower()))
            {
                if (obj.GetComponent<SistemaDeDanos>() != null || nomeChave == "Aeroporto" || nomeChave == "Bandeira") return obj;
            }
        }

        return null;
    }
}