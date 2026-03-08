using UnityEngine;
using System.Collections; // Necessário para o atraso da morte
using System; // Para eventos

public class SistemaDeDanos : MonoBehaviour
{
    // === EVENTOS PARA OUTROS SCRIPTS ===
    public event Action OnDano;  // Disparado quando recebe dano
    public event Action OnMorte; // Disparado quando morre
    
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

    // Referências aos efeitos ativos (para poder desligar depois/trocar)
    private GameObject fxFumacaLeve;
    private GameObject fxFumacaGrave;
    private GameObject fxFogo;
    
    // Estados
    private bool morreu = false;

    void Start()
    {
        vidaAtual = vidaMaxima;
        
        // Auto-detecta se é Muro pela tag
        if (gameObject.CompareTag("Destrutivel") || gameObject.name.Contains("Muro") || gameObject.name.Contains("Wall"))
        {
            ehEstrutura = true;
        }
    }

    public void AtualizarVidaMaxima(int bonus)
    {
        vidaMaxima += bonus;
        vidaAtual += bonus; 
    }

    public void ReceberDano(float dano)
    {
        if (morreu) return;

        vidaAtual -= dano;
        float porcentagem = vidaAtual / vidaMaxima;
        
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
            if (unidadeBiologica) MorrerBiologico();
            else if (ehEstrutura) MorrerEstrutura();
            else StartCoroutine(SequenciaDeMorte());
        }
        else
        {
             // Opcional: Debug.Log($"Vida restante do {gameObject.name}: {vidaAtual}");
        }
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

    void ExplodirFinal()
    {
        if(GerenciadorFXGlobal.Instancia != null)
        {
            // ⚫ Fase 5: Explosão Final (+30% tamanho)
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, tamanhoDoEfeito * 1.3f);
        }
        TocarSomExplosao();
    }

    void TocarSomExplosao()
    {
        if (somExplosaoExclusivo != null)
        {
            AudioSource.PlayClipAtPoint(somExplosaoExclusivo, transform.position);
        }
        else if(GerenciadorFXGlobal.Instancia != null && GerenciadorFXGlobal.Instancia.somExplosao != null)
        {
            AudioSource.PlayClipAtPoint(GerenciadorFXGlobal.Instancia.somExplosao, transform.position);
        }
    }

    void DesativarUnidade()
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

        // Tenta desativar scripts de controle
        foreach(var script in GetComponents<MonoBehaviour>())
        {
            if (script == null) continue; // PREVENÇÃO DE MISSING SCRIPT (Evita Crash Silencioso)
            if (script == this) continue; // Não desativa a si mesmo ainda
            if (script.GetType().Name.Contains("Controle") || 
                script.GetType().Name.Contains("Patrulha") ||
                script.GetType().Name.Contains("Helicopter"))
            {
                script.enabled = false;
            }
        }
    }

    void MorrerBiologico()
    {
        morreu = true;
        OnMorte?.Invoke(); // Notifica a morte
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
        OnMorte?.Invoke();
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
        morreu = true;
        OnMorte?.Invoke(); // Notifica a morte
        
        // ⚫ Fase 5: Colapso Total
        // 1. Desativa controles imediatamente
        DesativarUnidade(); 

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
             }
        }

        yield return new WaitForSeconds(0.5f); // Breve momento de colapso

        // 4. Explosão Final (+30%)
        ExplodirFinal();

        // 5. Destroços
        if (prefabDestrocos != null)
        {
            GameObject destrocos = Instantiate(prefabDestrocos, transform.position, transform.rotation);
            // Destroços podem ter escala ajustada se necessário
            destrocos.transform.localScale = transform.localScale; 
            LimparDestroco(destrocos);
            // Tanques destruídos ficam um tempo e somem
            Destroy(destrocos, 60.0f);
        }

        // 6. Remove a unidade
        Destroy(gameObject);
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
