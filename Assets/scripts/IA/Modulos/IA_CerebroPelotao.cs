using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// "CÉREBRO DO PELOTÃO" - Script Avançado de Estratégia Tática
/// Controla um esquadrão específico, organizando agrupamento, formação de grid (Follow Leader),
/// sistema tático de flancos (Waypoints) e regras de espalhamento em combate.
/// </summary>
public class IA_CerebroPelotao : MonoBehaviour
{
    public enum FaseTatica
    {
        PontoDeEncontro,    // Fase 1: Juntando tropas na Caixa Invisível (Staging Area)
        NavegacaoEstrategica, // Fase 2 e 3: Seguindo Líder em Grade via Flancos/Waypoints
        Combate             // Fase 4: Micro-gerenciamento (Espalhamento e Foco de Alvo)
    }

    public FaseTatica faseAtual = FaseTatica.PontoDeEncontro;
    public string nomeDoPelotao = "Alpha";

    [Header("Membros do Pelotão")]
    public List<GameObject> membros = new List<GameObject>();
    public GameObject lider;
    
    [Header("Configurações")]
    public int tamanhoDesejado = 15;
    public Vector3 pontoDeEncontro;
    public Transform alvoInimigoFinal;

    // Offsets calculados
    private Dictionary<GameObject, Vector3> formacaoOffsets = new Dictionary<GameObject, Vector3>();
    private Vector3 waypointDeFlanco;
    private bool esperandoNaEmboscada = false;
    private float espacamentoBase = 6f;

    public int meuTeamID = 2;

    public void Inicializar(Vector3 stagingArea, Transform alvo, int qtdDesejada, string nome, int teamID)
    {
        pontoDeEncontro = stagingArea;
        alvoInimigoFinal = alvo;
        tamanhoDesejado = qtdDesejada;
        nomeDoPelotao = nome;
        meuTeamID = teamID;
        faseAtual = FaseTatica.PontoDeEncontro;
        StartCoroutine(CicloDeDecisao());
    }

    public void AdicionarMembro(GameObject unidade)
    {
        if (!membros.Contains(unidade))
        {
            membros.Add(unidade);
            MandaProPontoDeEncontro(unidade);
        }
    }

