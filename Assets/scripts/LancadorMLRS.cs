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
    public float alcanceDoRadar = 300f;
    
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

    // Variáveis internas (Controle)
    private float cronometroDisparo;
    private int indiceBocaAtual = 0; // Qual tubo vai atirar agora (0 a 11)
    private Transform alvoAtual;
    private AudioSource audioSourceDisparo; // Canal para tiros
    private AudioSource audioSourceMotor;   // Canal para motor

    void Start()
    {
        // Configura o áudio automaticamente
        ConfigurarAudio();
    }

    void Update()
    {
        // 1. Procura alvo se não tiver um
        if (alvoAtual == null)
        {
            BuscarAlvo();
        }
        else
        {
            // 2. SEGURANÇA: Se o alvo foi destruído, reseta
            if (alvoAtual == null) return;

            // Se tiver alvo, verifica se ele ainda está vivo ou no alcance
            float distancia = Vector3.Distance(transform.position, alvoAtual.position);
            
            // Verifica se está ativo na hierarquia (para pooling)
            if (distancia > alcanceDoRadar || !alvoAtual.gameObject.activeInHierarchy)
            {
                alvoAtual = null;
                return;
            }

            // 3. Mira e Atira
            MirarNoAlvo();
            GerenciarDisparo();
        }
    }

    void ConfigurarAudio()
    {
        // Pega o AudioSource que já existe no objeto ou adiciona
        audioSourceDisparo = GetComponent<AudioSource>();
        
        // Cria um segundo canal de áudio só para o motor (para não cortar o som do tiro)
        GameObject motorObj = new GameObject("SomDoMotor");
        motorObj.transform.parent = this.transform;
        motorObj.transform.localPosition = Vector3.zero;
        
        audioSourceMotor = motorObj.AddComponent<AudioSource>();
        audioSourceMotor.loop = true; // Motor fica em loop
        audioSourceMotor.clip = somMotor;
        audioSourceMotor.volume = volumeMotor;
        audioSourceMotor.spatialBlend = 1f; // Som 3D
        audioSourceMotor.Play(); // Dá a partida no motor
    }

    void BuscarAlvo()
    {
        // Cria uma esfera invisível para detectar colisores em volta
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, alcanceDoRadar);
        float menorDistancia = Mathf.Infinity;
        Transform alvoMaisProximo = null;

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag(tagInimiga))
            {
                float distancia = Vector3.Distance(transform.position, hit.transform.position);
                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    alvoMaisProximo = hit.transform;
                }
            }
        }

        if (alvoMaisProximo != null)
        {
            alvoAtual = alvoMaisProximo;
            Debug.Log($"🎯 MLRS: Alvo identificado! [{alvoAtual.name}] Distância: {menorDistancia:F1}m");
        }
    }

    void MirarNoAlvo()
    {
        if (torreRotatoria != null)
        {
            // Faz a torre olhar para o alvo (apenas no eixo Y - horizontal)
            Vector3 direcaoAlvo = alvoAtual.position - torreRotatoria.position;
            direcaoAlvo.y = 0; // Mantém a rotação plana
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoAlvo);
            torreRotatoria.rotation = Quaternion.Slerp(torreRotatoria.rotation, rotacaoAlvo, Time.deltaTime * 5f);
        }

        if (canoElevacao != null)
        {
            // Faz os canos olharem para o alvo (ajuste de altura)
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
            cronometroDisparo = intervaloEntreDisparos; // Reseta o tempo
        }
    }

    void Atirar()
    {
        // Segurança: Verifica se temos o prefab do míssil e pontos de saída
        if (missilPrefab == null || pontosDeSaida.Length == 0) return;

        // Pega o ponto atual (0, 1, 2... até 11)
        Transform pontoDeDisparoAtual = pontosDeSaida[indiceBocaAtual];

        if (pontoDeDisparoAtual != null)
        {
            // Cria o míssil na posição e rotação da "boca" atual
            Instantiate(missilPrefab, pontoDeDisparoAtual.position, pontoDeDisparoAtual.rotation);

            // Toca o som de tiro
            if (audioSourceDisparo != null && somDisparo != null)
            {
                audioSourceDisparo.PlayOneShot(somDisparo);
            }

            // Efeito visual (Debug no editor)
            Debug.DrawRay(pontoDeDisparoAtual.position, pontoDeDisparoAtual.forward * 5, Color.red, 1f);
        }

        // Avança para a próxima boca
        indiceBocaAtual++;

        // Se chegou na última (12), volta para a primeira (0)
        if (indiceBocaAtual >= pontosDeSaida.Length)
        {
            indiceBocaAtual = 0;
        }
    }

    // Desenha o alcance do radar no editor para facilitar sua vida
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcanceDoRadar);
    }
}
