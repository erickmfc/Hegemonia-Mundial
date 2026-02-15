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

    // --- Internos ---
    private GameObject dono;           // Quem atirou (para não se auto-atingir)
    private Vector3 direcaoCustom;     // Direção de voo definida externamente
    private bool temDirecaoCustom = false;
    private bool jaAcertou = false;    // Evita dano duplo se colidir com múltiplos colliders no mesmo frame

    void Start()
    {
        // Auto-destrói depois de X segundos para não poluir a cena
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        // Movimento neste frame
        float passo = velocidade * Time.deltaTime;
        Vector3 direcao = temDirecaoCustom ? direcaoCustom : transform.forward;
        
        // --- DETECÇÃO CONTÍNUA (Anti-Tunneling) ---
        // Lança um raio da posição atual até onde a bala vai estar no próximo frame
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direcao, passo);
        
        // Ordena por distância para processar do mais perto para o mais longe
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool bateuEmAlgoValido = false;

        foreach (var hit in hits)
        {
            // Ignora colisores que são triggers (se desejado, geralmente projéteis ignoram triggers de zona)
            if (hit.collider.isTrigger) continue;

            // Se bateu no dono ou em filho do dono, ignora e continua o raio
            if (dono != null && (hit.collider.gameObject == dono || hit.transform.IsChildOf(dono.transform)))
                continue;

            // Se bateu em si mesmo
            if (hit.collider.gameObject == gameObject) continue;

            // Achou algo válido!
            transform.position = hit.point;
            ProcessarImpacto(hit.collider.gameObject); 
            bateuEmAlgoValido = true;
            return; // Encerra o frame e a vida do projétil
        }

        // Se não bateu em nada válido, move normalmente
        if (!bateuEmAlgoValido)
        {
            transform.position += direcao * passo;
        }
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

        // Ignora outros projéteis para não explodirem no ar
        if (alvo.GetComponent<Projetil>() != null) return;

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
