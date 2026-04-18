using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de torreta modular que suporta múltiplas armas simultaneamente.
/// Cada arma pode ter munição, cadência e comportamento diferentes.
/// </summary>
public class ControleTorretaModular : MonoBehaviour
{
    [Header("📡 Sistema de Radar")]
    [Tooltip("Tag dos alvos que esta torreta vai procurar")]
    public string etiquetaAlvo = "Aereo";
    
    [Tooltip("Alcance máximo do radar (metros)")]
    public float alcanceRadar = 120f;
    
    [Header("🔧 Mecânica de Rotação")]
    [Tooltip("Peça que gira para mirar (geralmente a torre/barril)")]
    public Transform pecaQueGira;
    
    [Tooltip("Velocidade de rotação (graus/segundo)")]
    public float velocidadeGiro = 60f;
    
    [Header("🔒 Limites de Rotação")]
    public bool limitarRotacao = true;
    [Range(-180, 180)] public float anguloMinimo = -90f;
    [Range(-180, 180)] public float anguloMaximo = 90f;
    
    [Header("⚙️ Comportamento")]
    [Tooltip("Se ativo, torreta não ataca")]
    public bool modoPassivo = false;
    
    [Header("🔫 Armamento (Múltiplas Armas)")]
    [Tooltip("Lista de armas instaladas nesta torreta")]
    public List<ModuloArma> armas = new List<ModuloArma>();
    
    [Header("🎯 Sistema de Priorização")]
    [Tooltip("Qual arma usar primeiro")]
    public PrioridadeArma prioridade = PrioridadeArma.PorOrdem;
    
    public enum PrioridadeArma
    {
        PorOrdem,          // Usa na ordem da lista
        MaisRapida,        // Usa a que tem menor cooldown
        MaisDano,          // Usa a que causa mais dano
        MaisAlcance,       // Usa a de maior alcance
        Alternada          // Alterna entre todas igualmente
    }
    
    [Header("🎨 Efeitos Globais")]
    public AudioClip somRecargaPadrao;
    private AudioSource fonteAudio;
    
    // ===== VARIÁVEIS INTERNAS =====
    private Transform alvoAtual;
    private Collider[] bufferColisores = new Collider[40];
    private int indiceArmaAlternada = 0;
    private int meuTime = 1;
    private Transform minhaRaiz;
    private bool souAntiAereo;
    
    void Start()
    {
        // Inicializa audio
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1f;
        
        // Inicializa armas
        foreach (var arma in armas)
        {
            arma.Inicializar();
        }
        
        // Garante referência da torre
        IdentidadeUnidade meuID = GetComponentInParent<IdentidadeUnidade>();
        meuTime = (meuID != null) ? meuID.teamID : 1;
        minhaRaiz = transform.root;
        if (pecaQueGira == null) pecaQueGira = transform;
        souAntiAereo = DeterminarSouAntiAereo();
        
        // Inicia busca de alvos
        float offset = Random.Range(0f, 0.5f);
        InvokeRepeating("ProcurarAlvo", offset, 0.4f);
    }

    bool DeterminarSouAntiAereo()
    {
        string nomeBase = minhaRaiz != null ? minhaRaiz.name.ToLowerInvariant() : transform.root.name.ToLowerInvariant();
        string nomeObj = transform.name.ToLowerInvariant();
        string etiqueta = etiquetaAlvo ?? string.Empty;

        return etiqueta.Equals("Aereo", System.StringComparison.OrdinalIgnoreCase) ||
               etiqueta.Equals("Areo", System.StringComparison.OrdinalIgnoreCase) ||
               nomeBase.Contains("ares") || nomeBase.Contains("antiaerea") || nomeBase.Contains("anti-aerea") ||
               nomeBase.Contains("ciws") || nomeBase.Contains("sam") ||
               nomeObj.Contains("ares") || nomeObj.Contains("antiaerea") || nomeObj.Contains("anti-aerea") ||
               nomeObj.Contains("ciws") || nomeObj.Contains("sam");
    }

    Transform ResolverTransformAlvo(Transform alvo)
    {
        if (alvo == null) return null;

        SistemaDeDanos vida = alvo.GetComponentInParent<SistemaDeDanos>();
        if (vida != null) return vida.transform;

        ControleAviao aviao = alvo.GetComponentInParent<ControleAviao>();
        if (aviao != null) return aviao.transform;

        Helicoptero helicoptero = alvo.GetComponentInParent<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) return identidade.transform;

