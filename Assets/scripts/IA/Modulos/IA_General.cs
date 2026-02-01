using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class IA_General : MonoBehaviour
{
    private IA_Comandante chefe;

    public enum EstadoGuerra { Reagrupando, AtaqueTotal, DefesaCritica }
    public EstadoGuerra estadoAtual = EstadoGuerra.Reagrupando;

    [Header("Doutrina de Armas Combinadas (Adaptativa)")]
    public int minimoTanques = 1;
    public int minimoHelicopteros = 1;
    public int minimoArtilharia = 0;
    
    [Header("Configuração de Ataque")]
    public Transform pontoDeEncontro;
    public Transform alvoPrioritario;
    
    // Categorização do exército
    private List<GameObject> tanquesDisponiveis = new List<GameObject>();
    private List<GameObject> helicopterosDisponiveis = new List<GameObject>();
    private List<GameObject> artilhariaDisponivel = new List<GameObject>();
    private List<GameObject> naviosDisponiveis = new List<GameObject>();
    private List<GameObject> outrasUnidades = new List<GameObject>();
    
    private List<Fabrica> minhasFabricas = new List<Fabrica>();
    
    // Timer para recrutamento
    private float ultimoRecrutamento = 0f;
    public float intervaloRecrutamento = 3.0f;

    // Timer para construção de defesas
    private float ultimoCheckDefesa = 0f;

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    public void RegistrarSoldado(GameObject unidade)
    {
        if (unidade == null) return;
        
        // Categoriza a unidade baseado nos componentes
        string nomeLower = unidade.name.ToLower();
        
        if (nomeLower.Contains("tank") || nomeLower.Contains("leopard") || nomeLower.Contains("arthur"))
        {
            if (!tanquesDisponiveis.Contains(unidade)) tanquesDisponiveis.Add(unidade);
        }
        else if (unidade.GetComponent<Helicoptero>() != null)
        {
            if (!helicopterosDisponiveis.Contains(unidade)) helicopterosDisponiveis.Add(unidade);
        }
        else if (nomeLower.Contains("lancador") || nomeLower.Contains("missile") || nomeLower.Contains("artillery"))
        {
            if (!artilhariaDisponivel.Contains(unidade)) artilhariaDisponivel.Add(unidade);
        }
        else if (nomeLower.Contains("ship") || nomeLower.Contains("navio") || nomeLower.Contains("corveta"))
        {
            if (!naviosDisponiveis.Contains(unidade)) naviosDisponiveis.Add(unidade);
        }
        else
        {
            if (!outrasUnidades.Contains(unidade)) outrasUnidades.Add(unidade);
        }
    }

    public void RegistrarFabrica(Fabrica fab)
    {
        if (fab != null && !minhasFabricas.Contains(fab))
        {
            minhasFabricas.Add(fab);
        }
    }

    public void ProcessarEstrategia()
    {
        LimparMortos();

        // 🛡️ 1. Sistema de Alarme de Base (Prioridade Máxima!)
        if (VerificarBaseSobAtaque())
        {
            if (estadoAtual != EstadoGuerra.DefesaCritica)
            {
                Debug.LogWarning("🚨 [IA General] ALARME! BASE SOB ATAQUE! Todas as unidades retornando!");
                estadoAtual = EstadoGuerra.DefesaCritica;
            }
        }
        else if (estadoAtual == EstadoGuerra.DefesaCritica)
        {
            // Se parou o ataque, volta ao normal
            Debug.Log("✅ [IA General] Ameaça à base neutralizada. Reagrupando.");
            estadoAtual = EstadoGuerra.Reagrupando;
        }

        // 🏗️ 2. Construção Automática de Defesas
        ConstruirDefesasAutomaticas();

        // 🧠 3. Adaptação ao Inimigo
        AdaptarEstrategia();

        // 4. Recrutamento
        TentarRecrutarArmasCombinadas();

        // 5. Máquina de Estados de Combate
        switch (estadoAtual)
        {
            case EstadoGuerra.DefesaCritica:
                DefenderBaseTotal();
                break;

            case EstadoGuerra.Reagrupando:
                int totalUnidades = TotalDeUnidades();
                
                if (totalUnidades >= 3 || ForcaDeTarefaCompleta())
                {
                    alvoPrioritario = BuscarAlvoEstrategico();
                    if (alvoPrioritario != null)
                    {
                        Debug.Log($"⚔️ [IA General] Iniciando Ataque Coordenado contra {alvoPrioritario.name}");
                        estadoAtual = EstadoGuerra.AtaqueTotal;
                        LancarAtaqueFlanqueado(alvoPrioritario); // Novo ataque inteligente
                    }
                }
                else
                {
                    ManterPosicaoDefensiva();
                }
                break;

            case EstadoGuerra.AtaqueTotal:
                if (TotalDeUnidades() < 1)
                {
                    estadoAtual = EstadoGuerra.Reagrupando;
                }
                else
                {
                    if (alvoPrioritario == null)
                    {
                        alvoPrioritario = BuscarAlvoEstrategico();
                        if (alvoPrioritario != null) LancarAtaqueFlanqueado(alvoPrioritario);
                    }
                }
                break;
        }
    }


    bool VerificarBaseSobAtaque()
    {
        // Verifica se tem inimigos perto do Centro da Base
        if (chefe.basePrincipal != null || chefe.transform != null)
        {
            Vector3 centro = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
            Collider[] hits = Physics.OverlapSphere(centro, 40f); // Raio de 40m
            
            foreach (var hit in hits)
            {
                var id = hit.GetComponentInParent<IdentidadeUnidade>();
                if (id != null && id.teamID == 1) // Inimigo (Jogador)
                {
                    return true;
                }
            }
        }
        return false;
    }

    void DefenderBaseTotal()
    {
        Vector3 centroDefesa = (chefe.basePrincipal != null) ? chefe.basePrincipal.position : chefe.transform.position;
        
        // Manda TODO MUNDO pra casa
        MoverGrupoPara(tanquesDisponiveis, centroDefesa);
        MoverGrupoPara(helicopterosDisponiveis, centroDefesa);
        MoverGrupoPara(artilhariaDisponivel, centroDefesa);
        MoverGrupoPara(outrasUnidades, centroDefesa);
    }

    void ConstruirDefesasAutomaticas()
    {
        if (Time.time - ultimoCheckDefesa < 10.0f) return; // Checa a cada 10s
        ultimoCheckDefesa = Time.time;

        if (chefe.dinheiro < 600) return; // Precisa de grana extra

        // Pede pro Arquiteto achar um lugar
        if (chefe.cerebroArquiteto != null)
        {
            Vector3 local = chefe.cerebroArquiteto.EncontrarPontoDefensivo();
            
            // Busca torreta no catálogo
            var itemTorreta = MenuConstrucao.catalogoGlobal.Find(x => 
                x.nomeItem.ToLower().Contains("torreta") || 
                x.nomeItem.ToLower().Contains("turret"));
            
            if (itemTorreta != null && chefe.GastarDinheiro(itemTorreta.preco))
            {
                // Constrói
                Construtor construtor = FindFirstObjectByType<Construtor>();
                if (construtor != null)
                {
                    GameObject predio = construtor.ConstruirEstruturaIA(itemTorreta.prefabDaUnidade, local, Quaternion.identity);
                    
                    // Configura identidade
                    if (predio != null)
                    {
                        var id = predio.GetComponent<IdentidadeUnidade>();
                        if (id == null) id = predio.AddComponent<IdentidadeUnidade>();
                        id.teamID = 2; // AI
                    }
                    
                    Debug.Log($"🧱 [IA General] Construindo Defesa em {local}");
                }
            }
        }
    }

    void AdaptarEstrategia()
    {
        if (Time.frameCount % 300 != 0) return; // A cada 5-10s

        // Espiona inimigo
        var inimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None).Where(u => u.teamID == 1).ToList();
        
        int tanquesInimigos = inimigos.Count(u => u.name.ToLower().Contains("tank") || u.name.ToLower().Contains("leopard"));
        int infantariaInimiga = inimigos.Count(u => !u.name.ToLower().Contains("tank") && !u.name.ToLower().Contains("heli"));
        
        // Contramedidas
        if (tanquesInimigos > 2)
        {
            // Se ele tem tank, faço heli
            if (minimoHelicopteros < 3)
            {
                minimoHelicopteros++;
                Debug.Log("🧠 [IA Adaptativa] Detectei Tanques! Aumentando produção de Helicópteros.");
            }
        }
        
        if (infantariaInimiga > 5)
        {
            // Se ele tem infantaria, faço tanque (esmaga)
            if (minimoTanques < 3)
            {
                minimoTanques++;
                Debug.Log("🧠 [IA Adaptativa] Muita infantaria! Aumentando Tanques.");
            }
        }
    }

    // --- Versão melhorada do Ataque ---
    void LancarAtaqueFlanqueado(Transform alvo)
    {
        if (alvo == null) return;
        
        // Grupo Principal (Frente)
        Vector3 direcao = (alvo.position - chefe.transform.position).normalized;
        Vector3 flancoDireito = Quaternion.Euler(0, 45, 0) * direcao;
        Vector3 flancoEsquerdo = Quaternion.Euler(0, -45, 0) * direcao;

        // Vanguarda (Tanques) - Vai de frente
        foreach(var t in tanquesDisponiveis) MoverUnidadeParaAlvo(t, alvo.position);
        
        // Flanco (Helicópteros/Infantaria) - Tenta cercar
        for (int i=0; i < helicopterosDisponiveis.Count; i++)
        {
            Vector3 pos = alvo.position + (flancoDireito * 30f);
            MoverUnidadeParaAlvo(helicopterosDisponiveis[i], pos);
        }
        
        for (int i=0; i < outrasUnidades.Count; i++)
        {
            Vector3 pos = alvo.position + (flancoEsquerdo * 20f);
            MoverUnidadeParaAlvo(outrasUnidades[i], pos);
        }
        
        // Artilharia de longe
        foreach(var a in artilhariaDisponivel) 
            MoverUnidadeParaAlvo(a, alvo.position - (direcao * 60f));
    }

    Transform BuscarAlvoEstrategico()
    {
        var inimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        Transform melhorAlvo = null;
        float melhorScore = -1000f;

        foreach (var ini in inimigos)
        {
            if (ini.teamID == 1) // Jogador
            {
                float score = 0f;
                string nome = ini.name.ToLower();

                // 1. Tipo do Alvo
                if (nome.Contains("refinaria") || nome.Contains("gerador")) score += 100f; // Economia
                else if (nome.Contains("torreta")) score += 60f; // Defesa
                else if (nome.Contains("tank")) score += 50f; // Ameaça
                else score += 10f; // Infantaria

                // 2. Proximidade (não atacar do outro lado do mundo se tem um aqui)
                float dist = Vector3.Distance(chefe.transform.position, ini.transform.position);
                score -= dist * 0.1f;

                // 3. Foco em Feridos (Target Priority)
                var danos = ini.GetComponent<SistemaDeDanos>();
                if (danos != null)
                {
                    float porcentagemVida = danos.vidaAtual / danos.vidaMaxima;
                    if (porcentagemVida < 0.3f) score += 200f; // MATAR AGORA!
                    else if (porcentagemVida < 0.6f) score += 50f;
                }

                if (score > melhorScore)
                {
                    melhorScore = score;
                    melhorAlvo = ini.transform;
                }
            }
        }
        return melhorAlvo;
    }

    // --- Métodos Auxiliares ---
    void LimparMortos()
    {
        tanquesDisponiveis.RemoveAll(u => u == null);
        helicopterosDisponiveis.RemoveAll(u => u == null);
        artilhariaDisponivel.RemoveAll(u => u == null);
        naviosDisponiveis.RemoveAll(u => u == null);
        outrasUnidades.RemoveAll(u => u == null);
    }
    
    int TotalDeUnidades() => tanquesDisponiveis.Count + helicopterosDisponiveis.Count + artilhariaDisponivel.Count + outrasUnidades.Count;

    bool ForcaDeTarefaCompleta()
    {
        return tanquesDisponiveis.Count >= minimoTanques &&
               helicopterosDisponiveis.Count >= minimoHelicopteros &&
               artilhariaDisponivel.Count >= minimoArtilharia;
    }

    void TentarRecrutarArmasCombinadas()
    {
        if (Time.time - ultimoRecrutamento < intervaloRecrutamento) return;
        if (chefe.dinheiro < 300) return;
        
        var catalogo = MenuConstrucao.catalogoGlobal;
        if (catalogo == null) return;

        // Prioridade 1: TANQUES
        if (tanquesDisponiveis.Count < minimoTanques + 2)
        {
            var tanques = catalogo.FindAll(i =>
                (i.nomeItem.ToLower().Contains("tank") || i.nomeItem.ToLower().Contains("leopard") || i.nomeItem.ToLower().Contains("arthur")) &&
                (i.categoria == DadosConstrucao.CategoriaItem.Exercito));
            
            if (tanques.Count > 0 && ProdubirUnidade(tanques[Random.Range(0, tanques.Count)])) { ultimoRecrutamento = Time.time; return; }
        }
        
        // Prioridade 2: HELIS
        if (helicopterosDisponiveis.Count < minimoHelicopteros + 1 && chefe.dinheiro > 500)
        {
            var helis = catalogo.FindAll(i => i.nomeItem.ToLower().Contains("heli"));
            if (helis.Count > 0 && ProdubirUnidade(helis[Random.Range(0, helis.Count)])) { ultimoRecrutamento = Time.time; return; }
        }
        
        // Prioridade 3: INFANTARIA
        if (outrasUnidades.Count < 5 && chefe.dinheiro > 200)
        {
            var inf = catalogo.FindAll(i => i.categoria == DadosConstrucao.CategoriaItem.Exercito && !i.nomeItem.ToLower().Contains("tank"));
            if (inf.Count > 0 && ProdubirUnidade(inf[Random.Range(0, inf.Count)])) { ultimoRecrutamento = Time.time; return; }
        }
    }

    bool ProdubirUnidade(DadosConstrucao dados)
    {
        Fabrica fabricaEscolhida = null;
        bool precisaQuartel = dados.categoria == DadosConstrucao.CategoriaItem.Exercito;

        foreach (var fab in minhasFabricas)
        {
            if (fab == null) continue;
            if (precisaQuartel && fab.ehQuartel) fabricaEscolhida = fab;
            else if (!precisaQuartel && !fab.ehQuartel) fabricaEscolhida = fab;
            if (fabricaEscolhida != null) break;
        }

        if (fabricaEscolhida != null && chefe.GastarDinheiro(dados.preco))
        {
            GameObject nova = fabricaEscolhida.ProduzirUnidade(dados.prefabDaUnidade);
            if (nova != null)
            {
                RegistrarSoldado(nova);
                EnviarUnidadeRecemRecrutada(nova);
                Debug.Log($"[IA General] Recrutou {dados.nomeItem}");
                return true;
            }
        }
        return false;
    }

    void EnviarUnidadeRecemRecrutada(GameObject unidade)
    {
        if (unidade == null) return;
        Vector3 destino;
        
        if (estadoAtual == EstadoGuerra.AtaqueTotal && alvoPrioritario != null) destino = alvoPrioritario.position;
        else if (estadoAtual == EstadoGuerra.DefesaCritica) destino = chefe.transform.position;
        else
        {
            if (pontoDeEncontro == null) pontoDeEncontro = chefe.transform;
            destino = pontoDeEncontro.position + (Vector3.forward * 20f);
        }
        MoverUnidadeParaAlvo(unidade, destino);
    }
    
    void MoverUnidadeParaAlvo(GameObject unidade, Vector3 destino)
    {
        if (unidade == null) return;
        var controle = unidade.GetComponent<ControleUnidade>();
        if (controle != null) { controle.MoverParaPonto(destino); return; }
        var nav = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.isOnNavMesh) { nav.SetDestination(destino); nav.isStopped = false; }
    }
    
    void MoverGrupoPara(List<GameObject> grupo, Vector3 destino)
    {
        foreach(var u in grupo) MoverUnidadeParaAlvo(u, destino);
    }

    void ManterPosicaoDefensiva()
    {
        if (pontoDeEncontro == null) pontoDeEncontro = chefe.transform;
        
        var todos = new List<GameObject>();
        todos.AddRange(tanquesDisponiveis);
        todos.AddRange(helicopterosDisponiveis); 
        todos.AddRange(outrasUnidades);

        // Formação simples em grid
        int cols = 5;
        for(int i=0; i<todos.Count; i++)
        {
            int r = i/cols; int c = i%cols;
            Vector3 pos = pontoDeEncontro.position + new Vector3((c-2)*8, 0, r*8 + 15);
            MoverUnidadeParaAlvo(todos[i], pos);
        }
    }
}
