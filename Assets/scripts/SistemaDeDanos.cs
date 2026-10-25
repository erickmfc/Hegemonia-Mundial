using UnityEngine;
using System.Collections; // Necessário para o atraso da morte
using System; // Para eventos
using System.Collections.Generic;

public class SistemaDeDanos : MonoBehaviour
{
    private static readonly List<IdentidadeUnidade> candidatosAgressor = new List<IdentidadeUnidade>(256);

    // === EVENTOS PARA OUTROS SCRIPTS ===
    public event Action OnDano;  // Disparado quando recebe dano
    public event Action OnMorte; // Disparado quando morre
    public static event Action<SistemaDeDanos, GameObject, float> OnDanoGlobal;
    public static event Action<SistemaDeDanos, GameObject> OnMorteGlobal;
    
    [Header("Configuração Vital")]
    public float vidaMaxima = 100f;
    public float vidaAtual;

    [Header("Tipo de Unidade")]
    [Tooltip("Marque se for Soldado ou Monstro (sangra/morre sem explodir). Desmarque para Tanques/Prédios.")]
    public bool unidadeBiologica = false; 
    [Tooltip("Marque se for um Muro ou Estrutura simples (sem efeitos de fumaça complexos, apenas quebra).")]
    public bool ehEstrutura = false;

    [Header("Personalização Visual")]
    public GameObject prefabDestrocos; // O modelo 3D do tanque destruído/queimado OU corpo do soldado
    public AudioClip somExplosaoExclusivo; // Som específico desta unidade (opcional)
    [Range(0.1f, 10f)]
    public float tamanhoDoEfeito = 1.0f; // 1 = Soldado, 5 = Tanque, 10 = Porta-aviões
    public Vector3 ajusteDePosicao = Vector3.zero; // Para subir ou descer o fogo
    [Header("Afundamento Naval")]
    public bool habilitarAfundamentoNaval = true;
    public float anguloAfundamentoX = -6f;
    [Min(0.1f)] public float duracaoAfundamento = 55f;
    [Min(0f)] public float atrasoPosAfundamento = 0.6f;

    // Referências aos efeitos ativos (para poder desligar depois/trocar)
    private GameObject fxFumacaLeve;
    private GameObject fxFumacaGrave;
    private GameObject fxFogo;
    
    // Estados
    private bool morreu = false;
    private int ultimoAgressorTeamId = -1;
    private GameObject ultimoAgressor;

    /// <summary>
    /// Resolve a identidade da entidade mesmo quando o collider, o sistema de
    /// dano e o marcador de dono estão em níveis diferentes da hierarquia.
    /// Estruturas navais antigas usam as três variações (raiz, filho e pai),
    /// por isso a resolução não pode depender somente de GetComponent.
    /// </summary>
    public static IdentidadeUnidade ResolverIdentidade(Component origem)
    {
        if (origem == null) return null;

        IdentidadeUnidade identidade = origem.GetComponent<IdentidadeUnidade>();
        if (identidade != null) return identidade;

        identidade = origem.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) return identidade;

