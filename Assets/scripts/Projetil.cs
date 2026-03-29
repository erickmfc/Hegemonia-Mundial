using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Classe de Projétil genérico (Bala, Canhão, etc.).
/// Usada por SistemaDeTiro, ControleTorreta, Torreta, ModuloArma, 
/// LancadorSimples e LancadorMultiplo.
/// 
/// Move-se em linha reta na direção definida, e ao colidir aplica dano 
/// via SistemaDeDanos no alvo.
/// </summary>
public class Projetil : MonoBehaviour
{
    [Header("Balística")]
    [Tooltip("Velocidade do projétil em metros por segundo.")]
    public float velocidade = 60f;

    [Tooltip("Dano causado ao atingir o alvo.")]
    public int dano = 10;

    [Tooltip("Tempo de vida em segundos antes de se auto-destruir.")]
    public float tempoDeVida = 5f;

    [Header("Dano em Área (AoE)")]
    [Tooltip("Raio de explosão. Se maior que 0, causa dano em área. Se 0, causa apenas dano direto no alvo atingido.")]
    public float raioDeExplosao = 0f;

    [Tooltip("Dano da explosão. Se 0, usa o mesmo valor do Dano direto.")]
    public int danoExplosao = 0;

    [Header("Efeitos de Impacto")]
    [Tooltip("Prefab de efeito visual no impacto (opcional).")]
    public GameObject efeitoImpacto;

    // --- Internos e Míssil Guiado ---
    private GameObject dono;           // Quem atirou (para não se auto-atingir)
    private Vector3 direcaoCustom;     // Direção de voo definida externamente
    private bool temDirecaoCustom = false;
    private bool jaAcertou = false;    // Evita dano duplo se colidir com múltiplos colliders no mesmo frame
    
    // Homing (Perseguição)
    public float curvaDePerseguicao = 0f; // Se maior que zero, é míssil teleguiado (Graus/Seg)
    public float distanciaDetonacaoProximidade = 6f;
    private Transform alvoPerseguido;

    // Zero-alloc Raycasting buffer
    private static RaycastHit[] bufferTiros = new RaycastHit[10];

    void Start()
    {
        // Garante que qualquer som do projétil (ex: assobio, motor) seja 3D
        AudioSource[] audios = GetComponentsInChildren<AudioSource>();
        foreach (AudioSource a in audios)
        {
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 10f;
            a.maxDistance = 300f;
        }

        // Auto-destrói depois de X segundos para não poluir a cena
        Destroy(gameObject, tempoDeVida);
    }

    /// <summary>
    /// Define quem este projétil deve perseguir (Míssil Teleguiado)
    /// </summary>
    public void SetAlvo(Transform alvo)
    {
        alvoPerseguido = alvo;
    }

    void Update()
    {
        // --- HOMING (MÍSSIL TELEGUIADO) ---
        if (alvoPerseguido != null && curvaDePerseguicao > 0f)
        {
            if (alvoPerseguido.gameObject.activeInHierarchy)
            {
                Vector3 pontoAlvo = alvoPerseguido.position + Vector3.up * 1f; // Aponta um pouco acima do centro da base
                Vector3 vetorParaAlvo = pontoAlvo - transform.position;
                float distanciaAlvo = vetorParaAlvo.magnitude;
                float fusivelProximidade = Mathf.Max(distanciaDetonacaoProximidade, velocidade * Time.deltaTime * 1.5f);

                if (distanciaAlvo <= fusivelProximidade)
                {
                    ProcessarImpacto(alvoPerseguido.gameObject);
                    return;
                }

                if (vetorParaAlvo.sqrMagnitude <= 0.0001f)
                {
                    ProcessarImpacto(alvoPerseguido.gameObject);
                    return;
                }

                Vector3 direcaoIdeal = vetorParaAlvo / distanciaAlvo;
                
                Vector3 direcaoAtual = (temDirecaoCustom && direcaoCustom.sqrMagnitude > 0.0001f) ? direcaoCustom : transform.forward;
                Vector3 novaDirecao = Vector3.RotateTowards(direcaoAtual, direcaoIdeal, curvaDePerseguicao * Mathf.Deg2Rad * Time.deltaTime, 0f);
                
                if (novaDirecao.sqrMagnitude > 0.0001f)
                {
                    SetDirecao(novaDirecao); // Atualiza a direção real do voo
                }
            }
            else
            {
                alvoPerseguido = null; // Perde o alvo se ele sumir
            }
        }

        // Movimento neste frame
        float passo = velocidade * Time.deltaTime;
        Vector3 direcao = temDirecaoCustom ? direcaoCustom : transform.forward;
        
        // --- DETECÇÃO CONTÍNUA OTIMIZADA (Zero Allocation) ---
        // Lança um raio da posição atual até onde a bala vai estar no próximo frame usando buffer
        int numColisoes = Physics.RaycastNonAlloc(transform.position, direcao, bufferTiros, passo);
        
        float distMaisPerto = Mathf.Infinity;
        RaycastHit hitMaisPerto = new RaycastHit();
        bool bateuEmAlgoValido = false;

        for (int i = 0; i < numColisoes; i++)
        {
            var hit = bufferTiros[i];

            if (hit.collider.isTrigger) continue;
            
            if (hit.collider.gameObject == gameObject) continue;
            
            if (dono != null && (hit.collider.gameObject == dono || hit.transform.IsChildOf(dono.transform))) continue;

            // Se for válido e for mais perto do que o anterior
            if (hit.distance < distMaisPerto)
            {
                distMaisPerto = hit.distance;
                hitMaisPerto = hit;
                bateuEmAlgoValido = true;
            }
        }

        if (bateuEmAlgoValido)
        {
            // Achou algo válido!
            transform.position = hitMaisPerto.point;
            ProcessarImpacto(hitMaisPerto.collider.gameObject); 
            
            // Limpa o buffer manual para este míssil
            for(int k=0; k < numColisoes; k++) bufferTiros[k] = new RaycastHit(); 
            return; // Encerra o frame e a vida do projétil
        }

        // Se não bateu em nada, move normalmente
        transform.position += direcao * passo;
        
        // Limpa o buffer manual após o processamento
        for(int k=0; k < numColisoes; k++) bufferTiros[k] = new RaycastHit(); 
    }

