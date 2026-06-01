using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Cérebro do Avião Bombardeiro. Pode ser editado no Inspector para uso geral em diversos bombardeiros.
/// Permite voar alto e soltar bombas/mísseis com precisão em três formas de ataque.
/// </summary>
[RequireComponent(typeof(ControleAviao))]
[RequireComponent(typeof(SistemaDeDanos))]
public class AviaoBombardeiro : MonoBehaviour
{
    public enum ModoAtaque { AtaqueAoSolo, Patrulha, AtaqueEmMassa }

    [Header("=== GERAL DO BOMBARDEIRO ===")]
    [Tooltip("Modo de ataque do Bombardeiro")]
    public ModoAtaque modoDeAtaque = ModoAtaque.AtaqueAoSolo;
    
    [Tooltip("Quão alto ele deve voar para não ser pego facilmente")]
    public float altitudeDeVoo = 150f;
    
    [Tooltip("Prefab do Projétil (Míssil ou Bomba) a ser lançado.")]
    public GameObject projetilPrefab;

    [Tooltip("De onde as bombas vão sair. (Deixe vazio para o script buscar automaticamente)")]
    public Transform[] comportasDeBomba;
    
    [Tooltip("Tempo em segundos entre soltar cada bomba")]
    public float intervaloEntreBombas = 0.3f;

    [Header("=== 1. ATAQUE AO SOLO (ÁREA) ===")]
    [Tooltip("Ponto central no solo onde as bombas serão despejadas como um tapete.")]
    public Vector3 alvoAreaSolo;
    
    [Tooltip("Tamanho do raio da área que será bombardeada no alvo.")]
    public float raioDaArea = 40f;
    
    [Tooltip("Quantas bombas soltar aleatoriamente dentro da área de ataque.")]
    public int quantidadeBombasArea = 8;

    [Header("=== 2. PATRULHA (AUTO-MIRA) ===")]
    [Tooltip("Distância que o bombardeiro rastreia para achar alvos móveis.")]
    public float raioVisaoPatrulha = 150f;
    
    [Tooltip("O que o radar considerará inimigo (Layer). Opcional se for tudo inimigo.")]
    public LayerMask layerInimigos;

    [Header("=== 3. ATAQUE EM MASSA (2 ALVOS) ===")]
    [Tooltip("Ataque dividido ou concentrado de aniquilação em mais de um local simultaneamente.")]
    public Vector3 alvoMassa1;
    public Vector3 alvoMassa2;
    
    [Tooltip("Quantidade de bombas jogadas em CADA um dos dois alvos no modo Massa.")]
    public int quantidadePorAlvoMassa = 5;

    // --- Internas ---
    private ControleAviao controleAviao;
    private IdentidadeUnidade meuID;
    private SistemaDeDanos sistemaDanos;
    private bool emProcessoDeAtaque = false;
    private int indiceSaida = 0;
    
    // Visuais
    private GameObject marcadorAreaVisual;
    private bool marcadorCriado = false;
    private Vector3 ultimoAlvoEstrategico = Vector3.zero;

    // Estado de Voo
    private Vector3 direcaoPassagemFixa = Vector3.zero;
    private bool travouDirecao = false;
    private bool avisoSemArmamentoEmitido = false;

    void Start()
    {
        controleAviao = GetComponent<ControleAviao>();
        meuID = GetComponent<IdentidadeUnidade>();
        sistemaDanos = GetComponent<SistemaDeDanos>();
        if (sistemaDanos == null)
        {
            sistemaDanos = gameObject.AddComponent<SistemaDeDanos>();
        }
        if (sistemaDanos.vidaMaxima <= 0f)
        {
            sistemaDanos.vidaMaxima = 100f;
        }
        if (sistemaDanos.vidaAtual <= 0f)
        {
            sistemaDanos.vidaAtual = sistemaDanos.vidaMaxima;
        }

        if (comportasDeBomba == null || comportasDeBomba.Length == 0)
        {
            comportasDeBomba = PontoSaidaUtil.Garantir(transform, comportasDeBomba, "bomba", "suporte", "ponto", "saida", "lancador");
            if (comportasDeBomba == null || comportasDeBomba.Length == 0)
            {
                comportasDeBomba = new Transform[] { this.transform };
            }
        }
    }