        // Quando o SistemaDeDanos está na raiz e a identidade está em um
        // marcador filho, a busca no próprio objeto completa o caso.
        identidade = origem.GetComponentInChildren<IdentidadeUnidade>(true);
        return identidade;
    }

    void Start()
    {
        vidaAtual = vidaMaxima;
        
        // O nome de alguns navios também contém "Wall" (por exemplo,
        // Des_Wall). A detecção por nome só pode marcar estruturas depois de
        // excluir plataformas que já possuem componentes/identidade naval.
        if (!EhEmbarcacaoConfigurada()
            && (gameObject.CompareTag("Destrutivel")
                || gameObject.name.Contains("Muro")
                || gameObject.name.Contains("Wall")))
        {
            ehEstrutura = true;
        }
    }

    public void AtualizarVidaMaxima(int bonus)
    {
        vidaMaxima += bonus;
        vidaAtual += bonus; 
    }

    public void ReceberDano(float dano, UnityEngine.GameObject agressor = null)
    {
        if (morreu) return;

        // Alguns mísseis antigos não carregam o dono no impacto. Quando isso
        // acontece, tenta identificar uma unidade hostil que esteja próxima do
        // alvo para não perder a autoria do ataque.
        if (agressor == null)
        {
            agressor = InferirAgressorProximo();
        }
        ultimoAgressor = agressor;

        vidaAtual -= dano;
        float porcentagem = vidaAtual / vidaMaxima;
        
        if (agressor != null)
        {
            IdentidadeUnidade vitimaID = ResolverIdentidade(this);
            IdentidadeUnidade agressorID = ResolverIdentidade(agressor.transform);

            if (vitimaID != null && agressorID != null && vitimaID.teamID != agressorID.teamID)
            {
                // O cartel e uma faccao criminosa, nao um pais. Ataques contra
                // ele (ou dele) nao podem abrir uma guerra diplomatica.
                bool envolveCartel = vitimaID.teamID == 9 || agressorID.teamID == 9;
                ultimoAgressorTeamId = agressorID.teamID;
                if (!envolveCartel && SistemaGovernoMundial.Instancia != null)
                {
#if UNITY_EDITOR
                    if (Projetil.IdProbeProjectileNoDano > 0
                        && Application.isPlaying
                        && string.Equals(
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                            "Assets/Tests/PlayMode/IA03_WarValidation.unity",
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEngine.Debug.Log("[IA03][PROJECTILE_PROBE][DAMAGE] id="
                            + Projetil.IdProbeProjectileNoDano
                            + " frame=" + UnityEngine.Time.frameCount
                            + " victim=" + name
                            + " victimTeam=" + vitimaID.teamID
                            + " victimIdentity=" + vitimaID.name + "#" + vitimaID.GetInstanceID()
                            + " aggressor=" + agressor.name
                            + " aggressorTeam=" + agressorID.teamID
                            + " aggressorIdentity=" + agressorID.name + "#" + agressorID.GetInstanceID(),
                            this);
                    }
#endif
                    // O governo registra cada impacto e decide quando a crise passa a guerra.
                    SistemaGovernoMundial.Instancia.RegistrarAgressao(vitimaID.teamID, agressorID.teamID);
                }
            }
        }
        
        OnDanoGlobal?.Invoke(this, agressor, dano);

        // Notifica outros sistemas que recebeu dano
        OnDano?.Invoke();

        // Se for máquina, aplica o Protocolo de Estado Visual
        // Estruturas (Muros) geralmente não soltam fumaça gradual, só quebram no final ou soltam poeira
        if (!unidadeBiologica && !ehEstrutura)
        {
            GerenciarEstadosDano(porcentagem);
        }

        if (vidaAtual <= 0)
        {
            IdentidadeUnidade identidadeMorta = ResolverIdentidade(this);
            if (identidadeMorta != null && identidadeMorta.teamID == 9
                && ultimoAgressorTeamId > 0
                && SistemaGovernoMundial.Instancia != null)
            {
                SistemaGovernoMundial.Instancia.AplicarBonusFelicidadeCartel(ultimoAgressorTeamId);
            }

            if (unidadeBiologica) MorrerBiologico();
            else if (ehEstrutura && !DeveAfundarComoEmbarcacao()) MorrerEstrutura();
            else StartCoroutine(SequenciaDeMorte());
        }
        else
        {
             // Opcional: Debug.Log($"Vida restante do {gameObject.name}: {vidaAtual}");
        }
    }

    private GameObject InferirAgressorProximo()
    {
        IdentidadeUnidade vitima = ResolverIdentidade(this);
        if (vitima == null || vitima.teamID <= 0) return null;

        // Identidades ativas já são mantidas pelo registro central no ciclo
        // OnEnable/OnDisable. Reutilizar a lista evita uma busca e um array
        // novo em cada impacto que chega sem autoria do projétil.
        RegistroEntidadesJogo.FillUnidades(candidatosAgressor);
        IdentidadeUnidade melhor = null;
        float menorDistancia = 120f * 120f;
        for (int i = 0; i < candidatosAgressor.Count; i++)
        {
            IdentidadeUnidade candidata = candidatosAgressor[i];
            if (candidata == null || !candidata.isActiveAndEnabled
                || candidata.teamID <= 0 || candidata.teamID == vitima.teamID
                || candidata.tipoUnidade == TipoUnidade.Estrutura) continue;

            float distancia = (candidata.transform.position - transform.position).sqrMagnitude;
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhor = candidata;
            }
        }
        return melhor != null ? melhor.gameObject : null;
    }

    // --- MÉTODOS DE REPARO (USADO PELO PIER DE MANUTENÇÃO) ---
    public void Reparar(float quantidade)
    {
        if (morreu) return;

        vidaAtual = Mathf.Min(vidaAtual + quantidade, vidaMaxima);
        float porcentagem = vidaAtual / vidaMaxima;

        // Atualiza visual (Remove fumaça se estiver bom)
        if (!unidadeBiologica)
        {
            GerenciarEstadosDano(porcentagem);
        }
    }

    void GerenciarEstadosDano(float porcentagem)
    {
        // 🟢 Fase 1: Operacional (> 70%)
        if (porcentagem > 0.70f)
        {
            LimparTodosEfeitos();
        }
        // 🟡 Fase 2: Avaria Leve (<= 70% e > 40%) -> Fumaça Branca
        else if (porcentagem <= 0.70f && porcentagem > 0.40f)
        {
            if (fxFumacaLeve == null) fxFumacaLeve = CriarFxContinuo("FumacaLeve");
            
            // Garante que os mais graves estejam desligados se foi reparado
            if (fxFumacaGrave != null) DestruirFx(ref fxFumacaGrave);
            if (fxFogo != null) DestruirFx(ref fxFogo);
        }
        // 🟠 Fase 3: Avaria Grave (<= 40% e > 20%) -> Fumaça Preta
        else if (porcentagem <= 0.40f && porcentagem > 0.20f)
        {
            // Troca Branca pela Preta
            if (fxFumacaLeve != null) DestruirFx(ref fxFumacaLeve);
            
            if (fxFumacaGrave == null) fxFumacaGrave = CriarFxContinuo("FumacaEscura");
            
            if (fxFogo != null) DestruirFx(ref fxFogo);
        }
        // 🔴 Fase 4: Estado Crítico (<= 20%) -> Fogo + Fumaça Preta
        else if (porcentagem <= 0.20f && porcentagem > 0f)
        {
            if (fxFumacaLeve != null) DestruirFx(ref fxFumacaLeve);
            
            // Mantém ou cria a fumaça preta
            if (fxFumacaGrave == null) fxFumacaGrave = CriarFxContinuo("FumacaEscura");
            
            // Adiciona Fogo
            if (fxFogo == null) fxFogo = CriarFxContinuo("Fogo");
        }
    }

    GameObject CriarFxContinuo(string tipo)
    {
        if (GerenciadorFXGlobal.Instancia != null)
        {
            // Cria o efeito e já define este objeto como pai
            GameObject fx = GerenciadorFXGlobal.Instancia.CriarEfeitoContinuo(tipo, this.transform);
            
            // Ajuste de posição (Motor/Exaustor)
            if (fx != null)
            {
                fx.transform.localPosition = ajusteDePosicao;
                fx.transform.localScale = Vector3.one * tamanhoDoEfeito; // Aplica escala baseada no tamanho da unidade
                
                // --- FORÇA O LOOP INFINITO (CORREÇÃO DE FUMAÇA PARANDO) ---
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    // Para o sistema antes de modificar configurações
                    if(ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    
                    var main = ps.main;
                    main.loop = true; // Garante que não pare nunca
                    main.duration = 1.0f; // Duração curta para loop rápido
                    
                    // Reinicia o sistema
                    ps.Play();
                }
                
                // Tenta forçar loop nos filhos também (caso seja um efeito composto)
                foreach(var psFilho in fx.GetComponentsInChildren<ParticleSystem>())
                {
                    if(psFilho.isPlaying) psFilho.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    
                    var m = psFilho.main;
                    m.loop = true;
                    m.duration = 1.0f;
                    
                    psFilho.Play();
                }
            }
            return fx;
        }
        return null;
    }

    void DestruirFx(ref GameObject efeito)
    {
        if (efeito != null)
        {
            Destroy(efeito);
            efeito = null;
        }
    }

    void LimparTodosEfeitos()
    {
        DestruirFx(ref fxFumacaLeve);
        DestruirFx(ref fxFumacaGrave);
        DestruirFx(ref fxFogo);
    }

    void ExplodirFinal(Vector3? posicao = null)
    {
        Vector3 posicaoEfeito = posicao ?? transform.position;
        if(GerenciadorFXGlobal.Instancia != null)
        {
            // ⚫ Fase 5: Explosão Final (+30% tamanho)
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", posicaoEfeito, tamanhoDoEfeito * 1.3f);
        }
        TocarSomExplosao(posicaoEfeito);
    }

    void TocarSomExplosao(Vector3? posicao = null)
    {
        Vector3 posicaoSom = posicao ?? transform.position;
        if (somExplosaoExclusivo != null)
        {
            AudioRuntime.PlayClipAtPoint(somExplosaoExclusivo, posicaoSom);
        }
        else if(GerenciadorFXGlobal.Instancia != null && GerenciadorFXGlobal.Instancia.somExplosao != null)
        {
            AudioRuntime.PlayClipAtPoint(GerenciadorFXGlobal.Instancia.somExplosao, posicaoSom);
        }
    }

    void DesativarUnidade(bool pararHierarquiaInteira = false)
    {
        var sistemaTiro = GetComponent<SistemaDeTiro>();
        if (sistemaTiro != null) sistemaTiro.enabled = false;

        var navMesh = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMesh != null) 
        {
            if (navMesh.isOnNavMesh && navMesh.isActiveAndEnabled)
            {
                navMesh.isStopped = true;
            }
            navMesh.enabled = false;
        }

        // Navios podem ter controles fora da raiz (por exemplo, o ciclo do
        // petroleiro e os controladores das torretas). Durante o afundamento,
        // qualquer comportamento restante poderia mover ou operar o casco.
        MonoBehaviour[] scripts = pararHierarquiaInteira
            ? GetComponentsInChildren<MonoBehaviour>(true)
            : GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue; // PREVENÇÃO DE MISSING SCRIPT (Evita Crash Silencioso)
            if (script == this) continue; // Não desativa a si mesmo ainda
            if (pararHierarquiaInteira
                || script is ControleUnidade
                || script.GetType().Name.Contains("Controle") ||
                script.GetType().Name.Contains("Patrulha") ||
                script.GetType().Name.Contains("Helicopter"))
            {
                script.enabled = false;
            }
        }

        if (!pararHierarquiaInteira) return;

        foreach (var agente in GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
        {
            if (agente == null) continue;
            if (agente.isActiveAndEnabled && agente.isOnNavMesh) agente.isStopped = true;
            agente.enabled = false;
        }

        foreach (var colisor in GetComponentsInChildren<Collider>(true))
        {
            if (colisor != null) colisor.enabled = false;
        }

        foreach (var corpo in GetComponentsInChildren<Rigidbody>(true))
        {
            if (corpo == null) continue;
            if (!corpo.isKinematic)
            {
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
            corpo.isKinematic = true;
            corpo.detectCollisions = false;
        }

        foreach (var particulas in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particulas != null)
                particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void MorrerBiologico()
    {
        morreu = true;
        NotificarMorte();
        DesativarUnidade();
        
        // Efeito de Sangue
        if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("Sangue", transform.position + Vector3.up, 1.0f);
        }

        if (prefabDestrocos != null)
        {
            // Instancia o corpo com a mesma posição e rotação do vivo (Em Pé)
            // O script "AjusteCadaver" no prefab cuidará de deitar o corpo e limpar componentes.
            GameObject corpo = Instantiate(prefabDestrocos, transform.position, transform.rotation);
            
            // Verifica se o usuário esqueceu de colocar o script
            AjusteCadaver ajuste = corpo.GetComponent<AjusteCadaver>();
            if (ajuste == null)
            {
                // Adiciona o script de ajuste automaticamente se faltar
                Debug.LogWarning($"[SistemaDeDanos] O prefab {prefabDestrocos.name} não tem o script 'AjusteCadaver'. Adicionando automaticamente...");
                ajuste = corpo.AddComponent<AjusteCadaver>();
                ajuste.rotacaoX = 180f; // Força 180 conforma solicitado, se não tiver script
            }
            
            // Se já tiver script, confiamos na configuração do inspector dele.
            LimparDestroco(corpo);
            Destroy(corpo, 60.0f); // Fica 1 min no chão
        }
        
        Destroy(gameObject);
    }

    void MorrerEstrutura() // Lógica para Muros/Caixas
    {
        Debug.Log($"🧱 [SistemaDeDanos] O muro/estrutura '{gameObject.name}' chegou a vida 0! Iniciando Destruição...");
        morreu = true;
        NotificarMorte();
        DesativarUnidade();

        // Toca som de desmoronamento/quebra
        TocarSomExplosao(); 

        // Cria Poeira/Destroços
        if (GerenciadorFXGlobal.Instancia != null)
        {
            // Poeira cinza subindo
            GerenciadorFXGlobal.Instancia.TocarEfeito("FumacaLeve", transform.position, tamanhoDoEfeito * 2f);
        }

        // Troca pelo modelo destruído (ex: muro quebrado)
        if (prefabDestrocos != null)
        {
            GameObject escombros = Instantiate(prefabDestrocos, transform.position, transform.rotation);
            escombros.transform.localScale = transform.localScale; 
            LimparDestroco(escombros);
            // Escombros de muro geralmente ficam para sempre ou por muito tempo
            // Destroy(escombros, 60.0f);
        }

        Destroy(gameObject);
    }

    IEnumerator SequenciaDeMorte()
    {
        bool afundarNavio = DeveAfundarComoEmbarcacao();
        Vector3 posicaoNaSuperficie = transform.position;
        float alturaAgua = posicaoNaSuperficie.y;
        if (afundarNavio && RegistroSuperficieMapa.TryGetAltura(
                transform.position, TipoSuperficieMapa.Agua, out alturaAgua))
        {
            posicaoNaSuperficie.y = alturaAgua;
        }

        morreu = true;
        NotificarMorte();
        
        // ⚫ Fase 5: Colapso Total
        // 1. Desativa controles imediatamente
        DesativarUnidade(afundarNavio);

        // 2. Limpa efeitos de "avaria" para limpar a cena para a explosão
        LimparTodosEfeitos();

        // 3. Pequeno delay dramático ou instantâneo?
        // O usuário disse: "fogo queimando mais forte 50% ate o prefab mudar".
        // Vamos simular isso criando um fogo temporário maior antes do Kabum.
        if (GerenciadorFXGlobal.Instancia != null)
        {
             // Cria efeito contínuo anexado à unidade (vai sumir junto com ela em breve)
             GameObject fogoFinal = GerenciadorFXGlobal.Instancia.CriarEfeitoContinuo("Fogo", transform);
             if (fogoFinal != null)
             {
                 fogoFinal.transform.localScale = Vector3.one * tamanhoDoEfeito * 1.5f; // +50% força
                 if (afundarNavio)
                     fogoFinal.transform.position = posicaoNaSuperficie;
             }
        }

        yield return new WaitForSeconds(0.5f); // Breve momento de colapso

        if (afundarNavio)
        {
            // A explosão acontece na superfície antes de o casco desaparecer.
            ExplodirFinal(posicaoNaSuperficie);
            yield return StartCoroutine(RotinaAfundamentoNaval(posicaoNaSuperficie));
        }
        else
        {
            ExplodirFinal();
        }

        // 5. Destroços permanecem na superfície mesmo com o casco já submerso.
        if (prefabDestrocos != null)
        {
            Vector3 posicaoDestrocos = afundarNavio ? posicaoNaSuperficie : transform.position;
            GameObject destrocos = Instantiate(prefabDestrocos, posicaoDestrocos, transform.rotation);
            // Destroços podem ter escala ajustada se necessário
            destrocos.transform.localScale = transform.localScale; 
            LimparDestroco(destrocos);
            // Tanques destruídos ficam um tempo e somem
            Destroy(destrocos, 60.0f);
        }

        // 6. Remove a unidade
        Destroy(gameObject);
    }

    private void NotificarMorte()
    {
        // O evento global é apenas uma leitura do mesmo evento de morte que a
        // unidade já usava. A Carta não assume a aplicação de dano nem decide
        // quem venceu o combate.
        CartaCombateRegistro.RegistrarUnidadeDestruida(this, ultimoAgressor);
        OnMorteGlobal?.Invoke(this, ultimoAgressor);
        OnMorte?.Invoke();
    }

    bool DeveAfundarComoEmbarcacao()
    {
        if (!habilitarAfundamentoNaval || unidadeBiologica)
        {
            return false;
        }

        // Componentes navais são a fonte mais forte de classificação. Alguns
        // prefabs antigos receberam ehEstrutura pela heurística do nome, mas
        // continuam sendo navios e devem completar a sequência de naufrágio.
        if (EhEmbarcacaoConfigurada())
        {
            return true;
        }

        if (ehEstrutura)
        {
            return false;
        }

        return false;
    }

    private bool EhEmbarcacaoConfigurada()
    {
        if (GetComponentInChildren<HovercraftTransporte>(true) != null
            || GetComponentInChildren<ControleNavioRealista>(true) != null
            || GetComponentInChildren<ControleSubmarino>(true) != null
            || GetComponentInChildren<NavioPetroleiro>(true) != null
            || GetComponentInChildren<IdentidadeNaval>(true) != null)
        {
            return true;
        }

        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInChildren<IdentidadeUnidade>(true);
        return identidade != null && identidade.tipoUnidade == TipoUnidade.Naval;
    }

    IEnumerator RotinaAfundamentoNaval(Vector3 posicaoNaSuperficie)
    {
        float tempo = 0f;
        float duracao = Mathf.Max(0.1f, duracaoAfundamento);
        Bounds limitesVisuais = ObterLimitesVisuaisAfundamento();
        Transform visual = CriarPivotVisualAfundamento(limitesVisuais);
        float profundidade = Mathf.Max(3f, limitesVisuais.extents.y + 2f);
        Vector3 posicaoVisualInicial = visual != null ? visual.position : transform.position;
        Vector3 posicaoVisualFinal = posicaoVisualInicial;
        posicaoVisualFinal.y = posicaoNaSuperficie.y - profundidade;
        // Prefabs antigos guardam o ângulo como -46°, que levantava a proa
        // (o forward naval do jogo é +Z). No naufrágio, a rotação deve baixar
        // a proa; usar o módulo mantém os ajustes de intensidade existentes.
        float anguloProaParaBaixo = Mathf.Abs(anguloAfundamentoX);
        Quaternion inclinacaoLocalAfundamento = Quaternion.AngleAxis(anguloProaParaBaixo, Vector3.right);
        Quaternion rotacaoVisualFinal = inclinacaoLocalAfundamento;

        Quaternion rotacaoRaizInicial = transform.rotation;
        Vector3 posicaoRaizInicial = transform.position;
        Quaternion rotacaoRaizFinal = rotacaoRaizInicial * inclinacaoLocalAfundamento;

        while (tempo < duracao)
        {
            float t = Mathf.Clamp01(tempo / duracao);
            float inclinacao = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
            float submersao = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.28f) / 0.72f));

            if (visual != null)
            {
                visual.localRotation = Quaternion.Slerp(Quaternion.identity, rotacaoVisualFinal, inclinacao);
                visual.position = Vector3.Lerp(posicaoVisualInicial, posicaoVisualFinal, submersao);
            }
            else
            {
                // Fallback para prefabs navais sem malhas em objetos filhos.
                transform.rotation = Quaternion.Slerp(rotacaoRaizInicial, rotacaoRaizFinal, inclinacao);
                transform.position = Vector3.Lerp(posicaoRaizInicial, posicaoVisualFinal, submersao);
            }

            tempo += Time.deltaTime;
            yield return null;
        }

        if (visual != null)
        {
            visual.localRotation = rotacaoVisualFinal;
            visual.position = posicaoVisualFinal;
        }
        else
        {
            transform.rotation = rotacaoRaizFinal;
            transform.position = posicaoVisualFinal;
        }

        if (atrasoPosAfundamento > 0f)
        {
            yield return new WaitForSeconds(atrasoPosAfundamento);
        }
    }

    private Bounds ObterLimitesVisuaisAfundamento()
    {
        Renderer[] renderizadores = GetComponentsInChildren<Renderer>(true);
        Bounds limites = new Bounds(transform.position, Vector3.one * 2f);
        bool encontrouMalha = false;

        foreach (Renderer renderizador in renderizadores)
        {
            if (!(renderizador is MeshRenderer) && !(renderizador is SkinnedMeshRenderer)) continue;

            if (!encontrouMalha)
            {
                limites = renderizador.bounds;
                encontrouMalha = true;
            }
            else
            {
                limites.Encapsulate(renderizador.bounds);
            }
        }

        return limites;
    }

    private Transform CriarPivotVisualAfundamento(Bounds limitesVisuais)
    {
        var raizesVisuais = new List<Transform>();
        foreach (Transform filho in transform)
        {
            if (filho.GetComponentInChildren<MeshRenderer>(true) != null
                || filho.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                raizesVisuais.Add(filho);
            }
        }

        if (raizesVisuais.Count == 0) return null;

        var objetoPivot = new GameObject("VisualAfundamento_Runtime");
        Transform pivot = objetoPivot.transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = transform.InverseTransformPoint(limitesVisuais.center);
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        foreach (Transform raizVisual in raizesVisuais)
        {
            if (raizVisual != null) raizVisual.SetParent(pivot, true);
        }

        return pivot;
    }

    void LimparDestroco(GameObject obj)
    {
        // 1. Remove qualquer sistema de controle que possa ter vindo copiado no Prefab
        var controles = obj.GetComponentsInChildren<ControleUnidade>();
        foreach (var c in controles) Destroy(c);
        
        var selecoes = obj.GetComponentsInChildren<GerenteSelecao>();
        foreach (var s in selecoes) Destroy(s);
        
        var tiros = obj.GetComponentsInChildren<SistemaDeTiro>();
        foreach (var t in tiros) Destroy(t);
        
        var id = obj.GetComponentsInChildren<IdentidadeUnidade>();
        foreach (var i in id) Destroy(i);
        
        // 2. Tira ele da listagem removendo Tag e Layer de Seleção
        obj.tag = "Untagged";
        obj.layer = 0; // Default layer

        // 3. Remove visuais de seleção que podem ter ficado presos
        Transform circulo = obj.transform.Find("CirculoSelecao");
        if (circulo != null) Destroy(circulo.gameObject);
        Transform selecaoUI = obj.transform.Find("SelecaoUI");
        if (selecaoUI != null) Destroy(selecaoUI.gameObject);
    }
}