    void MandaProPontoDeEncontro(GameObject u)
    {
        if (u == null) return;
        var ctrl = u.GetComponent<ControleUnidade>();
        if (ctrl) ctrl.MoverParaPonto(pontoDeEncontro + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f)));
    }

    IEnumerator CicloDeDecisao()
    {
        while (true)
        {
            membros.RemoveAll(m => m == null || !m.activeInHierarchy);
            if (membros.Count == 0 && faseAtual != FaseTatica.PontoDeEncontro) 
            {
                Destroy(gameObject); // Pelotão aniquilado
                yield break;
            }

            switch (faseAtual)
            {
                case FaseTatica.PontoDeEncontro:
                    LogicaPontoEncontro();
                    break;

                case FaseTatica.NavegacaoEstrategica:
                    LogicaNavegacaoEstrategica();
                    break;

                case FaseTatica.Combate:
                    LogicaDeCombate();
                    break;
            }
            
            // Verifica transição para combate
            VerificarInimigosProximos();

            yield return new WaitForSeconds(1.5f);
        }
    }

    void LogicaPontoEncontro()
    {
        // FASE 1: Espera reunir o limite necessário (Efeito Drop e não Conta-Gotas)
        int naArea = 0;
        foreach (var m in membros)
        {
            if (Vector3.Distance(m.transform.position, pontoDeEncontro) < 30f)
                naArea++;
        }

        if (naArea >= tamanhoDesejado || (membros.Count >= tamanhoDesejado && naArea > tamanhoDesejado * 0.7f))
        {
            // Chegamos ao limite! Pelotão pronto para a marcha.
            ConfigurarFormacao();
            SorteioTaticoDeWaypoints();
            faseAtual = FaseTatica.NavegacaoEstrategica;
            Debug.Log($"[IA Cérebro] Pelotão {nomeDoPelotao} Formado! Iniciando marcha Tática.");
        }
        else
        {
            // Garante que os que estão perdidos tentem voltar pro ponto
            if (Random.value > 0.8f) // Redundância leve
            {
                foreach(var m in membros) MandaProPontoDeEncontro(m);
            }
        }
    }

    void ConfigurarFormacao()
    {
        // FASE 2: Elege o Líder e cria a Grade de Deslocamento (Offsets)
        // Elege líder (preferência por tanque pesado no centro)
        lider = membros.OrderByDescending(m => m.name.Contains("Tank") || m.name.Contains("Tanque") ? 1 : 0).FirstOrDefault();
        if (lider == null) lider = membros[0];

        List<GameObject> infantaria = new List<GameObject>();
        List<GameObject> tanques = new List<GameObject>();

        foreach (var m in membros)
        {
            if (m == lider) continue;
            string n = m.name.ToLower();
            if (n.Contains("tank") || n.Contains("blindado")) tanques.Add(m);
            else infantaria.Add(m);
        }

        // Distribui matematicamente
        formacaoOffsets.Clear();
        formacaoOffsets.Add(lider, Vector3.zero); // Lider no (0,0)

        int indexTanque = 1;
        foreach (var t in tanques)
        {
            // Formação V invertido ou Linha para tanques
            float x = (indexTanque % 2 == 0 ? 1 : -1) * Mathf.Ceil(indexTanque / 2f) * espacamentoBase * 1.5f;
            float z = -Mathf.Ceil(indexTanque / 2f) * espacamentoBase * 0.5f; // Levemente recuados
            formacaoOffsets.Add(t, new Vector3(x, 0, z));
            indexTanque++;
        }

        int indexInf = 1;
        foreach (var inf in infantaria)
        {
            // Infantaria vai mais recuada atrás dos tanques
            float x = (indexInf % 2 == 0 ? 1 : -1) * Mathf.Ceil(indexInf / 2f) * (espacamentoBase * 0.6f);
            float z = -espacamentoBase * 2f - (Mathf.Ceil(indexInf / 4f) * espacamentoBase); // Mais atrás
            formacaoOffsets.Add(inf, new Vector3(x, 0, z));
            indexInf++;
        }
    }

    void SorteioTaticoDeWaypoints()
    {
        // FASE 3: Cria um "Waypoint" de flanco em vez de andar em linha reta direta.
        if (alvoInimigoFinal == null) return;
        
        Vector3 dirProAlvo = (alvoInimigoFinal.position - lider.transform.position).normalized;
        Vector3 rightDir = Vector3.Cross(Vector3.up, dirProAlvo);
        float distanciaProAlvo = Vector3.Distance(lider.transform.position, alvoInimigoFinal.position);

        int roleta = Random.Range(1, 4); // 1 = Esquerda, 2 = Centro/Emboscada, 3 = Direita
        
        if (roleta == 1)
        {
            waypointDeFlanco = lider.transform.position + (dirProAlvo * distanciaProAlvo * 0.6f) - (rightDir * 80f);
        }
        else if (roleta == 3)
        {
            waypointDeFlanco = lider.transform.position + (dirProAlvo * distanciaProAlvo * 0.6f) + (rightDir * 80f);
        }
        else
        {
            // Centro com Emboscada (Espera um pouco antes de atacar)
            waypointDeFlanco = lider.transform.position + (dirProAlvo * distanciaProAlvo * 0.5f);
            esperandoNaEmboscada = true;
            StartCoroutine(EmboscadaTimer());
        }
    }

    IEnumerator EmboscadaTimer()
    {
        yield return new WaitForSeconds(15f); // Pausa táctica para juntar tropas ou atacar junto c/ outro Pelotão
        esperandoNaEmboscada = false;
        waypointDeFlanco = Vector3.zero; // Limpa waypoint, vai direto pro alvo agora
    }

    void LogicaNavegacaoEstrategica()
    {
        if (lider == null) { ConfigurarFormacao(); return; } // Perdeu o líder
        if (alvoInimigoFinal == null) return;

        Vector3 destinoMovel = alvoInimigoFinal.position;
        
        // Verifica se tá usando o Waypoint Invisível ou indo pro Alvo Final
        if (waypointDeFlanco != Vector3.zero)
        {
            if (Vector3.Distance(lider.transform.position, waypointDeFlanco) < 30f)
            {
                // Chegou no Flanco! Agrupando e limpando o waypoint para ir pra base Inimiga.
                if (!esperandoNaEmboscada) waypointDeFlanco = Vector3.zero;
            }
            else
            {
                destinoMovel = waypointDeFlanco; // Vai pro Flanco
            }
        }

        if (esperandoNaEmboscada && waypointDeFlanco != Vector3.zero)
        {
            destinoMovel = lider.transform.position; // Fica parado aguardando
        }

        // Executa o Grid de Movimentação (Segue o Líder)
        Vector3 direcaoDoGrupo = (destinoMovel - lider.transform.position).normalized;
        if (direcaoDoGrupo == Vector3.zero) direcaoDoGrupo = lider.transform.forward;
        Quaternion rotDoGrupo = Quaternion.LookRotation(direcaoDoGrupo);

        foreach (var m in membros)
        {
            if (formacaoOffsets.TryGetValue(m, out Vector3 offset))
            {
                Vector3 posicaoNaGrade = lider.transform.position + (rotDoGrupo * offset);
                
                // Manda só quem tá fora de posição andar
                if (Vector3.Distance(m.transform.position, posicaoNaGrade) > 4f)
                {
                    var ctrl = m.GetComponent<ControleUnidade>();
                    if (ctrl) ctrl.MoverParaPonto(posicaoNaGrade);
                }
            }
        }

        // Lider avança
        if (!esperandoNaEmboscada)
        {
            var liderCtrl = lider.GetComponent<ControleUnidade>();
            if (liderCtrl) liderCtrl.MoverParaPonto(destinoMovel);
        }
    }

    void VerificarInimigosProximos()
    {
        if (faseAtual == FaseTatica.Combate || lider == null) return;
        
        // Se alguma unidade ver o jogador de perto, quebra a formação e vai pro combate!
        Collider[] hits = Physics.OverlapSphere(lider.transform.position, 60f); // Raio de visão
        bool viuInimigo = false;
        int meuTime = meuTeamID;

        foreach (var h in hits)
        {
            IdentidadeUnidade id = h.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.teamID != meuTime && id.teamID != 0) 
            {
                viuInimigo = true;
                break;
            }
        }

        if (viuInimigo)
        {
            faseAtual = FaseTatica.Combate;
            Debug.Log($"[IA Cérebro] Pelotão {nomeDoPelotao} avistou inimigos! Quebrando a formação e ativando Protocolo Militar.");
            AplicarEspalhamentoMicro();
        }
    }

    void LogicaDeCombate()
    {
        // FASE 4: Micro-gerenciamento Ativo (Modificando alvos nativos das tropas)
        // Quando entra no combate, a formação rígida desliga e os soldados focam pela Regra de Engajamento

        Collider[] alvosPerifericos = Physics.OverlapSphere(lider != null ? lider.transform.position : transform.position, 100f);
        int meuTime = meuTeamID;

        List<Transform> inimigosBlindados = new List<Transform>();
        List<Transform> inimigosLeves = new List<Transform>();

        foreach (var h in alvosPerifericos)
        {
            IdentidadeUnidade id = h.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.teamID != meuTime && id.teamID != 0)
            {
                string tagInimigo = h.transform.root.name.ToLower();
                if (tagInimigo.Contains("tank") || tagInimigo.Contains("torreta") || tagInimigo.Contains("defesa"))
                    inimigosBlindados.Add(h.transform.root);
                else
                    inimigosLeves.Add(h.transform.root);
            }
        }

        // Distribui Alvos por Tipo
        foreach (var m in membros)
        {
            SistemaDeTiro arma = m.GetComponentInChildren<SistemaDeTiro>();
            if (arma != null)
            {
                string meuNome = m.name.ToLower();
                if (meuNome.Contains("tank") || meuNome.Contains("blindado"))
                {
                    // Tanque prefere atirar em Torres e outros Tanques
                    if (inimigosBlindados.Count > 0)
                    {
                        var maisPerto = inimigosBlindados.OrderBy(x => Vector3.Distance(m.transform.position, x.position)).First();
                        // Força o alvo da unidade sem alterar seu script de tiro internamente:
                        arma.alvoAtual = maisPerto; 
                    }
                }
                else
                {
                    // Soldado foca na infantaria para limpar rápido
                    if (inimigosLeves.Count > 0)
                    {
                        var maisPerto = inimigosLeves.OrderBy(x => Vector3.Distance(m.transform.position, x.position)).First();
                        arma.alvoAtual = maisPerto;
                    }
                }
            }
        }

        // Se todo o inimigo morreu num raio de 100m, volta a andar
        if (inimigosBlindados.Count == 0 && inimigosLeves.Count == 0 && alvoInimigoFinal != null)
        {
             faseAtual = FaseTatica.NavegacaoEstrategica;
             ConfigurarFormacao(); // Tenta re-agrupar e recomeça a marcha
        }
    }

    void AplicarEspalhamentoMicro()
    {
        // Fase de Combate Inicial (Scatter)
        // Faz eles darem um passinho de 2~4 metros para os lados, saindo do bolo para não tomarem dano em área de morteiro/tanque.
        foreach (var m in membros)
        {
            if (m == null) continue;
            Vector3 passinhoLateral = new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            var ctrl = m.GetComponent<ControleUnidade>();
            if (ctrl) ctrl.MoverParaPonto(m.transform.position + passinhoLateral);
        }
    }
}