    void LateUpdate()
    {
        SincronizarAlvoDoAeroporto();

        if (controleAviao != null && controleAviao.estaEmModoVooFisico)
        {
            Vector3 destinoForcado = controleAviao.alvoGPSVoo;
            float distanciaPreparacao = ObterDistanciaPreparacaoAtaque();
            
            if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                if (modoDeAtaque == ModoAtaque.AtaqueAoSolo && alvoAreaSolo != Vector3.zero)
                {
                    Vector3 paraAlvo = alvoAreaSolo - transform.position;
                    paraAlvo.y = 0;
                    float distSqr = paraAlvo.sqrMagnitude;

                    if (distSqr > distanciaPreparacao * distanciaPreparacao)
                    {
                        destinoForcado = alvoAreaSolo;
                        travouDirecao = false;
                    }
                    else
                    {
                        // RETA FINAL: Trava a direção para passar por cima sem oscilar
                        if (!travouDirecao)
                        {
                            direcaoPassagemFixa = transform.forward;
                            direcaoPassagemFixa.y = 0;
                            direcaoPassagemFixa.Normalize();
                            travouDirecao = true;
                        }
                        destinoForcado = transform.position + direcaoPassagemFixa * 3000f;
                    }
                }
                else if (modoDeAtaque == ModoAtaque.AtaqueEmMassa && alvoMassa1 != Vector3.zero)
                {
                    Vector3 meio = Vector3.Lerp(alvoMassa1, alvoMassa2, 0.5f);
                    Vector3 paraMeio = meio - transform.position;
                    paraMeio.y = 0;
                    
                    if (paraMeio.sqrMagnitude > distanciaPreparacao * distanciaPreparacao)
                    {
                        destinoForcado = meio;
                        travouDirecao = false;
                    }
                    else
                    {
                        if (!travouDirecao)
                        {
                            direcaoPassagemFixa = transform.forward;
                            direcaoPassagemFixa.y = 0;
                            direcaoPassagemFixa.Normalize();
                            travouDirecao = true;
                        }
                        destinoForcado = transform.position + direcaoPassagemFixa * 3000f;
                    }
                }
            }
            else
            {
                travouDirecao = false;
            }

            if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || 
                controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando)
            {
                destinoForcado.y = altitudeDeVoo;
            }

