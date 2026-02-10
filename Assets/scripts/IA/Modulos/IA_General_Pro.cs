using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia táticas de Armas Combinadas.
/// Recruta unidades e coordena ataques.
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição do Batalhão")]
    public int tanquesNecessarios = 2; // AI tentará manter isso
    public int soldadosNecessarios = 4; // Reduzi para 4 para facilitar o start
    public int transportesNecessarios = 1;

    // Listas de Controle
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTransporte = new List<GameObject>();

    // Fábricas registradas pelo Arquiteto
    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();

    private float _timerRecrutamento;
    private string _ultimoStatus = "Iniciando..."; // Para Debug na tela

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Update()
    {
        if (chefe == null) return;

        // 1. Processamento Tático (Lento)
        if (Time.frameCount % 60 == 0) 
        {
            VerificarProntidaoParaCombate();
        }

        // 2. Recrutamento (A cada 2 segundos - mais rápido)
        _timerRecrutamento += Time.deltaTime;
        if (_timerRecrutamento >= 2.0f)
        {
            _timerRecrutamento = 0;
            TentarRecrutar();
        }
    }

    void VerificarProntidaoParaCombate()
    {
        LimparMortos();

        bool temForcaSuficiente = grupoTanques.Count >= tanquesNecessarios && 
                                  grupoSoldados.Count >= soldadosNecessarios &&
                                  grupoTransporte.Count >= transportesNecessarios;

        if (temForcaSuficiente)
        {
            _ultimoStatus = "⚔️ ATACANDO! Batalhão Pronto.";
            Transform alvo = BuscarAlvo();
            if (alvo != null)
            {
                LancarAtaqueCoordenado(alvo.position);
            }
            else
            {
                _ultimoStatus = "ATACANDO! (Procurando inimigos...)";
            }
        }
        else
        {
            _ultimoStatus = $"🛡️ REAGRUPANDO (Recrutando reforços)";
            ReagruparNoPontoDeEncontro();
        }
    }

    // --- RECRUTAMENTO ---
    void TentarRecrutar()
    {
        if (chefe.dinheiro < 100) 
        {
            _ultimoStatus = "Sem dinheiro para recrutar!";
            return; 
        }

        // Prioridade 1: Transporte (Essencial para IA Pro)
        // Busca "Houver" pois descobrimos que é este o nome no projeto
        if (grupoTransporte.Count < transportesNecessarios)
        {
            // Se comprar com sucesso, retorna para esperar nex frame. 
            // Se falhar (ex: sem fábrica), continua para tentar outras coisas.
            if (ComprarUnidade("Houver", "Hover", "Transporte", false)) return;
            
            _ultimoStatus = "Falha ao comprar Transporte (Tentando outros...)";
        }

        // Prioridade 2: Tanques
        if (grupoTanques.Count < tanquesNecessarios)
        {
             if (ComprarUnidade("Tanque", "Battle", "Tank", false)) return;
        }

        // Prioridade 3: Soldados
        if (grupoSoldados.Count < soldadosNecessarios)
        {
             if (ComprarUnidade("Soldado", "Rifle", "Infantaria", true)) return; // true = requer quartel
        }
    }

    bool ComprarUnidade(string k1, string k2, string k3, bool requerQuartel)
    {
        // Debug.Log($"[IA DEBUG] Tentando comprar unidade: {k1}/{k2}/{k3} (Requer Quartel: {requerQuartel})");

        // 1. Achar Fábrica compatível
        Fabrica fabrica = minhasFabricas.FirstOrDefault(f => f != null && f.ehQuartel == requerQuartel);
        if (fabrica == null) 
        {
            // Debug.LogWarning("[IA DEBUG] Nenhuma fábrica registrada encontrada na lista interna. Tentando busca global...");
            // Tenta achar qualquer fábrica na cena que seja da IA (Fallback)
            var todasFabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
            foreach(var f in todasFabricas)
            {
                 var id = f.GetComponentInParent<IdentidadeUnidade>();
                 if (id != null && id.teamID == 2 && f.ehQuartel == requerQuartel)
                 {
                     fabrica = f;
                     RegistrarFabrica(f); // Registra para a próxima
                     // Debug.Log($"[IA DEBUG] Fábrica encontrada via busca global: {f.name}");
                     break;
                 }
            }
            
            if (fabrica == null)
            {
                // Apenas avisa, não é erro critico (pode não ter construido ainda)
                _ultimoStatus = requerQuartel ? "Sem Quartel!" : "Sem Fábrica/Aeroporto!";
                // Debug.LogWarning($"[IA INFO] Não encontrei nenhuma fábrica válida para (Quartel={requerQuartel}). Ignorando pedido.");
                return false;
            }
        }

        // 2. Achar Prefab no Catálogo
        if (MenuConstrucao.catalogoGlobal == null) 
        {
            Debug.LogError("[IA DEBUG] O Catálogo Global está NULO!");
            return false;
        }
        
        Debug.Log($"[IA DEBUG] Pesquisando no catálogo com {MenuConstrucao.catalogoGlobal.Count} itens...");

        var ficha = MenuConstrucao.catalogoGlobal.FirstOrDefault(item =>  
            (item.nomeItem.ToLower().Contains(k1.ToLower()) || item.nomeItem.ToLower().Contains(k2.ToLower()) || item.nomeItem.ToLower().Contains(k3.ToLower())) &&
            item.prefabDaUnidade != null &&
            item.preco <= chefe.dinheiro
        );

        // Debug se não achar
        if (ficha == null)
        {
            Debug.LogWarning($"[IA DEBUG] Não encontrei item compatível com '{k1}'/'{k2}'/'{k3}' por menos de ${chefe.dinheiro}. Tentando fallback...");
            // Tenta pegar QUALQUER UM da categoria se for soldado
            if (k1.Contains("Soldado"))
            {
                ficha = MenuConstrucao.catalogoGlobal.FirstOrDefault(item => 
                    item.categoria == DadosConstrucao.CategoriaItem.Exercito && item.preco < 200 && item.prefabDaUnidade != null);
            }
        }

        if (ficha == null)
        {
             Debug.LogError($"[IA DEBUG] DESISTI: Realmente não achei nada no catálogo para '{k1}'. Verifique os nomes nos assets!");
             return false;
        }

        // 3. Produzir
        Debug.Log($"[IA DEBUG] Item escolhido: {ficha.nomeItem} (${ficha.preco}). Tentando produzir na fábrica {fabrica.name}...");

        if (chefe.GastarDinheiro(ficha.preco))
        {
            GameObject novaUnidade = fabrica.ProduzirUnidade(ficha.prefabDaUnidade);
            if (novaUnidade != null)
            {
                RegistrarSoldado(novaUnidade); 
                
                // Vai para o ponto de encontro
                Vector3 pontoReagrupamento = chefe.transform.position + new Vector3(0,0,15);
                var mov = novaUnidade.GetComponent<ControleUnidade>();
                if (mov) mov.MoverParaPonto(pontoReagrupamento);
                
                Debug.Log($"[IA General] SUCESSO: Comprou {ficha.nomeItem}");
                return true;
            }
            else
            {
                Debug.LogError("[IA DEBUG] A Fábrica falhou em instanciar o objeto (retornou null).");
            }
        }
        else
        {
            _ultimoStatus = "Dinheiro insuficiente...";
            Debug.Log($"[IA DEBUG] Falta de fundos: Tenho ${chefe.dinheiro}, preciso de ${ficha.preco}");
        }
        return false;
    }

    // --- COMANDO ---
    void LancarAtaqueCoordenado(Vector3 destino)
    {
        // Tanques na Frente
        MoverGrupo(grupoTanques, destino);

        // Transportes Recuados
        if (chefe != null)
        {
            Vector3 dir = (chefe.transform.position - destino).normalized;
            if (dir == Vector3.zero) dir = Vector3.back;
            MoverGrupo(grupoTransporte, destino + dir * 15f);
        }

        // Soldados (Se sobrarem a pé) flanqueiam
        MoverGrupo(grupoSoldados, destino + new Vector3(8, 0, 8));

        // ORDEM DE DESEMBARQUE: Se chegarem perto, desembarcam
        foreach(var t in grupoTransporte)
        {
            if (Vector3.Distance(t.transform.position, destino) < 30f)
            {
                t.GetComponent<TransporteTerrestre>()?.DesembarcarTudo();
            }
        }
    }

    public void RegistrarSoldado(GameObject unidade)
    {
        if (unidade == null) return;
        string n = unidade.name.ToLower();
        
        if (grupoTanques.Contains(unidade) || grupoSoldados.Contains(unidade) || grupoTransporte.Contains(unidade)) return;

        if (n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado")) grupoTanques.Add(unidade);
        else if (n.Contains("truck") || n.Contains("transp") || n.Contains("houver")) grupoTransporte.Add(unidade);
        else grupoSoldados.Add(unidade); 
    }

    public void RegistrarFabrica(Fabrica fab)
    {
        if (fab != null && !minhasFabricas.Contains(fab))
        {
            minhasFabricas.Add(fab);
        }
    }

    void MoverGrupo(List<GameObject> grupo, Vector3 destino)
    {
        foreach (var u in grupo)
        {
            if (u == null) continue;
            
            // Infantaria tenta pegar carona (DESATIVADO A PEDIDO DO USUÁRIO)
            /*
            bool ehInfantaria = grupoSoldados.Contains(u);
            if (ehInfantaria && Vector3.Distance(u.transform.position, destino) > 50f)
            {
                 var transporteLivre = grupoTransporte.FirstOrDefault(t => !t.GetComponent<TransporteTerrestre>().EstaCheio());
                 if (transporteLivre != null)
                 {
                     u.GetComponent<ControleUnidade>()?.MoverParaPonto(transporteLivre.transform.position);
                     continue; 
                 }
            }
            */

            var controle = u.GetComponent<ControleUnidade>();
            if (controle != null) controle.MoverParaPonto(destino);
        }
    }

    void ReagruparNoPontoDeEncontro()
    {
        if (chefe == null) return;
        Vector3 ponto = chefe.transform.position + new Vector3(0, 0, 20);
        
        for(int i=0; i<grupoTanques.Count; i++) 
            if(grupoTanques[i]) Enviar(grupoTanques[i], ponto + new Vector3(i*6 - 10, 0, 10));

        for(int i=0; i<grupoTransporte.Count; i++) 
            if(grupoTransporte[i]) Enviar(grupoTransporte[i], ponto + new Vector3(i*8, 0, 0));

        for(int i=0; i<grupoSoldados.Count; i++) 
        {
            if(!grupoSoldados[i]) continue;
            // Soldados vão DIRETO para perto dos caminhões para facilitar embarque
            if (grupoTransporte.Count > 0)
                Enviar(grupoSoldados[i], grupoTransporte[i % grupoTransporte.Count].transform.position + Vector3.back * 3f);
            else
                Enviar(grupoSoldados[i], ponto + new Vector3(i*2 - 5, 0, -10));
        }
    }

    void Enviar(GameObject u, Vector3 pos)
    {
        u.GetComponent<ControleUnidade>()?.MoverParaPonto(pos);
    }

    void LimparMortos()
    {
        grupoTanques.RemoveAll(u => u == null);
        grupoSoldados.RemoveAll(u => u == null);
        grupoTransporte.RemoveAll(u => u == null);
        minhasFabricas.RemoveAll(f => f == null);
    }

    Transform BuscarAlvo()
    {
        var inimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        return inimigos.FirstOrDefault(i => i.teamID == 1)?.transform; // Ataca jogador (Time 1)
    }

    // --- DEBUG VISUAL NA TELA ---
    void OnGUI()
    {
        if (chefe == null) return;

        // Desenha painel no canto superior esquerdo
        GUI.Box(new Rect(10, 10, 300, 160), "CÉREBRO DA IA");

        string status = $"Estado: {_ultimoStatus}\n" +
                        $"Dinheiro: ${chefe.dinheiro:F0}\n" +
                        $"--------------------------\n" +
                        $"Fábricas Conhecidas: {minhasFabricas.Count}\n" +
                        $"--------------------------\n" +
                        $"Tanques: {grupoTanques.Count} / {tanquesNecessarios}\n" +
                        $"Transportes: {grupoTransporte.Count} / {transportesNecessarios}\n" +
                        $"Soldados: {grupoSoldados.Count} / {soldadosNecessarios}";

        GUI.Label(new Rect(20, 35, 280, 140), status);
    }
}
