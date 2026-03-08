using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Linq;

/// <summary>
/// IA Arquiteto Pro: Responsável por urbanismo militar.
/// Constrói base com layout aberto e espaçado, SEM prender unidades.
/// ATUALIZADO: Modo Tira-Restrições (God Mode) e Força bruta de posicionamento.
/// </summary>
public class IA_Arquiteto_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    private bool baseIniciada = false;

    [Header("Configurações de Construção")]
    public float espacamentoEdificios = 25f; // Aumentado para evitar aperto
    public float nivelDoMar = 0f; // Altura da água para estaleiros

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Start()
    {
        if (chefe == null) chefe = GetComponent<IA_Comandante>();
        
        // Aguarda um pouco para garantir que o catálogo do MenuConstrucao carregou
        Invoke("PlanejarBaseMilitar", 4.0f);

        // --- MANUTENÇÃO DE BASE ---
        InvokeRepeating("VerificarIntegridadeEExpandir", 15.0f, 15.0f);
    }

    void VerificarIntegridadeEExpandir()
    {
        if (!baseIniciada || chefe == null) return;

        Debug.Log("🏗️ [IA Arquiteto] Verificando base...");
        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        // 1. QUARTEL (Soldados)
        if (!ExistePredio("Quartel") && !ExistePredio("Tenda"))
        {
            ConstruirNaTerra("Tenda", centro, 500); 
        }

        // 2. FÁBRICA DE VEÍCULOS (Tanques)
        if (!ExistePredio("Veiculos") && !ExistePredio("Hangar"))
        {
            ConstruirNaTerra("Veiculos", centro, 800); 
        }
        else if (ContarPredios("Veiculos") < 4 && chefe.dinheiro > 1800f)
        {
            ConstruirNaTerra("Veiculos", centro, 1000, 50f);
        }

        // 3. AEROPORTO - Ajustado para 120 metros (Visível, mas protegido na fronteira)
        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 500f)
        {
            Debug.Log("🏗️ [IA Arquiteto] Economia Forte! Projetando construção do Aeroporto Tático Militar...");
            ConstruirNaTerra("Aeroporto", centro, 500, 120f); 
        }
        else if (!ExistePredio("Heliporto") && chefe.dinheiro >= 3000f)
        {
            ConstruirNaTerra("Heliporto", centro, 3000, 30f); 
        }
        
        // 4. ESTALEIRO (Navais)
        if (chefe.dinheiro > 1500 && !ExistePredio("Estaleiro") && !ExistePredio("Naval"))
        {
            Vector3 posAgua = EncontrarAgua(centro, 50f, 600f);
            if (posAgua != Vector3.zero)
            {
                if (GerenteDeTerritorio.Instancia != null)
                {
                    int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posAgua);
                    if (dono == chefe.identidade.teamID)
                    {
                        Vector3 dirMar = (posAgua - centro).normalized;
                        ConstruirNaAgua("Estaleiro", posAgua, dirMar);
                    }
                    else
                    {
                        if (chefe.dinheiro > 500)
                        {
                            Vector3 dirMar = (posAgua - centro).normalized;
                            Vector3 posBandeira = centro + (dirMar * (espacamentoEdificios * 3f));
                            if (Terrain.activeTerrain != null) posBandeira.y = Terrain.activeTerrain.SampleHeight(posBandeira);
                            ConstruirNaTerra("Bandeira", posBandeira, 0, 50f);
                        }
                    }
                }
            }
        }

        // 5. CINTURÃO DE DEFESA (Torretas e AA)
        if (chefe.dinheiro > 1000)
        {
            int qtdAA = ContarPredios("Antiaerea") + ContarPredios("Aerea") + ContarPredios("AA");
            int qtdSolo = ContarPredios("Torreta") + ContarPredios("Defesa"); 
            qtdSolo -= qtdAA; 
            if (qtdSolo < 0) qtdSolo = 0;

            if (qtdAA < 3 && chefe.dinheiro >= 800)
            {
                ConstruirDefesaInteligente("Antiaerea", centro, 800);
            }
            else if (qtdSolo < 5 && chefe.dinheiro >= 500)
            {
                ConstruirDefesaInteligente("Torreta", centro, 500);
            }
        }
        
        // 6. MUROS
        if (chefe.dinheiro > 600)
        {
            int qtdMuros = ContarPredios("Muro") + ContarPredios("Cerca") + ContarPredios("Wall");
            if (qtdMuros < 10 && chefe.dinheiro >= 200) ConstruirDefesaInteligente("Muro", centro, 100);
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
                else if (nomeParcial == "Veiculos" && (nomeLimpo.Contains("hangar") || nomeLimpo.Contains("fabrica") || nomeLimpo.Contains("veiculo"))) count++;
                else if (nomeParcial == "Hangar" && (nomeLimpo.Contains("veiculo") || nomeLimpo.Contains("hangar"))) count++;
                else if (nomeParcial == "Tenda" && (nomeLimpo.Contains("quartel") || nomeLimpo.Contains("tenda"))) count++;
            }
        }
        
        if (nomeParcial == "Muro" || nomeParcial == "Cerca" || nomeParcial == "Wall")
        {
            IdentidadeUnidade[] todasIdentidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            foreach(var id in todasIdentidades)
            {
                if (id.teamID == chefe.identidade.teamID)
                {
                    string nm = id.name.ToLower();
                    if (nm.Contains("muro") || nm.Contains("cerca") || nm.Contains("wall") || nm.Contains("parede")) count++;
                }
            }
        }
        
        if (nomeParcial.Contains("Estaleiro"))
        {
            Estaleiro[] estaleiros = FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
            foreach(var e in estaleiros) 
            {
                 if(e == null) continue;
                 var id = e.GetComponent<IdentidadeUnidade>();
                 if (id != null && id.teamID == chefe.identidade.teamID) count++;
            }
        }

        if (nomeParcial.Contains("Heliporto"))
        {
            Heliporto[] heliportos = FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
            foreach(var h in heliportos)
            {
                if(h == null) continue;
                var id = h.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == chefe.identidade.teamID) count++;
            }
        }

        if (nomeParcial.ToLower().Contains("aeroporto"))
        {
            GerenciadorAeroporto[] aeroportos = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            foreach(var a in aeroportos)
            {
                if(a == null) continue;
                var id = a.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == chefe.identidade.teamID) count++;
            }
        }

        return count;
    }

    bool ExistePredio(string nomeParcial)
    {
        return ContarPredios(nomeParcial) > 0;
    }

    void PlanejarBaseMilitar()
    {
        if (baseIniciada) return;

        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
        {
             Invoke("PlanejarBaseMilitar", 2.0f);
             return;
        }

        if (chefe == null) 
        {
             chefe = GetComponent<IA_Comandante>();
             if (chefe == null) 
             { 
                 Invoke("PlanejarBaseMilitar", 2.0f);
                 return; 
             }
        }

        Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;

        if (!ExistePredio("Prefeitura") && !ExistePredio("Complexo")) ConstruirNaTerra("Prefeitura", centro, 0);
        if (!ExistePredio("Bandeira") && !ExistePredio("Flag")) ConstruirNaTerra("Bandeira", centro, 0);
        if (!ExistePredio("Tenda")) ConstruirNaTerra("Tenda", centro, 0);
        if (!ExistePredio("Veiculos")) ConstruirNaTerra("Veiculos", centro, 500); 

        // AEROPORTO - Construído no começo mas perto (120m)
        if (!ExistePredio("Aeroporto") && chefe.dinheiro >= 100f) 
        {
             ConstruirNaTerra("Aeroporto", centro, 100, 120f); 
        }

        baseIniciada = true;
    }

    void ConstruirNaTerra(string nomeChave, Vector3 centro, int custoMinimo, float espacamentoCustom = -1f)
    {
        if (chefe == null) return;
        if (chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) 
        {
            Debug.LogWarning($"<color=red>⛔ [IA Arquiteto] ERRO CRÍTICO: Não achei '{nomeChave}' de forma alguma! Ele não existe no projeto!</color>");
            return;
        }

        bool ehBandeiraOuPref = nomeChave.ToLower().Contains("bandeira") || nomeChave.ToLower().Contains("flag") || nomeChave.ToLower().Contains("prefeit");
        bool ehAeroporto = nomeChave.ToLower().Contains("aeroporto");
        if (ehAeroporto) ehBandeiraOuPref = true; 

        float espMaior = espacamentoCustom > 0f ? espacamentoCustom : espacamentoEdificios;
        float bolhaDeColisao = (espMaior > 100f) ? 80f : espMaior;

        // REMOÇÃO DE RESTRIÇÕES EXTREMAS: O Aeroporto ganha passe livre e pode nascer mais na marra!
        for (int i = 0; i < 20; i++)
        {
            float margemReal = (i > 10) ? bolhaDeColisao * 0.5f : bolhaDeColisao;
            Vector3 pos = EncontrarPosicaoEspiral(centro, i, 0f, espMaior);
            
            // Ignora colisão brutal se for Aeroporto na tentativa 15 em diante
            bool ignorarColisao = (ehAeroporto && i >= 15);

            if (!TemPredioProximo(pos, margemReal) || ignorarColisao) 
            {
                if (GerenteDeTerritorio.Instancia != null && !ehBandeiraOuPref)
                {
                    int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(pos);
                    int idInimigo = chefe.identidade.teamID;
                    
                    if (dono != idInimigo && dono != 0) continue; // Fora daqui só se for do inimigo
                }

                float yTerra = 0;
                if (Terrain.activeTerrain != null) yTerra = Terrain.activeTerrain.SampleHeight(pos);
                pos.y = yTerra;

                SpawnarPredio(prefab, pos, Quaternion.identity);

                if (ehAeroporto && chefe.dinheiro > 100)
                {
                    GameObject prefabBandeiraX = BuscarNoCatalogo("Bandeira");
                    if (prefabBandeiraX != null)
                    {
                         Vector3 posPostoAvancado = pos + new Vector3(30, 0, 30);
                         if (Terrain.activeTerrain != null) posPostoAvancado.y = Terrain.activeTerrain.SampleHeight(posPostoAvancado);
                         SpawnarPredio(prefabBandeiraX, posPostoAvancado, Quaternion.identity);
                    }
                }
                return;
            }
        }
        
        // FORÇA BRUTA DEFINITIVA NA FRONTEIRA: 
        // Bota para criar ali no local determinado custe o que custar!
        Vector3 dirAleatoria = chefe.transform.forward; // Joga pra frente na direção do inimigo
        dirAleatoria.y = 0;
        float distanciaFinal = espacamentoCustom > 0f ? espacamentoCustom : (espacamentoEdificios * 4f);
        Vector3 posForcada = centro + (dirAleatoria.normalized * distanciaFinal);
        if (Terrain.activeTerrain != null) posForcada.y = Terrain.activeTerrain.SampleHeight(posForcada);
        
        Debug.LogWarning($"<color=cyan>⚡ [IA Arquiteto] TIRANDO RESTRIÇÕES. Construindo {nomeChave} FORÇADO na fronteira XYZ: {posForcada}!</color>");
        SpawnarPredio(prefab, posForcada, Quaternion.identity);
    }

    void ConstruirDefesaInteligente(string nomeChave, Vector3 centro, int custoMinimo)
    {
        if (chefe == null || chefe.dinheiro < custoMinimo) return;

        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        int meuTime = chefe.identidade.teamID;

        for (int i = 0; i < 16; i++)
        {
             float ang = (360f / 16f) * i * Mathf.Deg2Rad;
             float distanciaBorda = espacamentoEdificios * Random.Range(1.0f, 2.8f); 
             float raioAfastamentoPredio = 15f;
             
             if (nomeChave.ToLower().Contains("anti") || nomeChave.ToLower().Contains("aerea") || nomeChave.ToLower().Contains("ares"))
             {
                 distanciaBorda = Random.Range(90f, 150f); 
                 raioAfastamentoPredio = 60f; 
             }
             else if (nomeChave.ToLower().Contains("muro") || nomeChave.ToLower().Contains("cerca"))
             {
                 distanciaBorda = Random.Range(100f, 140f);
                 raioAfastamentoPredio = 5f; 
             }

             Vector3 dirExt = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
             Vector3 posSugerida = centro + (dirExt * distanciaBorda);
             
             if (Terrain.activeTerrain != null) posSugerida.y = Terrain.activeTerrain.SampleHeight(posSugerida);

             if (TemPredioProximo(posSugerida, raioAfastamentoPredio)) continue;

             if (GerenteDeTerritorio.Instancia != null)
             {
                 int dono = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(posSugerida);
                 if (dono != meuTime && dono != 0) continue; 
             }
             
             Quaternion rotacaoFinal = Quaternion.LookRotation(dirExt);
             if (nomeChave.ToLower().Contains("muro") || nomeChave.ToLower().Contains("cerca")) rotacaoFinal *= Quaternion.Euler(0, 90, 0); 
             
             SpawnarPredio(prefab, posSugerida, rotacaoFinal);
             return; 
        }
    }

    void ConstruirNaAgua(string nomeChave, Vector3 posicaoCosta, Vector3 direcaoMar)
    {
        GameObject prefab = BuscarNoCatalogo(nomeChave);
        if (prefab == null) return;

        Quaternion rot = Quaternion.LookRotation(direcaoMar);
        Vector3 posFinal = posicaoCosta + (direcaoMar.normalized * 35f); 
        posFinal.y = nivelDoMar; 

        SpawnarPredio(prefab, posFinal, rot);
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
                    Vector3 pontoCosta = pontoTeste - (dir * 5f); 
                    return pontoCosta;
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
        Debug.Log($"<color=#00FF00>🏗️ [IA Arquiteto] {prefab.name} ERGUIDO COM SUCESSO na Posição XYZ: {pos}</color>");
    }

    Vector3 EncontrarPosicaoEspiral(Vector3 centro, int indice, float alturaFixa, float espacCustom = -1f)
    {
        float usarEspac = espacCustom > 0f ? espacCustom : espacamentoEdificios;
        float passoCrescimento = espacCustom > 0f ? 15f : (usarEspac * 0.8f);

        float angulo = indice * 137.5f;
        float raio = usarEspac + (indice * passoCrescimento);
        
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * raio, 0, Mathf.Sin(rad) * raio);
        
        return centro + offset;
    }

    bool TemPredioProximo(Vector3 posicao, float raioMinimo)
    {
        Collider[] vizinhos = Physics.OverlapSphere(posicao, raioMinimo);
        foreach (var col in vizinhos)
        {
            if (col == null || col is TerrainCollider) continue;

            if (col.GetComponentInParent<Fabrica>() != null) return true;
            if (col.GetComponentInParent<Estaleiro>() != null) return true;
            if (col.GetComponentInParent<Heliporto>() != null) return true;
            
            var danos = col.GetComponentInParent<SistemaDeDanos>();
            if (danos != null && danos.ehEstrutura) return true;
            
            if (col.GetComponentInParent<ControleUnidade>() != null) return true;
            if (col.GetComponentInParent<IdentidadeUnidade>() != null) return true;

            if (col is BoxCollider && col.bounds.size.magnitude > 3f) return true;
        }
        return false;
    }

    void ConfigurarIdentidade(GameObject obj)
    {
        if (obj == null) return;
        var id = obj.GetComponent<IdentidadeUnidade>();
        if (id == null) id = obj.AddComponent<IdentidadeUnidade>();
        
        if (chefe != null && chefe.identidade != null)
        {
            id.teamID = chefe.identidade.teamID;
        }
        else
        {
            id.teamID = 2; 
        }
        
        var fab = obj.GetComponent<Fabrica>();
        if (fab != null && chefe != null && chefe.cerebroGeneral != null)
        {
            chefe.cerebroGeneral.RegistrarFabrica(fab);
        }
    }

    GameObject BuscarNoCatalogo(string nomeChave)
    {
        GameObject prefabAchado = null;

        // 1. BUSCA NORMAL (Onde estava falhando pois vc não adicionou o item no MenuConstrucao)
        if (MenuConstrucao.catalogoGlobal != null)
        {
            foreach (var item in MenuConstrucao.catalogoGlobal)
            {
                 if (item.nomeItem.ToLower().Contains(nomeChave.ToLower()))
                 {
                     prefabAchado = item.prefabDaUnidade;
                     break;
                 }
            }

            if (prefabAchado == null)
            {
                foreach (var item in MenuConstrucao.catalogoGlobal) 
                {
                    string nm = item.nomeItem.ToLower();
                    if (nomeChave == "Veiculos" && (nm.Contains("hangar") || nm.Contains("fabrica") || nm.Contains("factory") || nm.Contains("veiculos"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Tenda" && (nm.Contains("quartel") || nm.Contains("barraca") || nm.Contains("infantaria") || nm.Contains("tenda"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Antiaerea" && (nm.Contains("anti") || nm.Contains("aérea") || nm.Contains("aerea") || nm.Contains("patriot") || nm.Contains("sam") || nm.Contains("missil") || nm.Contains("ares"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Torreta" && (nm.Contains("torreta") || nm.Contains("defesa") || nm.Contains("bunker") || nm.Contains("canhao") || nm.Contains("metralhadora"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Muro" && (nm.Contains("muro") || nm.Contains("cerca") || nm.Contains("wall") || nm.Contains("barricada"))) prefabAchado = item.prefabDaUnidade;
                    else if (nomeChave == "Aeroporto" && (nm.Contains("aeroporto") || nm.Contains("base aerea") || nm.Contains("pista") || nm.Contains("airport") || nm.Contains("hangar") && (nm.Contains("voo") || nm.Contains("aviao") || nm.Contains("aereo")))) prefabAchado = item.prefabDaUnidade;
                }
            }
        }

        if (prefabAchado != null) return prefabAchado;

        // ==============================================================
        // 🚨 GOD MODE: MODO "TIRA RESTRIÇÕES" ATIVADO 🚨
        // Se a IA não achar no Catálogo Oficial, ela ROUBA direto do Jogo!
        // ==============================================================
        
        Debug.LogWarning($"<color=yellow>⚠️ [IA God Mode] Ficha de '{nomeChave}' não encontrada no catálogo. Ativando clonagem forçada!</color>");

        if (nomeChave == "Aeroporto")
        {
            var aeroNaCena = Object.FindFirstObjectByType<GerenciadorAeroporto>();
            if (aeroNaCena != null) return aeroNaCena.gameObject;
        }

        // Tenta achar qualquer objeto na memória do Unity com o nome parecido
        GameObject[] todosRecursos = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in todosRecursos)
        {
            // Ignora lixo de memória da Unity
            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

            if (obj.name.ToLower().Contains(nomeChave.ToLower()))
            {
                // Verifica se é uma construção válida
                if (obj.GetComponent<SistemaDeDanos>() != null || nomeChave == "Aeroporto" || nomeChave == "Bandeira")
                {
                    Debug.Log($"<color=magenta>🧬 [God Mode] Prefab escondido localizado e roubado: {obj.name}</color>");
                    return obj;
                }
            }
        }

        return null;
    }

    public Vector3 EncontrarPontoDefensivo()
    {
        Vector3 centro = (chefe != null && chefe.basePrincipal != null) ? chefe.basePrincipal.position : transform.position;
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0;
        return centro + (direcaoAleatoria.normalized * 30f);
    }
}