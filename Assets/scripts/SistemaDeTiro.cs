using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SistemaDeTiro : MonoBehaviour
{
    [Header("Comportamento IA")]
    public bool modoPassivo = false; // Se true, só ataca se mandarem
    public string etiquetaAlvo = "Inimigo"; 
    public float intervaloEntreTiros = 0.5f;
    private float tempoParaProximoTiro = 0f;
    public Transform alvoAtual;

    [Header("Configuração de Munição")]
    public GameObject prefabProjetil; // A munição
    public int capacidadePente = 30;  // Quantidade de balas
    public float tempoRecarga = 2.0f; // Tempo para trocar o pente
    private int balasAtuais;
    private bool recarregando = false;

    [Header("Configuração de Alcance e Balística")]
    public float alcanceTiro = 50f;   // Distância máxima (Visual e Lógica)
    
    [Tooltip("Controla a velocidade visual da bala (Metros por segundo).")]
    public float velocidadeDoTiro = 60f; // Nova variável pedida
    
    public Transform bocaDoCano;      // O ponto de saída
    
    [Tooltip("Força física aplicada APENAS se a bala tiver Rigidbody (Impacto). Não afeta a velocidade de voo.")]
    public float forcaDoTiro = 1000f; 

    [Header("Áudio")]
    public AudioClip somTiro; 
    public AudioClip somRecarga; 
    public AudioClip somSemMuni; 
    private AudioSource fonteAudio;

    private ControleUnidade selecao; 
    private IdentidadeUnidade minhaIdentidade;
    private Transform minhaRaiz;
    private bool souSoldadoLeve;
    private float alcanceComMargemSqr;

    // OTIMIZAÇÃO: Buffer de colisão para evitar GC
    private Collider[] bufferColisores = new Collider[50];

    void Update()
    {
        // Se estiver recarregando ou em modo passivo, não faz nada
        if (recarregando || modoPassivo) return;

        // Decrementa o cooldown
        if (tempoParaProximoTiro > 0) tempoParaProximoTiro -= Time.deltaTime;

        // Se temos um alvo válido
        if (alvoAtual != null)
        {
            // Validação de segurança: Alvo foi destruído?
            if (alvoAtual == null || !alvoAtual.gameObject.activeInHierarchy || !ControleSubmarino.PodeSerAlvoConvencional(alvoAtual))
            {
                alvoAtual = null;
                return;
            }

            // Verifica distância (o Scan faz isso, mas o Update é mais rápido para parar de atirar se o alvo fugir)
            Collider colInimigo = alvoAtual.GetComponentInChildren<Collider>();
            Vector3 alvoPosicaoReal = (colInimigo != null) ? colInimigo.ClosestPoint(transform.position) : alvoAtual.position;
            
            float distSqr = (transform.position - alvoPosicaoReal).sqrMagnitude;
            if (distSqr > alcanceComMargemSqr) // Margem de 3 metros pra nao perder o alvo bobamente ao parar
            {
                alvoAtual = null; // Perde o alvo se sair do alcance
                return;
            }

            // Gira em direção ao alvo (Apenas Y para soldados terrestres)
            Vector3 direcao = (alvoAtual.position - transform.position).normalized;
            direcao.y = 0; // Mantém o soldado em pé
            
            float anguloParaAlvo = 180f; // Valor padrão alto para não atirar se direção for zero
            
            if (direcao != Vector3.zero)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                
                // Evita que o soldado gire o veículo inteiro caso esteja embarcado
                Transform objParaGirar = transform.root;
                if (objParaGirar.GetComponent<TransporteTerrestre>() != null) 
                {
                    objParaGirar = transform; // Gira apenas o corpo do atirador montado
                }

                // VELOCIDADE DE ROTAÇÃO: 45 graus/segundo para alinhamento rápido
                objParaGirar.rotation = Quaternion.Slerp(objParaGirar.rotation, rotacaoAlvo, Time.deltaTime * 45f);
                
                // Calcula o ângulo atual
                anguloParaAlvo = Vector3.Angle(objParaGirar.forward, direcao);
            }

            // Lógica de Tiro - SÓ ATIRA SE ESTIVER APONTANDO PRO ALVO
            // Adiciona verificação: o tanque precisa estar virado para o alvo (< 10 graus - mais preciso)
            if (tempoParaProximoTiro <= 0 && anguloParaAlvo < 10f)
            {
                // --- CHECAGEM DE FOGO AMIGO (NOVO) ---
                if (HaAmigoNaLinhaDeTiro())
                {
                    // Se tem amigo na frente, espera um pouco (0.3s) e não atira
                    tempoParaProximoTiro = 0.3f;
                    return;
                }

                if (balasAtuais > 0)
                {
                    Atirar();
                    tempoParaProximoTiro = intervaloEntreTiros;
                }
                else
                {
                    StartCoroutine(RotinaRecarga());
                }
            }
        }
    }

    // Função para evitar atirar nas costas dos aliados
    bool HaAmigoNaLinhaDeTiro()
    {
        if (alvoAtual == null) return false;

        Transform origem = (bocaDoCano != null) ? bocaDoCano : transform;
        Vector3 direcao = (alvoAtual.position - origem.position).normalized;
        float distancia = Vector3.Distance(origem.position, alvoAtual.position);

        RaycastHit hit;
        // Usa SphereCast (raio 0.5m) para ser mais seguro que um Raycast fino
        // Assim detecta se passar "raspando" no amigo
        if (Physics.SphereCast(origem.position, 0.5f, direcao, out hit, distancia))
        {
            // Ignora a si mesmo e ao alvo
            if (hit.transform.root == transform.root) return false;
            if (hit.transform.root == alvoAtual.root) return false;

            // Verifica se o obstáculo é uma unidade
            IdentidadeUnidade idObstaculo = hit.transform.GetComponentInParent<IdentidadeUnidade>();
            
            if (idObstaculo != null && minhaIdentidade != null)
            {
                // Se for do MESMO TIME, bloqueia o tiro
                if (idObstaculo.teamID == minhaIdentidade.teamID)
                {
                    return true; // TEM AMIGO NA FRENTE
                }
            }
        }
        return false; // Caminho limpo
    }

    // CACHE de Componentes
    private SomUnidade somUnidadeCached;
    private AnimacoesSoldado animCached;

    void Start()
    {
        balasAtuais = capacidadePente; 
        selecao = GetComponentInParent<ControleUnidade>();
        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        minhaRaiz = transform.root;
        PoolDeObjetosCombate.Prewarm(prefabProjetil, Mathf.Clamp(capacidadePente > 0 ? 6 : 4, 2, 8));

        // Cache para performance (evita GetComponent a cada tiro)
        somUnidadeCached = GetComponentInParent<SomUnidade>();
        animCached = GetComponentInParent<AnimacoesSoldado>();

        // AUTO-CORREÇÃO: Se eu não tenho Identidade, crio uma como Time 1 (Jogador)
        if (minhaIdentidade == null)
        {
            var root = transform.root.gameObject;
            minhaIdentidade = root.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1; 
            minhaIdentidade.nomeDoPais = "Minha Nação";
        }

        string meuNomeTag = minhaRaiz != null ? minhaRaiz.name.ToLowerInvariant() : name.ToLowerInvariant();
        souSoldadoLeve = meuNomeTag.Contains("soldado") || meuNomeTag.Contains("infantaria") || meuNomeTag.Contains("rifle") || meuNomeTag.Contains("jipe");
        float alcanceComMargem = alcanceTiro + 3f;
        alcanceComMargemSqr = alcanceComMargem * alcanceComMargem;

        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1.0f; 

        // OTIMIZAÇÃO: Scan com intervalo aleatório
        float inicioAleatorio = Random.Range(0f, 1.0f);
        InvokeRepeating("ProcurarAlvo", inicioAleatorio, 0.5f);
    }
    
    void ProcurarAlvo()
    {
        if (modoPassivo) return;

        if (alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
             Collider col = alvoAtual.GetComponentInChildren<Collider>();
             Vector3 alvoPosReal = (col != null) ? col.ClosestPoint(transform.position) : alvoAtual.position;
             float distSqr = (transform.position - alvoPosReal).sqrMagnitude;
             if (distSqr <= alcanceComMargemSqr) return;
        }

        alvoAtual = null; // Reseta para buscar o mais próximo

        // Busca nova lista de alvos potenciais usando buffer para ZERO alocação de memória (GC Free)
        int naviosNaArea = Physics.OverlapSphereNonAlloc(transform.position, alcanceTiro, bufferColisores, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        
        float menorDistancia = Mathf.Infinity;
        Transform candidato = null;

        // Calcula quem eu sou FORA DO LOOP para não gastar processamento (String manipulation pesada)
        string meuNomeTag = transform.root.name.ToLower();
        for (int i = 0; i < naviosNaArea; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null || hit.transform.root == minhaRaiz) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(hit.transform)) continue;

            // Busca IdentidadeUnidade (Componente que define time)
            IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
            if (idAlvo == null) idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();

            if (idAlvo != null && minhaIdentidade != null)
            {
                // Verifica se é inimigo (Time diferente)
                if (idAlvo.teamID != minhaIdentidade.teamID) 
                {
                    // NÃO ATIRA EM ALVOS AÉREOS A NÃO SER QUE SEJA UM SOLDADO
                    // Tenta otimizar checando o eixo Y antes para evitar ler strings atoa
                    bool podeSerAereo = hit.transform.position.y > 6f;
                    
                    bool alvoAereo = podeSerAereo ||
                                     hit.GetComponentInParent<Helicoptero>() != null ||
                                     hit.GetComponentInParent<ControleAviao>() != null ||
                                     hit.name.Contains("Aviao") || 
                                     hit.name.Contains("Heli") ||
                                     hit.name.Contains("caca");

                    // Apenas INFANTARIA (Soldados) ou Jipes com metralhadora muito leves podem tentar atirar com suas mãos em alvos aéreos
                    bool souSoldado = souSoldadoLeve;
                    
                    if (alvoAereo && !souSoldado) continue; // Tanques e caminhões cegos para os céus!

                    // Prioriza o mais próximo usando o transform
                    Vector3 alvoHitCenter = hit.transform.position;
                    float d = (transform.position - alvoHitCenter).sqrMagnitude;
                    
                    if (d < menorDistancia)
                    {
                        menorDistancia = d;
                        candidato = hit.transform;
                    }
                }
            }
        }

        if (candidato != null)
        {
            alvoAtual = candidato;
            // Debug.Log($"[SistemaDeTiro] {name} encontrou alvo: {alvoAtual.name}");
        }

        // --- Limpa o buffer manual para a próxima passada ---
        for (int i = 0; i < naviosNaArea; i++) bufferColisores[i] = null;
    }

    void Atirar()
    {
        // Se a boca do cano não estiver definida, usa a posição do próprio objeto
        Transform origem = (bocaDoCano != null) ? bocaDoCano : transform;

        GameObject bala = PoolDeObjetosCombate.Spawn(prefabProjetil, origem.position, origem.rotation);
        
        // --- CORREÇÃO DE SEGURANÇA ---
        Projetil scriptBala = bala.GetComponent<Projetil>();
        if (scriptBala == null) 
        {
            scriptBala = bala.AddComponent<Projetil>();
        }

        if (scriptBala != null)
        {
            scriptBala.SetDono(transform.root.gameObject);
            scriptBala.velocidade = velocidadeDoTiro;

            if (alvoAtual != null)
            {
                 Vector3 pontoAlvo = alvoAtual.position + Vector3.up * 1.2f; 
                 Vector3 direcaoAlvo = (pontoAlvo - origem.position).normalized;
                 scriptBala.SetDirecao(direcaoAlvo);
            }
            else
            {
                scriptBala.SetDirecao(origem.forward);
            }
        }

        balasAtuais--;
        
        // SISTEMA DE SOM (OTIMIZADO)
        if (somUnidadeCached != null)
        {
            somUnidadeCached.TocarSomTiro();
        }
        else if (fonteAudio != null && somTiro != null)
        {
            fonteAudio.PlayOneShot(somTiro);
        }
        
        // ANIMAÇÃO (OTIMIZADO)
        if(animCached != null) animCached.DefinirAtaque(true);
        
        CancelInvoke("PararAnimacaoTiro");
        Invoke("PararAnimacaoTiro", 0.1f);
    }
    
    void PararAnimacaoTiro()
    {
        if(animCached != null) animCached.DefinirAtaque(false);
    }

    IEnumerator RotinaRecarga()
    {
        recarregando = true;
        if(somSemMuni != null) fonteAudio.PlayOneShot(somSemMuni); // Click seco
        Debug.Log("Recarregando...");
        
        if(somRecarga != null) 
        {
            yield return new WaitForSeconds(0.2f);
            fonteAudio.PlayOneShot(somRecarga);
        }
        
        yield return new WaitForSeconds(tempoRecarga);

        balasAtuais = capacidadePente;
        recarregando = false;
    }

    public void DefinirModoPassivo(bool estado)
    {
        modoPassivo = estado;
        if (modoPassivo)
        {
            alvoAtual = null; // Para de mirar imediatamente
            // Opcional: Cancelar recarga se quiser ser muito estrito, mas deixar recarregar é bom.
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f); 
        Gizmos.DrawWireSphere(transform.position, alcanceTiro);
    }
}
