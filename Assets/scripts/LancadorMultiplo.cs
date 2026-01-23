using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class LancadorMultiplo : MonoBehaviour
{
    [Header("Configurações do Lançador")]
    [Tooltip("Arraste o Prefab do Míssil (Ex: Comar) aqui.")]
    public GameObject misselPrefab;

    [Tooltip("Componente ou objeto filho que vai girar 90 graus (A tampa/caixa de mísseis).")]
    public Transform compartimento;

    [Tooltip("Lista dos 12 pontos de saída dos mísseis.")]
    public Transform[] saidasDeMissel;

    [Header("Animação")]
    [Tooltip("Ângulo que o compartimento deve levantar (Eixo X local).")]
    public float anguloAberto = -90f; // Ajuste para +90 ou -90 conforme o eixo do modelo
    public float velocidadeAbrir = 45f; // Graus por segundo

    [Header("Tempos")]
    [Tooltip("Tempo que espera com a tampa aberta ANTES de começar a atirar.")]
    public float tempoPreparacao = 1.0f;
    [Tooltip("Tempo entre um míssil e outro na rajada.")]
    public float intervaloDisparo = 0.2f;
    
    [Header("Automático / Manual")]
    [Tooltip("Se TRUE, atira sozinho em inimigos quando parado.")]
    public bool modoAutomatico = true; // "Ativo" = Vermelho
    public bool lancamentoManual = false; // Flag ativada pelo botão ou tecla 'L'

    [Header("Radar")]
    public float alcanceRadar = 150f;
    public string[] tagsAlvo = { "Inimigo", "Destrutivel" }; // Pode atacar chão ou ar

    // Internas
    private bool estahAberto = false;
    private bool emDisparo = false;
    private NavMeshAgent agente;
    private ControleUnidade controle;
    private Transform alvoAtual;
    private IdentidadeUnidade meuID;
    private float anguloOriginalX; // Para saber onde voltar

    [Header("Visual do Alcance")]
    public bool mostrarAlcanceNoJogo = true;
    public Color corAlcanceAtivo = new Color(1f, 0f, 0f, 0.5f);
    public Color corAlcancePassivo = new Color(0f, 1f, 0f, 0.5f);
    public float larguraLinha = 0.5f; // "bem fino"
    private LineRenderer linhaAlcance;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        controle = GetComponent<ControleUnidade>();
        meuID = GetComponent<IdentidadeUnidade>();

        ConfigurarLinhaAlcance();

        // AUTO-CONFIGURAÇÃO: Tenta achar referências perdidas
        if (compartimento == null)
        {
            Transform t = transform.Find("Compartimento");
            if (t == null) t = transform.Find("Turret");
            if (t == null) t = transform.Find("Torreta");
            // Tenta nos filhos profundos se não achou
            if (t == null) 
            {
                foreach(Transform child in GetComponentsInChildren<Transform>())
                {
                    if(child.name == "Compartimento") { t = child; break; }
                }
            }

            if (t != null) 
            {
                compartimento = t;
                Debug.Log($"[LancadorMultiplo] Compartimento encontrado automaticamente: {t.name}");
            }
            else
            {
                 Debug.LogError($"[LancadorMultiplo] ERRO CRÍTICO: 'Compartimento' não foi atribuído no Inspector e não achei nada com esse nome!");
            }
        }

        if (compartimento != null)
        {
            anguloOriginalX = compartimento.localEulerAngles.x;
            if (anguloOriginalX > 180) anguloOriginalX -= 360;
        }

        if (saidasDeMissel == null || saidasDeMissel.Length == 0)
        {
            var todosFilhos = GetComponentsInChildren<Transform>();
            var lista = new System.Collections.Generic.List<Transform>();
            foreach(var f in todosFilhos)
            {
                if(f.name.Contains("Saida") || f.name.Contains("Element")) lista.Add(f);
            }
            saidasDeMissel = lista.ToArray();
            Debug.Log($"[LancadorMultiplo] Auto-configuradas {saidasDeMissel.Length} saídas de míssil.");
        }
    }

    void ConfigurarLinhaAlcance()
    {
        if (!mostrarAlcanceNoJogo) return;

        GameObject linhaObj = new GameObject("VisualAlcance");
        linhaObj.transform.SetParent(transform);
        linhaObj.transform.localPosition = Vector3.zero;

        linhaAlcance = linhaObj.AddComponent<LineRenderer>();
        linhaAlcance.useWorldSpace = true; // Radar fixo no mundo
        
        linhaAlcance.loop = true;
        linhaAlcance.positionCount = 60; // Mais suave
        linhaAlcance.startWidth = 1.0f; // Mais visível
        linhaAlcance.endWidth = 1.0f;
        
        // Material Simples
        Material mat = new Material(Shader.Find("Sprites/Default"));
        linhaAlcance.material = mat;
        
        // Sombra Off
        linhaAlcance.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linhaAlcance.receiveShadows = false;
    }

    void AtualizarLinhaAlcance()
    {
        if (linhaAlcance == null) return;

        // Cor
        Color corAlvo = modoAutomatico ? corAlcanceAtivo : corAlcancePassivo;
        linhaAlcance.startColor = corAlvo;
        linhaAlcance.endColor = corAlvo;

        bool devoMostrar = mostrarAlcanceNoJogo;
        if (controle != null) devoMostrar = devoMostrar && controle.selecionado; 
        
        linhaAlcance.enabled = devoMostrar;

        if (devoMostrar)
        {
            float anguloPasso = 360f / linhaAlcance.positionCount;
            for (int i = 0; i < linhaAlcance.positionCount; i++)
            {
                float ang = i * anguloPasso * Mathf.Deg2Rad;
                float x = Mathf.Cos(ang) * alcanceRadar;
                float z = Mathf.Sin(ang) * alcanceRadar;
                
                // 6.0f para garantir que fique acima de terrenos irregulares
                Vector3 pos = transform.position + new Vector3(x, 6.0f, z); 
                linhaAlcance.SetPosition(i, pos);
            }
        }
    }

    private float timerDebug = 0;

    void Update()
    {
        // DEBUG: Forçar disparo com K
        if(Input.GetKeyDown(KeyCode.K))
        {
            Debug.LogWarning("[LancadorMultiplo] FORÇANDO DISPARO DE TESTE (Tecla K)");
            StartCoroutine(CicloDeDisparo(null));
        }

        // 1. Verifica se está parado
        bool parado = VerdadeiramenteParado();

        // LOG DE DIAGNÓSTICO (A cada 1 segundo)
        if (mostrarLogs && Time.time - timerDebug > 1.0f)
        {
            timerDebug = Time.time;
            string statusAlvo = (alvoAtual != null) ? alvoAtual.name : "Nenhum";
            // Se tiver alvo mas não dispara, pode ser o 'parado' false
            Debug.Log($"[Status Launcher] Parado: {parado} (Vel: {agente.velocity.magnitude:F2}) | Mode Auto: {modoAutomatico} | Em Disparo: {emDisparo} | Alvo Atual: {statusAlvo}");
        }

        // 3. Lógica Automática
        if (parado && !emDisparo)
        {
            if (modoAutomatico)
            {
                // Busca alvo
                alvoAtual = BuscarAlvo();
                if (alvoAtual != null)
                {
                    if(mostrarLogs) Debug.Log("[LancadorMultiplo] Loop: Parado + Auto + Alvo = INICIANDO CICLO!");
                    StartCoroutine(CicloDeDisparo(alvoAtual));
                }
            }
        }
        else if (!parado && estahAberto && !emDisparo)
        {
            StartCoroutine(FecharCompartimento());
        }
    }

    [Header("Debug")]
    public bool mostrarLogs = true; // Ative para ver o que está acontecendo
    public float toleranciaVelocidade = 0.5f; // Aumentei a tolerância para considerar "parado"

    // --- LÓGICA DE COMBATE ---
    
    // --- LÓGICA DE COMBATE ---
    
    bool VerdadeiramenteParado()
    {
        bool p = true;
        if (agente != null && agente.enabled)
        {
            // Aumentei a tolerância para 1.0f para garantir que pequenas flutuações não impeçam o disparo
            p = agente.velocity.magnitude < 1.0f && !agente.pathPending;
        }
        return p;
    }

    Transform BuscarAlvo()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, alcanceRadar);
        float menorDist = Mathf.Infinity;
        Transform melhorAlvo = null;

        foreach (var hit in hits)
        {
            // 1. Ignora a si mesmo ou filhos
            if (hit.transform.root == transform.root) continue;

            // 2. Validação Básica: Tem Tag de Alvo OU tem Vida (SistemaDeDanos)?
            bool tagValida = false;
            foreach(string t in tagsAlvo) { if (hit.CompareTag(t)) tagValida = true; }
            
            // "Alvo de Oportunidade": Se tem script de vida, considera alvo mesmo sem tag,
            // desde que passe no teste de IFF (time).
            bool temVida = hit.GetComponent<SistemaDeDanos>() != null || hit.GetComponentInParent<SistemaDeDanos>() != null;

            if (!tagValida && !temVida) continue; 

            // 3. IFF (Identificação Amigo/Inimigo)
            bool ehInimigo = true; // Assume inimigo por padrão
            
            if (meuID != null)
            {
                // Tenta pegar ID no colisor ou no pai
                IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
                if(idAlvo == null) idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();

                if (idAlvo != null)
                {
                    if (idAlvo.teamID == meuID.teamID) ehInimigo = false; // Mesmo time
                }
            }

            if (ehInimigo)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < menorDist)
                {
                    menorDist = d;
                    melhorAlvo = hit.transform;
                }
            }
        }
        
        if(melhorAlvo != null && mostrarLogs && Time.time - timerDebug > 2.0f)
        {
             Debug.Log($"[Radar] Alvo Travado: {melhorAlvo.name} (Dist: {menorDist:F1}m)");
        }
        
        return melhorAlvo;
    }

    IEnumerator CicloDeDisparo(Transform alvo)
    {
        if (emDisparo) yield break; 
        emDisparo = true;
        
        Debug.LogWarning("[LancadorMultiplo] === INICIOU CICLO DE DISPARO ===");

        // DIAGNÓSTICO CRÍTICO
        if (misselPrefab == null)
        {
            Debug.LogError("[LancadorMultiplo] ERRO CRÍTICO: misselPrefab está NULL! Arraste o prefab do míssil no Inspector!");
            emDisparo = false;
            yield break;
        }

        if (saidasDeMissel == null || saidasDeMissel.Length == 0)
        {
            Debug.LogError("[LancadorMultiplo] ERRO CRÍTICO: Nenhuma saída de míssil configurada!");
            emDisparo = false;
            yield break;
        }

        Debug.Log($"[LancadorMultiplo] Prefab OK: {misselPrefab.name} | Saídas: {saidasDeMissel.Length}");

        // MODIFICAÇÃO: Animação desativada para resposta imediata conforme solicitado.
        // O compartimento é considerado "Sempre Pronto".
        /*
        // 1. ABRIR (Se necessário)
        if (!estahAberto)
        {
            if(mostrarLogs) Debug.Log("[LancadorMultiplo] Abrindo...");
            yield return StartCoroutine(MoverCompartimento(anguloAberto));
            estahAberto = true;
        }

        // 2. TEMPO DE PREPARAÇÃO
        if(mostrarLogs) Debug.Log($"[LancadorMultiplo] Preparando ({tempoPreparacao}s)...");
        yield return new WaitForSeconds(tempoPreparacao);
        */

        // 3. DISPARAR
        Debug.Log($"[LancadorMultiplo] Disparando {saidasDeMissel.Length} mísseis...");
        for (int i = 0; i < saidasDeMissel.Length; i++)
        {
            if (saidasDeMissel[i] != null)
            {
                Debug.Log($"[LancadorMultiplo] Lançando míssil {i+1}/{saidasDeMissel.Length} de {saidasDeMissel[i].name}");
                LancarMissel(saidasDeMissel[i], alvo);
                yield return new WaitForSeconds(intervaloDisparo);
            }
            else
            {
                Debug.LogWarning($"[LancadorMultiplo] Saída {i} está NULL!");
            }
        }

        // Não fechamos automaticamente para manter "Sempre Pronto",
        // ou podemos fechar se o update pedir. Por enquanto, mantém aberto.
        
        emDisparo = false;
        if(mostrarLogs) Debug.Log("[LancadorMultiplo] === FIM CICLO ===");
    }

    IEnumerator FecharCompartimento()
    {
        if (compartimento == null) yield break;
        yield return StartCoroutine(MoverCompartimento(anguloOriginalX));
        estahAberto = false;
    }

    IEnumerator MoverCompartimento(float anguloDestino)
    {
        if (compartimento == null) yield break;

        float anguloAtual = compartimento.localEulerAngles.x;
        // Ajuste para lidar com 0..360 do Unity
        if (anguloAtual > 180) anguloAtual -= 360;

        while (Mathf.Abs(anguloAtual - anguloDestino) > 1f)
        {
            anguloAtual = Mathf.MoveTowards(anguloAtual, anguloDestino, velocidadeAbrir * Time.deltaTime);
            compartimento.localEulerAngles = new Vector3(anguloAtual, 0, 0);
            yield return null;
        }
        
        // Garante valor final exato
        compartimento.localEulerAngles = new Vector3(anguloDestino, 0, 0);
    }

    void LancarMissel(Transform pontoSaida, Transform alvo)
    {
        if (misselPrefab == null) 
        {
            Debug.LogError("[LancadorMultiplo] misselPrefab é NULL em LancarMissel!");
            return;
        }

        Debug.Log($"[LancadorMultiplo] Instanciando {misselPrefab.name} em {pontoSaida.position}");
        GameObject missel = Instantiate(misselPrefab, pontoSaida.position, pontoSaida.rotation);
        
        if (missel == null)
        {
            Debug.LogError("[LancadorMultiplo] Falha ao instanciar o míssil!");
            return;
        }

        // Tenta achar script de míssil (ICBM, Tático ou genérico)
        MisselICBM scriptM = missel.GetComponent<MisselICBM>();
        MisselTatico scriptT = missel.GetComponent<MisselTatico>();
        
        // CONFIGURAÇÃO DO ALVO NO MÍSSEL
        if (scriptM != null)
        {
            Debug.Log("[LancadorMultiplo] Míssil tipo ICBM detectado");
            Vector3 destino = (alvo != null) ? alvo.position : (transform.position + transform.forward * 100f);
            scriptM.IniciarLancamento(destino);
        }
        else if (scriptT != null)
        {
            Debug.Log("[LancadorMultiplo] Míssil tipo Tático detectado");
            Vector3 destino = (alvo != null) ? alvo.position : (transform.position + transform.forward * 100f);
            scriptT.IniciarLancamento(destino);
        }
        else
        {
            Debug.LogWarning("[LancadorMultiplo] Míssil não tem script ICBM nem Tático, tentando Projetil genérico");
            // Tenta achar movimentação genérica do ControleTorreta
            var projetil = missel.GetComponent<Projetil>();
            if (projetil != null)
            {
                 Debug.Log("[LancadorMultiplo] Usando script Projetil genérico");
                 projetil.SetDono(this.gameObject);
                 if (alvo != null) projetil.SetDirecao((alvo.position - pontoSaida.position).normalized);
            }
            else
            {
                Debug.LogError("[LancadorMultiplo] ERRO: Míssil não tem nenhum script de controle (ICBM, Tático ou Projetil)!");
            }
        }
    }

    // --- VISUAL (Gizmos & Feedback) ---
    void UpdateVisualFeedback()
    {
        if (controle != null && controle.anelSelecao != null)
        {
            Renderer r = controle.anelSelecao.GetComponent<Renderer>();
            if (r != null)
            {
                // Vermelho = Ativo (Automático), Verde = Passivo (Manual/Seguro)
                Color alvoAlvo = modoAutomatico ? Color.red : Color.green;
                r.material.color = Color.Lerp(r.material.color, alvoAlvo, Time.deltaTime * 5f);
            }
        }
        
        // Atualiza o círculo do alcance
        AtualizarLinhaAlcance();
    }

    void LateUpdate()
    {
        UpdateVisualFeedback();
    }

    void OnDrawGizmos()
    {
        // "se ficar vermelho ta ativo"
        if (modoAutomatico) 
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, alcanceRadar);
        }
        else
        {
            Gizmos.color = Color.green; // Passivo
             Gizmos.DrawWireSphere(transform.position, alcanceRadar * 0.5f);
        }
    }

    // Método para UI chamar
    public void AlternarModo()
    {
        modoAutomatico = !modoAutomatico;
    }

    // Método para disparo manual via Comando
    public void DispararManual()
    {
        // Só dispara se estiver parado (regra original) ou podemos forçar
        // O user disse "o compartimento abre... quando ele tiver parado".
        // Vamos respeitar a regra de estar parado.
        if (VerdadeiramenteParado())
        {
            StartCoroutine(CicloDeDisparo(null)); // Null = Dispara para frente
        }
        else
        {
            Debug.Log("Veículo precisa estar parado para lançar!");
        }
    }
}
