using UnityEngine;

/// <summary>
/// Módulo de arma individual que pode ser anexado a uma torreta.
/// Suporta diferentes tipos de munição, cadência, alcance e comportamentos.
/// </summary>
[System.Serializable]
public class ModuloArma
{
    [Header("Identificação")]
    public string nomeArma = "Canhão Padrão";
    
    [Header("Munição")]
    public GameObject municaoPrefab;
    public Transform[] pontosDisparo; // Barris desta arma específica
    
    [Header("Balística")]
    [Tooltip("Velocidade do projétil (m/s)")]
    public float velocidadeProjetil = 200f;
    
    [Tooltip("Dano base do projétil")]
    public float danoBase = 10f;
    
    [Header("Cadência")]
    [Tooltip("Tempo entre disparos (segundos)")]
    public float intervaloTiro = 0.1f;
    
    [Tooltip("Número de tiros antes de recarregar (0 = infinito)")]
    public int tamanhoCartucho = 50;
    
    [Tooltip("Tempo de recarga (segundos)")]
    public float tempoRecarga = 2.0f;
    
    [Header("Alcance e Precisão")]
    [Tooltip("Alcance máximo efetivo (0 = usa alcance da torreta)")]
    public float alcanceMaximo = 0f;
    
    [Tooltip("Ângulo de erro de mira (0 = perfeito)")]
    [Range(0f, 15f)]
    public float dispersao = 0f;
    
    [Header("Tipo de Arma")]
    public TipoArma tipo = TipoArma.Automatica;
    
    [Header("Efeitos")]
    public AudioClip somDisparo;
    public ParticleSystem efeitoDisparo;
    
    // ===== VARIÁVEIS INTERNAS (Runtime) =====
    [HideInInspector] public float cooldownAtual = 0f;
    [HideInInspector] public int municaoAtual = 0;
    [HideInInspector] public bool estaRecarregando = false;
    [HideInInspector] public float tempoRecargaAtual = 0f;
    [HideInInspector] public int indiceBarrilAtual = 0;
    
    public enum TipoArma
    {
        Automatica,      // Dispara continuamente enquanto houver alvo
        SemiAutomatica,  // Um tiro por vez
        Rajada,          // 3-5 tiros rápidos, depois pausa
        Missil           // Míssil teleguiado
    }
    
    public void Inicializar()
    {
        municaoAtual = tamanhoCartucho;
        cooldownAtual = 0f;
        estaRecarregando = false;
    }
    
    public bool PodeAtirar()
    {
        if (estaRecarregando) return false;
        if (cooldownAtual > 0f) return false;
        if (tamanhoCartucho > 0 && municaoAtual <= 0) return false;
        return true;
    }
    
    public void IniciarRecarga(AudioSource fonte, AudioClip somPadrao)
    {
        estaRecarregando = true;
        tempoRecargaAtual = tempoRecarga;
        
        AudioClip som = somDisparo != null ? somDisparo : somPadrao;
        if (som != null && fonte != null) fonte.PlayOneShot(som);
    }
    
    public void AtualizarCooldowns(float deltaTime)
    {
        if (cooldownAtual > 0f)
            cooldownAtual -= deltaTime;
        
        if (estaRecarregando)
        {
            tempoRecargaAtual -= deltaTime;
            if (tempoRecargaAtual <= 0f)
            {
                estaRecarregando = false;
                municaoAtual = tamanhoCartucho;
            }
        }
    }
    
    public void Disparar(Transform torreMira, Transform alvo, GameObject torreDono, AudioSource fonte)
    {
        if (municaoPrefab == null || pontosDisparo == null || pontosDisparo.Length == 0) return;
        
        // Seleciona barril
        Transform barril = pontosDisparo[indiceBarrilAtual];
        indiceBarrilAtual = (indiceBarrilAtual + 1) % pontosDisparo.Length;
        
        // Cria projétil
        GameObject proj = Object.Instantiate(municaoPrefab, barril.position, barril.rotation);
        
        // Configura direção (com dispersão opcional)
        Vector3 direcao = (alvo.position - barril.position).normalized;
        
        if (dispersao > 0f)
        {
            // Adiciona erro aleatório
            float erroX = Random.Range(-dispersao, dispersao);
            float erroY = Random.Range(-dispersao, dispersao);
            direcao = Quaternion.Euler(erroX, erroY, 0) * direcao;
        }
        
        // Configura componente Projetil
        Projetil scriptProj = proj.GetComponent<Projetil>();
        if (scriptProj != null)
        {
            scriptProj.SetDono(torreDono);
            scriptProj.SetDirecao(direcao);
            scriptProj.velocidade = velocidadeProjetil;
            scriptProj.dano = (int)danoBase;
        }
        
        // Míssil teleguiado?
        if (tipo == TipoArma.Missil)
        {
            MissilTeleguiado missil = proj.GetComponent<MissilTeleguiado>();
            if (missil != null) missil.DefinirAlvo(alvo);
        }
        
        // Efeitos
        if (efeitoDisparo != null) efeitoDisparo.Play();
        if (somDisparo != null && fonte != null) fonte.PlayOneShot(somDisparo);
        
        // Atualiza estado
        cooldownAtual = intervaloTiro;
        if (tamanhoCartucho > 0)
        {
            municaoAtual--;
            if (municaoAtual <= 0)
            {
                IniciarRecarga(fonte, null);
            }
        }
    }
}
