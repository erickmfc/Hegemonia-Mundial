using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class LancadorMLRS : MonoBehaviour
{
    [Header("--- Configurações de Combate ---")]
    [Tooltip("Arraste aqui o Prefab do Míssil que será criado")]
    public GameObject missilPrefab;
    
    [Tooltip("Distância máxima que ele detecta inimigos")]
    public float alcanceDoRadar = 1000f; // Forcei 1000m para garantir
    
    [Tooltip("Tempo em segundos entre cada disparo")]
    public float intervaloEntreDisparos = 0.5f;

    [Tooltip("Tag para identificar inimigos (Ex: 'Inimigo')")]
    public string tagInimiga = "Inimigo";

    [Header("--- As 12 Bocas de Fogo ---")]
    [Tooltip("Crie 12 objetos vazios na ponta dos tubos e arraste eles para cá")]
    public Transform[] pontosDeSaida; // Array para os 12 tubos

    [Header("--- Parte da Torre (Rotação) ---")]
    [Tooltip("A parte do veículo que gira (Turret)")]
    public Transform torreRotatoria;
    [Tooltip("A parte que sobe e desce (Opcional)")]
    public Transform canoElevacao;

    [Header("--- Áudio e Sons ---")]
    public AudioClip somDisparo;
    public AudioClip somMotor;
    [Range(0f, 1f)] public float volumeMotor = 0.5f;

    [Header("--- Modos de Combate ---")]
    [Tooltip("Se marcado (ATIVO), busca e atira sozinho.")]
    public bool modoCombateAtivo = true;

    [Header("--- Debug (Modo Detetive) ---")]
    [Tooltip("Marque isso se o tanque não estiver atirando para saber o motivo no Console")]
    public bool mostrarLogsDeBusca = true;

    // Variáveis internas
    private float cronometroDisparo;
    private int indiceBocaAtual = 0;
    private Transform alvoAtual;
    private AudioSource audioSourceDisparo;
    private AudioSource audioSourceMotor;
    private float timerDebug = 0f; 

    void Start()
    {
        ConfigurarAudio();
        pontosDeSaida = PontoSaidaUtil.Garantir(transform, pontosDeSaida, "saida", "tube", "tubo", "muzzle", "fire", "spawn", "element");
    }

    void Update()
    {
        if (!modoCombateAtivo) { alvoAtual = null; return; }

        if (alvoAtual == null)
        {
            BuscarAlvo();
        }
        else
        {
            // Verifica se o alvo ainda existe
            if (alvoAtual == null || !alvoAtual.gameObject.activeInHierarchy)
            {
                alvoAtual = null;
                return;
            }

            // Verifica distância
            float distancia = Vector3.Distance(transform.position, alvoAtual.position);
            if (distancia > alcanceDoRadar)
            {
                if(mostrarLogsDeBusca) Debug.LogWarning($"⚠️ Alvo perdido! {alvoAtual.name} fugiu para {distancia:F1}m (Máx: {alcanceDoRadar}m)");
                alvoAtual = null;
                return;
            }

            MirarNoAlvo();
            GerenciarDisparo();
        }
    }

    void ConfigurarAudio()
    {
        audioSourceDisparo = GetComponent<AudioSource>();
        
        // Cria canal de motor se não existir
        Transform motorCheck = transform.Find("SomDoMotor");
        if (motorCheck == null)
        {
            GameObject motorObj = new GameObject("SomDoMotor");
            motorObj.transform.parent = this.transform;
            motorObj.transform.localPosition = Vector3.zero;
            
            audioSourceMotor = motorObj.AddComponent<AudioSource>();
            audioSourceMotor.loop = true;
            audioSourceMotor.clip = somMotor;
            audioSourceMotor.volume = volumeMotor;
            audioSourceMotor.spatialBlend = 1f;
            if(somMotor != null) audioSourceMotor.Play();
        }
    }

    void BuscarAlvo()
    {
        timerDebug -= Time.deltaTime;
        if (timerDebug > 0) return;
        timerDebug = 0.5f; // Busca a cada 0.5s

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, alcanceDoRadar);
        float menorDistancia = Mathf.Infinity;
        Transform candidato = null;

        foreach (var hit in hitColliders)
        {
            if (hit.transform.root == transform.root) continue; // Ignora a si mesmo

            if (TagSafe.Matches(hit, tagInimiga))
            {
                float distancia = Vector3.Distance(transform.position, hit.transform.position);
                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    candidato = hit.transform;
                }
            }
        }

        if (candidato != null)
        {
            alvoAtual = candidato;
            if(mostrarLogsDeBusca) Debug.Log($"🎯 ALVO TRAVADO: {alvoAtual.name} | Dist: {menorDistancia:F1}m");
        }
        else if (mostrarLogsDeBusca)
        {
            Debug.Log($"🔍 Radar varrendo... Nada encontrado em {alcanceDoRadar}m");
        }
    }

    void MirarNoAlvo()
    {
        if (torreRotatoria != null)
        {
            Vector3 direcaoAlvo = alvoAtual.position - torreRotatoria.position;
            direcaoAlvo.y = 0;
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoAlvo);
            torreRotatoria.rotation = Quaternion.Slerp(torreRotatoria.rotation, rotacaoAlvo, Time.deltaTime * 5f);
        }

        if (canoElevacao != null)
        {
            Vector3 direcaoCanos = alvoAtual.position - canoElevacao.position;
            Quaternion rotacaoCanos = Quaternion.LookRotation(direcaoCanos);
            canoElevacao.rotation = Quaternion.Slerp(canoElevacao.rotation, rotacaoCanos, Time.deltaTime * 5f);
        }
    }

    void GerenciarDisparo()
    {
        cronometroDisparo -= Time.deltaTime;

        if (cronometroDisparo <= 0)
        {
            Atirar();
            cronometroDisparo = intervaloEntreDisparos;
        }
    }

    void Atirar()
    {
        pontosDeSaida = PontoSaidaUtil.Garantir(transform, pontosDeSaida, "saida", "tube", "tubo", "muzzle", "fire", "spawn", "element");
        if (missilPrefab == null) 
        {
            Debug.LogError("⛔ LEOPARD ERRO! O campo 'Missil Prefab' está vazio no Inspector!");
            return;
        }

        if (pontosDeSaida == null || pontosDeSaida.Length == 0)
        {
            Debug.LogError("⛔ LEOPARD ERRO! A lista 'Pontos De Saida' está com tamanho 0!");
            return;
        }

        // --- SISTEMA ANTI-FALHA (Pula slots vazios) ---
        int tentativas = 0;
        // Enquanto o slot atual for nulo (None), avança para o próximo
        while (pontosDeSaida[indiceBocaAtual] == null && tentativas < pontosDeSaida.Length)
        {
            indiceBocaAtual++;
            if (indiceBocaAtual >= pontosDeSaida.Length) indiceBocaAtual = 0;
            tentativas++;
        }
        
        // Se depois de tentar rodar tudo, ainda for nulo (todos vazios)
        if (pontosDeSaida[indiceBocaAtual] == null)
        {
             Debug.LogError($"⛔ LEOPARD ERRO! Todos os {pontosDeSaida.Length} 'Pontos De Saida' estão vazios (None)!");
             return;
        }
        // ----------------------------------------------

        Transform pontoDeDisparoAtual = pontosDeSaida[indiceBocaAtual];

        if (pontoDeDisparoAtual != null)
        {
            GameObject novoMissil = Instantiate(missilPrefab, pontoDeDisparoAtual.position, pontoDeDisparoAtual.rotation);

            // --- SEGURANÇA: IGNORAR COLISÃO COM O PRÓPRIO TANQUE ---
            Collider[] tankColliders = GetComponentsInChildren<Collider>();
            Collider[] missilColliders = novoMissil.GetComponentsInChildren<Collider>();
            foreach (var tankCol in tankColliders)
            {
                foreach (var missilCol in missilColliders)
                {
                    Physics.IgnoreCollision(tankCol, missilCol);
                }
            }
            // -------------------------------------------------------

            // --- TENTATIVA UNIVERSAL DE INICIALIZAÇÃO ---
            // Tenta ativar o míssil de todas as formas conhecidas (Leopard Novo ou Submarino Antigo)
            
            bool disparoSucesso = false;

            // 1. Tenta script novo (Leopard Inteligente)
            var scriptLeopard = novoMissil.GetComponent<MisselLeopardAutomatico>();
            if (scriptLeopard != null)
            {
                scriptLeopard.DefinirAlvo(alvoAtual);
                disparoSucesso = true;
            }
            
            // 2. Tenta script antigo (Submarino) usando Vector3
            if (!disparoSucesso)
            {
                var scriptSub = novoMissil.GetComponent<MisselSubmarino>();
                if (scriptSub != null)
                {
                    // Lança mirando na posição atual do alvo
                    scriptSub.IniciarLancamento(alvoAtual.position, false); // false = não submerso
                    disparoSucesso = true;
                }
            }

            // 3. Fallback (Mensagens genéricas)
            if (!disparoSucesso)
            {
                novoMissil.SendMessage("DefinirAlvo", alvoAtual, SendMessageOptions.DontRequireReceiver);
                novoMissil.SendMessage("SetTarget", alvoAtual, SendMessageOptions.DontRequireReceiver);
            }

            if (mostrarLogsDeBusca) Debug.Log("🚀 Míssil Disparado!");

            if (audioSourceDisparo != null && somDisparo != null)
            {
                audioSourceDisparo.PlayOneShot(somDisparo);
            }
        }

        // Prepara próxima boca
        indiceBocaAtual++;
        if (indiceBocaAtual >= pontosDeSaida.Length) indiceBocaAtual = 0;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = (alvoAtual != null) ? Color.red : new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, alcanceDoRadar);
        
        if (alvoAtual != null)
        {
            Gizmos.DrawLine(transform.position, alvoAtual.position);
        }
    }
}