            controleAviao.alvoGPSVoo = destinoForcado;
        }

        if (emProcessoDeAtaque) return;
        if (controleAviao != null && controleAviao.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return;

        switch (modoDeAtaque)
        {
            case ModoAtaque.AtaqueAoSolo:
                ChecarAtaqueArea();
                break;
            case ModoAtaque.Patrulha:
                ChecarAtaquePatrulha();
                break;
            case ModoAtaque.AtaqueEmMassa:
                ChecarAtaqueEmMassa();
                break;
        }
    }

    private void OnGUI()
    {
        ControleUnidade cu = GetComponent<ControleUnidade>();
        if (cu == null || !cu.selecionado) return;

        // Desenha botões customizados no canto para o Bombardeiro
        float w = 180f;
        float h = 35f;
        float x = 20f;
        float y = Screen.height - 200f;

        GUI.Box(new Rect(x, y - 30, w, 180), "MODOS BOMBARD.");

        if (GUI.Button(new Rect(x + 10, y, w - 20, h), (modoDeAtaque == ModoAtaque.AtaqueAoSolo ? "[X] " : "") + "Ataque Area (Solo)"))
        {
            modoDeAtaque = ModoAtaque.AtaqueAoSolo;
            travouDirecao = false;
        }
        
        if (GUI.Button(new Rect(x + 10, y + 40, w - 20, h), (modoDeAtaque == ModoAtaque.Patrulha ? "[X] " : "") + "Radar Patrulha"))
        {
            modoDeAtaque = ModoAtaque.Patrulha;
            travouDirecao = false;
        }

        if (GUI.Button(new Rect(x + 10, y + 80, w - 20, h), (modoDeAtaque == ModoAtaque.AtaqueEmMassa ? "[X] " : "") + "Ataque em Massa"))
        {
            modoDeAtaque = ModoAtaque.AtaqueEmMassa;
            travouDirecao = false;
        }

        if (GUI.Button(new Rect(x + 10, y + 120, w - 20, h), "RETORNAR BASE"))
        {
            if (controleAviao != null) controleAviao.ordemParaRetorno = true;
        }
    }

    /// <summary>
    /// Pega as coordenadas de clique no mapa e aplica aos alvos.
    /// </summary>
    private void SincronizarAlvoDoAeroporto()
    {
        if (controleAviao == null) return;

        if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando || 
            controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            if (controleAviao.alvoEstrategico != Vector3.zero && controleAviao.alvoEstrategico != ultimoAlvoEstrategico)
            {
                ultimoAlvoEstrategico = controleAviao.alvoEstrategico;
                alvoAreaSolo = ultimoAlvoEstrategico;
                
                alvoMassa1 = ultimoAlvoEstrategico + new Vector3(-35f, 0, -10f);
                alvoMassa2 = ultimoAlvoEstrategico + new Vector3(35f, 0, 10f);

                // Reseta a trava de reta para o novo alvo
                travouDirecao = false;
                emProcessoDeAtaque = false;

                CriarMarcadorImediato();
            }
        }
    }

    private void CriarMarcadorImediato()
    {
        // Removido a pedido do usuário: o marcador 3D (X/Cilindro) não deve aparecer no mundo do jogo, apenas no mapa tático (UI)
        marcadorCriado = true;
    }

    void OnDestroy()
    {
        if (marcadorAreaVisual != null) Destroy(marcadorAreaVisual);
    }

    /// <summary>
    /// Modo 1: Solta bombas em uma ampla área ao fazer flyover.
    /// </summary>
    private void ChecarAtaqueArea()
    {
        if (alvoAreaSolo == Vector3.zero) return;
        
        Vector3 distParaAlvo = new Vector3(transform.position.x - alvoAreaSolo.x, 0, transform.position.z - alvoAreaSolo.z);
        float distanciaAtaque = ObterDistanciaAcionamentoAtaque();
        
        if (distParaAlvo.sqrMagnitude < distanciaAtaque * distanciaAtaque)
        {
            StartCoroutine(AtaqueTapeteArea());
        }
    }

    private IEnumerator AtaqueTapeteArea()
    {
        emProcessoDeAtaque = true;

        for (int i = 0; i < quantidadeBombasArea; i++)
        {
            Vector2 circulo = Random.insideUnitCircle * raioDaArea;
            // Usa o Y real do alvo (importante para a guiagem terminal da bomba funcionar)
            Vector3 pontoTapete = new Vector3(alvoAreaSolo.x + circulo.x, alvoAreaSolo.y, alvoAreaSolo.z + circulo.y);
            
            Lancamento(pontoTapete, null);
            yield return new WaitForSeconds(intervaloEntreBombas);
        }

        // Aguarda 3 segundos afastando e já dá a instrução de voltar pro hangar
        yield return new WaitForSeconds(3f); 
        emProcessoDeAtaque = false;
        
        if (controleAviao != null)
        {
            controleAviao.ordemParaRetorno = true;
            Debug.Log("[Bombardeiro] Payload solto. Retornando para a base.");
        }
    }

    /// <summary>
    /// Modo 2: Radar procurará autonomamente o inimigo e atirará de forma precisa em alvos únicos e móveis
    /// </summary>
    private static readonly Collider[] bufferRadar = new Collider[64];

    private void ChecarAtaquePatrulha()
    {
        // Detecção 3D (leva a altitude em conta para o range que é bem longo)
        int numObjetos = Physics.OverlapSphereNonAlloc(transform.position, raioVisaoPatrulha, bufferRadar, layerInimigos != 0 ? layerInimigos.value : Physics.DefaultRaycastLayers);
        
        Transform melhorAlvo = null;
        float menorDistancia = Mathf.Infinity;

        for (int i = 0; i < numObjetos; i++)
        {
            var col = bufferRadar[i];
            if (col.isTrigger) continue; // Ignora visualizadores

            // Validações de tiro amigável e IFF
            if (col.transform.root == transform.root) continue;
            
            if (meuID != null)
            {
                var idAlvo = col.GetComponent<IdentidadeUnidade>() ?? col.GetComponentInParent<IdentidadeUnidade>();
                if (idAlvo != null && idAlvo.teamID == meuID.teamID) continue; // Pula aliados
            }

            // Tem sistema de danos válido pra bater?
            var vida = col.GetComponent<SistemaDeDanos>() ?? col.GetComponentInParent<SistemaDeDanos>();
            if (vida == null || vida.vidaAtual <= 0) continue;

            float distancia = Vector3.Distance(transform.position, col.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhorAlvo = col.transform;
            }
        }
        
        for (int i = 0; i < numObjetos; i++) bufferRadar[i] = null;

        if (melhorAlvo != null)
        {
            // Marca ANTES de iniciar a coroutine para evitar chamadas duplas no mesmo frame
            emProcessoDeAtaque = true;
            StartCoroutine(AtaquePatrulhaPecisao(melhorAlvo));
        }
    }

    private IEnumerator AtaquePatrulhaPecisao(Transform alvo)
    {
        emProcessoDeAtaque = true;

        // Dispara uma mini-rajada focada e teleguiada no alvo patrulhado
        for (int i = 0; i < 3; i++)
        {
            if (alvo != null) Lancamento(alvo.position, alvo);
            yield return new WaitForSeconds(intervaloEntreBombas);
        }

        yield return new WaitForSeconds(4f);
        emProcessoDeAtaque = false;

        if (controleAviao != null && PertenceAIA())
        {
            controleAviao.ordemParaRetorno = true;
            Debug.Log("[Bombardeiro] Rajada de patrulha concluida pela IA. Retornando para o aeroporto.");
        }
    }

    /// <summary>
    /// Modo 3: Divide o bombardeio maciço entre dois alvos estáticos estratégicos selecionados em "alvoMassa1" e "alvoMassa2".
    /// </summary>
    private void ChecarAtaqueEmMassa()
    {
        if (alvoMassa1 == Vector3.zero) return;
        
        Vector3 distA1 = new Vector3(transform.position.x - alvoMassa1.x, 0, transform.position.z - alvoMassa1.z);
        Vector3 distA2 = new Vector3(transform.position.x - alvoMassa2.x, 0, transform.position.z - alvoMassa2.z);
        float distanciaAtaque = ObterDistanciaAcionamentoAtaque();

        if (distA1.sqrMagnitude < distanciaAtaque * distanciaAtaque || distA2.sqrMagnitude < distanciaAtaque * distanciaAtaque)
        {
            StartCoroutine(AtaqueDestruicaoEmMassa());
        }
    }

    private float ObterDistanciaPreparacaoAtaque()
    {
        return Mathf.Max(ObterDistanciaAcionamentoAtaque() + 70f, 140f);
    }

    private float ObterDistanciaAcionamentoAtaque()
    {
        if (projetilPrefab == null)
        {
            return 180f;
        }

        MisselBombardeiro misselBombardeiro = projetilPrefab.GetComponent<MisselBombardeiro>();
        if (misselBombardeiro != null)
        {
            return Mathf.Clamp(
                Mathf.Max(110f, misselBombardeiro.distanciaMergulho * 0.65f),
                120f,
                220f);
        }

        BombaBombardeiro bomba = projetilPrefab.GetComponent<BombaBombardeiro>();
        if (bomba != null)
        {
            float altura = Mathf.Max(40f, altitudeDeVoo);
            float gravidade = Mathf.Max(0.1f, Physics.gravity.magnitude * Mathf.Max(0.1f, bomba.multiplicadorGravidade));
            float tempoQueda = Mathf.Sqrt((2f * altura) / gravidade);
            float velocidadeHorizontal = controleAviao != null ? Mathf.Max(40f, controleAviao.velocidadeMaximaVoo) : 90f;
            return Mathf.Clamp((velocidadeHorizontal * tempoQueda) * 0.85f, 140f, 700f);
        }

        MisselTatico misselTatico = projetilPrefab.GetComponent<MisselTatico>();
        if (misselTatico != null)
        {
            return 170f;
        }

        MisselICBM misselIcbm = projetilPrefab.GetComponent<MisselICBM>();
        if (misselIcbm != null)
        {
            return 220f;
        }

        Projetil projetil = projetilPrefab.GetComponent<Projetil>();
        if (projetil != null)
        {
            return Mathf.Clamp(projetil.velocidade * 1.8f, 120f, 260f);
        }

        return 180f;
    }

    private IEnumerator AtaqueDestruicaoEmMassa()
    {
        emProcessoDeAtaque = true;

        // MASSACRE NO ALVO 1
        for (int i = 0; i < quantidadePorAlvoMassa; i++)
        {
            Vector3 diff = Random.insideUnitSphere * 12f;
            diff.y = 0;
            Vector3 pontoMassa1 = new Vector3(alvoMassa1.x + diff.x, alvoMassa1.y, alvoMassa1.z + diff.z);
            Lancamento(pontoMassa1, null);
            yield return new WaitForSeconds(intervaloEntreBombas * 0.5f);
        }

        yield return new WaitForSeconds(0.4f);

        // MASSACRE NO ALVO 2
        for (int i = 0; i < quantidadePorAlvoMassa; i++)
        {
            Vector3 diff = Random.insideUnitSphere * 12f;
            diff.y = 0;
            Vector3 pontoMassa2 = new Vector3(alvoMassa2.x + diff.x, alvoMassa2.y, alvoMassa2.z + diff.z);
            Lancamento(pontoMassa2, null);
            yield return new WaitForSeconds(intervaloEntreBombas * 0.5f);
        }

        // Vai embora pra base
        yield return new WaitForSeconds(3f); 
        emProcessoDeAtaque = false;
        
        if (controleAviao != null)
        {
            controleAviao.ordemParaRetorno = true;
        }
    }

    /// <summary>
    /// Instancia e lança a bomba/míssil usando Pool com detecção automática do tipo de armamento.
    /// Prioridade: BombaBombardeiro → MisselBombardeiro → MisselTatico → MisselICBM → Projetil genérico.
    /// </summary>
    private void Lancamento(Vector3 pontoFinalExato, Transform alvoMovelRef)
    {
        if (projetilPrefab == null)
        {
            if (!avisoSemArmamentoEmitido)
            {
                Debug.LogWarning($"[Bombardeiro] {name} está sem projetilPrefab configurado e não consegue atacar.");
                avisoSemArmamentoEmitido = true;
            }
            return;
        }
        
        Transform comporta = comportasDeBomba[indiceSaida];
        indiceSaida = (indiceSaida + 1) % comportasDeBomba.Length;
        avisoSemArmamentoEmitido = false;

        GameObject objArma = PoolDeObjetosCombate.Spawn(projetilPrefab, comporta.position, comporta.rotation);

        // ─── 1. BOMBA BALÍSTICA DO BOMBARDEIRO ───────────────────
        BombaBombardeiro bomba = objArma.GetComponent<BombaBombardeiro>();
        if (bomba != null)
        {
            bomba.SetDono(this.gameObject);
            // Fornece a velocidade horizontal atual do avião para simular inércia realista
            Vector3 velHoriz = transform.forward * controleAviao.velocidadeMaximaVoo;
            bomba.IniciarQueda(velHoriz, pontoFinalExato, this.gameObject);
            return;
        }

        // ─── 2. MÍSSIL AR-TERRA DO BOMBARDEIRO ───────────────────
        MisselBombardeiro missel = objArma.GetComponent<MisselBombardeiro>();
        if (missel != null)
        {
            missel.SetDono(this.gameObject);
            if (alvoMovelRef != null)
                missel.IniciarLancamentoRastreado(alvoMovelRef, this.gameObject);
            else
                missel.IniciarLancamento(pontoFinalExato, this.gameObject);
            return;
        }

        // ─── 3. MÍSSEIS ESPECÍFICOS DO JOGO BASE ─────────────────
        var icbm = objArma.GetComponent<MisselICBM>();
        if (icbm != null) { icbm.IniciarLancamento(pontoFinalExato); return; }

        var tatico = objArma.GetComponent<MisselTatico>();
        if (tatico != null) { tatico.IniciarLancamento(pontoFinalExato); return; }

        // ─── 4. PROJÉTIL GENÉRICO (Fallback) ─────────────────────
        Projetil p = objArma.GetComponent<Projetil>();
        if (p == null) p = objArma.AddComponent<Projetil>();
        
        p.SetDono(this.gameObject);

        if (alvoMovelRef != null)
        {
            p.SetAlvo(alvoMovelRef);
            if (p.curvaDePerseguicao == 0f) p.curvaDePerseguicao = 90f;
        }
        else
        {
            Vector3 direcaoPrecisa = (pontoFinalExato - comporta.position).normalized;
            p.SetDirecao(direcaoPrecisa);
        }
    }

    private bool PertenceAIA()
    {
        if (meuID == null)
        {
            meuID = GetComponent<IdentidadeUnidade>();
        }

        return meuID != null && meuID.teamID > 1;
    }
}