        return alvo.root != null ? alvo.root : alvo;
    }

    bool EhMissilReal(Transform alvo)
    {
        if (alvo == null) return false;
        string tagAtual = alvo.gameObject.tag;

        if (alvo.GetComponentInParent<MissileThreatTracker>() != null) return true;
        if (alvo.GetComponentInParent<MisselCaca>() != null) return true;
        if (alvo.GetComponentInParent<MissilTeleguiado>() != null) return true;
        if (alvo.GetComponentInParent<MisselICBM>() != null) return true;
        if (alvo.GetComponentInParent<MisselNaval>() != null) return true;
        if (alvo.GetComponentInParent<MisselSubmarino>() != null) return true;
        if (alvo.GetComponentInParent<MisselTatico>() != null) return true;
        if (alvo.GetComponentInParent<MisselLeopardAutomatico>() != null) return true;

        return tagAtual == "Missil" || tagAtual == "Missel";
    }

    bool EhAlvoAereo(Transform alvo, IdentidadeUnidade identidade)
    {
        if (alvo == null) return false;

        string nomeAlvo = alvo.name.ToLowerInvariant();

        return alvo.position.y > 6f ||
               alvo.GetComponentInParent<ControleAviao>() != null ||
               alvo.GetComponentInParent<ControleAviaoCaca>() != null ||
               alvo.GetComponentInParent<AviaoBombardeiro>() != null ||
               alvo.GetComponentInParent<Helicoptero>() != null ||
               (identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo) ||
               nomeAlvo.Contains("aviao") ||
               nomeAlvo.Contains("heli") ||
               nomeAlvo.Contains("caca") ||
               nomeAlvo.Contains("jato") ||
               nomeAlvo.Contains("drone") ||
               nomeAlvo.Contains("vap") ||
               nomeAlvo.Contains("bombard") ||
               nomeAlvo.Contains("bombardeiro") ||
               nomeAlvo.Contains("bomber") ||
               alvo.tag == "Areo" ||
               alvo.tag == "Aereo";
    }
    
    void Update()
    {
        // Atualiza cooldowns de todas as armas
        foreach (var arma in armas)
        {
            arma.AtualizarCooldowns(Time.deltaTime);
        }
        
        if (alvoAtual != null)
        {
            if (!alvoAtual.gameObject.activeInHierarchy || !ControleSubmarino.PodeSerAlvoConvencional(alvoAtual))
            {
                alvoAtual = null;
                return;
            }

            // Mira no alvo
            RotacionarParaAlvo();
            
            // Tenta disparar com as armas disponíveis
            TentarDisparar();
        }
        else
        {
            // Modo ocioso
            ModoOcioso();
        }
    }
    
    void ProcurarAlvo()
    {
        if (modoPassivo)
        {
            alvoAtual = null;
            return;
        }
        
        int quantidadeEncontrada = Physics.OverlapSphereNonAlloc(transform.position, alcanceRadar, bufferColisores, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;
        
        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null) continue;
            
            Transform alvoTr = hit.transform;
            if (alvoTr.root == minhaRaiz) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(alvoTr)) continue;
            
            bool ehInimigo = false;
            
            // Tenta por identidade
            IdentidadeUnidade idAlvo = alvoTr.GetComponentInParent<IdentidadeUnidade>();
            if (idAlvo != null)
            {
                if (idAlvo.teamID != meuTime && idAlvo.teamID != 0)
                {
                    ehInimigo = true;
                }
            }
            // Fallback por tag
            else if (TagSafe.Matches(hit, etiquetaAlvo) || TagSafe.Matches(hit, "Inimigo"))
            {
                ehInimigo = true;
            }
            
            if (ehInimigo)
            {
                Transform alvoPrincipal = ResolverTransformAlvo(alvoTr);
                IdentidadeUnidade idAlvoPrincipal = alvoPrincipal != null
                    ? alvoPrincipal.GetComponentInParent<IdentidadeUnidade>()
                    : idAlvo;
                bool ehMissil = EhMissilReal(alvoPrincipal != null ? alvoPrincipal : alvoTr);
                bool alvoAereo = ehMissil || EhAlvoAereo(alvoPrincipal != null ? alvoPrincipal : alvoTr, idAlvoPrincipal);

                if (souAntiAereo)
                {
                    if (!alvoAereo) continue;
                }
                else
                {
                    if (alvoAereo) continue;
                }

                Vector3 pontoMaisProximo = hit.ClosestPoint(transform.position);
                float dist = (transform.position - pontoMaisProximo).sqrMagnitude;
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = alvoPrincipal != null ? alvoPrincipal : alvoTr;
                }
            }
        }
        
        // Limpa buffer
        for (int i = 0; i < quantidadeEncontrada; i++) bufferColisores[i] = null;
        
        alvoAtual = melhorAlvo;
    }
    
    void RotacionarParaAlvo()
    {
        if (pecaQueGira == null || alvoAtual == null) return;
        
        Vector3 direcao = alvoAtual.position - pecaQueGira.position;
        
        if (limitarRotacao && pecaQueGira.parent != null)
        {
            // Lógica local com limites
            Vector3 localDir = pecaQueGira.parent.InverseTransformDirection(direcao);
            float anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float anguloTravado = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);
            
            Quaternion rotacaoAlvo = Quaternion.Euler(0, anguloTravado, 0);
            pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
        }
        else
        {
            // Rotação livre
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
            rotacaoAlvo = Quaternion.Euler(0, rotacaoAlvo.eulerAngles.y, 0);
            pecaQueGira.rotation = Quaternion.Lerp(pecaQueGira.rotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
        }
    }
    
    void TentarDisparar()
    {
        if (alvoAtual == null) return;
        
        // Analisa o ângulo de visão no plano 2D (ignora a altura)
        Vector3 dirAlvo = (alvoAtual.position - pecaQueGira.position);
        dirAlvo.y = 0;
        Vector3 minhaFrente = pecaQueGira.forward;
        minhaFrente.y = 0;

        if (Vector3.Angle(minhaFrente, dirAlvo) > 8f) return;
        
        // Seleciona arma baseado na prioridade
        ModuloArma armaEscolhida = SelecionarArma();
        
        if (armaEscolhida != null && armaEscolhida.PodeAtirar())
        {
            // Verifica alcance específico da arma
            float distAlvo = Vector3.Distance(transform.position, alvoAtual.position);
            float alcanceArma = armaEscolhida.alcanceMaximo > 0 ? armaEscolhida.alcanceMaximo : alcanceRadar;
            
            if (distAlvo <= alcanceArma)
            {
                armaEscolhida.Disparar(pecaQueGira, alvoAtual, transform.root.gameObject, fonteAudio);
                Debug.Log($"💥 {armaEscolhida.nomeArma} disparada! ({armaEscolhida.municaoAtual}/{armaEscolhida.tamanhoCartucho})");
            }
        }
    }
    
    ModuloArma SelecionarArma()
    {
        if (armas.Count == 0) return null;
        
        switch (prioridade)
        {
            case PrioridadeArma.PorOrdem:
                // Retorna a primeira arma disponível
                foreach (var arma in armas)
                {
                    if (arma.PodeAtirar()) return arma;
                }
                return null;
            
            case PrioridadeArma.MaisRapida:
                ModuloArma maisRapida = null;
                float menorCooldown = Mathf.Infinity;
                foreach (var arma in armas)
                {
                    if (arma.PodeAtirar() && arma.intervaloTiro < menorCooldown)
                    {
                        menorCooldown = arma.intervaloTiro;
                        maisRapida = arma;
                    }
                }
                return maisRapida;
            
            case PrioridadeArma.MaisDano:
                ModuloArma maisDano = null;
                float maiorDano = 0f;
                foreach (var arma in armas)
                {
                    if (arma.PodeAtirar() && arma.danoBase > maiorDano)
                    {
                        maiorDano = arma.danoBase;
                        maisDano = arma;
                    }
                }
                return maisDano;
            
            case PrioridadeArma.MaisAlcance:
                ModuloArma maisAlcance = null;
                float maiorAlcance = 0f;
                foreach (var arma in armas)
                {
                    if (arma.PodeAtirar() && arma.alcanceMaximo > maiorAlcance)
                    {
                        maiorAlcance = arma.alcanceMaximo;
                        maisAlcance = arma;
                    }
                }
                return maisAlcance;
            
            case PrioridadeArma.Alternada:
                // Alterna entre todas as armas disponíveis
                int tentativas = 0;
                while (tentativas < armas.Count)
                {
                    ModuloArma arma = armas[indiceArmaAlternada];
                    indiceArmaAlternada = (indiceArmaAlternada + 1) % armas.Count;
                    
                    if (arma.PodeAtirar()) return arma;
                    tentativas++;
                }
                return null;
            
            default:
                return armas[0];
        }
    }
    
    void ModoOcioso()
    {
        if (pecaQueGira == null) return;
        
        if (limitarRotacao)
        {
            // Volta para o centro
            pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, Quaternion.identity, Time.deltaTime * 2f);
        }
        else
        {
            // Gira como radar
            pecaQueGira.Rotate(0, 10f * Time.deltaTime, 0);
        }
    }
    
    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo;
        if (modoPassivo) alvoAtual = null;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);
        
        // Desenha alcance de cada arma
        if (armas != null)
        {
            Color[] cores = { Color.red, Color.blue, Color.green, Color.magenta, Color.cyan };
            for (int i = 0; i < armas.Count; i++)
            {
                if (armas[i].alcanceMaximo > 0)
                {
                    Gizmos.color = cores[i % cores.Length];
                    Gizmos.DrawWireSphere(transform.position, armas[i].alcanceMaximo);
                }
            }
        }
    }
}