    /// <summary>
    /// Define quem disparou este projétil (para não causar dano no próprio atirador).
    /// </summary>
    public void SetDono(GameObject quemAtirou)
    {
        dono = quemAtirou;
    }

    /// <summary>
    /// Define a direção de voo do projétil.
    /// </summary>
    public void SetDirecao(Vector3 dir)
    {
        direcaoCustom = dir.normalized;
        temDirecaoCustom = true;

        if (direcaoCustom != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direcaoCustom);
        }
    }

    // Mantemos os triggers como backup caso a bala nasça já dentro de algo
    void OnTriggerEnter(Collider other)
    {
        ProcessarImpacto(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        ProcessarImpacto(collision.gameObject);
    }

    void ProcessarImpacto(GameObject alvo)
    {
        if (jaAcertou) return;
        
        // Ignora colisões nulas
        if (alvo == null) return;

        // Ignora o dono (quem atirou) e seus filhos
        if (dono != null && (alvo == dono || alvo.transform.IsChildOf(dono.transform)))
            return;
            
        // Ignora a si mesmo (caso tenha colliders compostos)
        if (alvo == gameObject) return;

        // Ignora outros projéteis para não explodirem no ar, a menos que seja nosso alvo (interceptor anti-míssil)
        if (alvo.GetComponent<Projetil>() != null && (alvoPerseguido == null || alvo != alvoPerseguido.gameObject)) return;

        jaAcertou = true;

        // --- 1. Dano Direto ---
        SistemaDeDanos vidaDireta = alvo.GetComponent<SistemaDeDanos>();
        if (vidaDireta == null) vidaDireta = alvo.GetComponentInParent<SistemaDeDanos>();

        if (vidaDireta != null)
        {
            vidaDireta.ReceberDano(dano);
            // Debug.Log($"[Projetil] 🎯 ACERTOU {alvo.name}! Dano: {dano}. Vida Restante: {vidaDireta.vidaAtual}");
        }
        else
        {
             // Debug.Log($"[Projetil] Bateu em {alvo.name} (sem vida).");
        }

        // --- 2. Dano em Área (Se configurado) ---
        if (raioDeExplosao > 0f)
        {
            Explodir(vidaDireta);

            // Toca efeito de explosão global se disponível (Garante visual mesmo sem prefab local)
            if (GerenciadorFXGlobal.Instancia != null)
            {
                // Escala visual baseada no raio (ajuste o multiplicador conforme gosto, 0.8f é um bom chute)
                float escalaVisual = Mathf.Max(1.0f, raioDeExplosao * 0.8f);
                GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, escalaVisual);
            }
        }

        // Efeito de impacto específico deste projétil (opcional, se configurado no Inspector)
        if (efeitoImpacto != null)
        {
            GameObject fx = Instantiate(efeitoImpacto, transform.position, Quaternion.identity);
            
            // Garante que o som de impacto também fique 3D
            AudioSource[] audiosImpacto = fx.GetComponentsInChildren<AudioSource>();
            foreach (AudioSource a in audiosImpacto)
            {
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 20f;
                a.maxDistance = 600f;
            }

            // Se for explosão, escala o efeito também
            if (raioDeExplosao > 0f)
            {
                 fx.transform.localScale = Vector3.one * Mathf.Max(1.0f, raioDeExplosao * 0.8f);
            }

            Destroy(fx, 2f);
        }

        // Destrói o projétil imediatamente
        Destroy(gameObject);
    }

    void Explodir(SistemaDeDanos alvoDiretoIgnorar)
    {
        int danoReal = (danoExplosao > 0) ? danoExplosao : dano;
        Collider[] atingidos = Physics.OverlapSphere(transform.position, raioDeExplosao);
        
        // HashSet para garantir que cada entidade receba dano apenas uma vez (mesmo tendo vários colliders)
        HashSet<SistemaDeDanos> unicosAtingidos = new HashSet<SistemaDeDanos>();

        // Se já demos dano direto, adiciona à lista de 'já atingidos' para não tomar dano dobrado
        if (alvoDiretoIgnorar != null)
        {
            unicosAtingidos.Add(alvoDiretoIgnorar);
        }

        foreach (Collider hit in atingidos)
        {
            if (hit == null || hit.isTrigger) continue;

            GameObject obj = hit.gameObject;

            // Segurança: Ignora a bala e o dono
            if (obj == gameObject) continue;
            if (dono != null && (obj == dono || obj.transform.IsChildOf(dono.transform))) continue;

            SistemaDeDanos vida = obj.GetComponent<SistemaDeDanos>();
            if (vida == null) vida = obj.GetComponentInParent<SistemaDeDanos>();

            if (vida != null && !unicosAtingidos.Contains(vida))
            {
                vida.ReceberDano(danoReal);
                unicosAtingidos.Add(vida);
            }
        }
    }
}
