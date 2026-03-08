using UnityEngine;
using System.Collections;

public class TorretaAntiaerea : MonoBehaviour
{
    [Header("Configurações do Radar (Aéreo)")]
    [Tooltip("Distância máxima que a torreta consegue enxergar as ameaças aéreas")]
    public float alcanceArea = 150f;
    
    [Tooltip("Altura mínima no eixo Y para considerar que o alvo está voando (evita atirar em tropas no chão)")]
    public float alturaMinimaVoo = 5f;

    [Header("Articulações da Torreta")]
    [Tooltip("Peça que gira 360 graus horizontalmente (Esquerda/Direita)")]
    public Transform baseGiratoria; 
    
    [Tooltip("Peça que sobe e desce para mirar no céu (Apontamento vertical)")]
    public Transform canoElevacao; 
    
    [Tooltip("Locais de onde os tiros vão sair (Pode ser 1 cano, 2 canos, 4 canos...)")]
    public Transform[] pontosDeDisparo;
    
    [Header("Controle de Disparo")]
    [Tooltip("Quantidade de tiros consecutivos a cada rajada")]
    public int quantidadeDeDisparo = 10;
    
    [Tooltip("Tiros por segundo (Cadência/Rate of Fire)")]
    public float tirosPorSegundo = 5f;
    
    [Tooltip("Tempo de pausa/recarga entre as rajadas")]
    public float tempoPausaRajada = 2f;
    
    [Header("Munição e Visual")]
    public GameObject prefabProjetil;
    public float velocidadeProjetil = 200f;
    public AudioClip somDisparo;

    // Variáveis internas state
    private Transform alvoAtual;
    private IdentidadeUnidade minhaIdentidade;
    private bool atirando = false;
    private AudioSource audioSource;
    private int indexPontoDisparo = 0;
    // Removido o limite de 50 objetos para permitir que o radar de longo alcance enxergue todos!

    void Start()
    {
        // Procura ou cria a identidade do time para não atirar nos próprios aviões
        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        if (minhaIdentidade == null)
        {
            minhaIdentidade = gameObject.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1; // 1 = Time do Jogador por padrão
        }

        // Prepara sistema de som
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        // Inicia varredura do céu a cada 0.5s para economizar processamento
        InvokeRepeating("ProcurarAlvoAereo", Random.Range(0f, 0.5f), 0.5f);
    }

    void Update()
    {
        if (alvoAtual != null)
        {
            // Se o alvo morreu ou fugiu
            if (!alvoAtual.gameObject.activeInHierarchy || Vector3.Distance(transform.position, alvoAtual.position) > alcanceArea + 10f)
            {
                alvoAtual = null;
                return;
            }

            // Gira fisicamente a torreta
            MirarNoAlvo();

            // Lógica de Atirar
            if (!atirando)
            {
                if (MirouComAcerto())
                {
                    StartCoroutine(RotinaDeDisparo());
                }
            }
        }
        else
        {
            // Ocioso: Gira a base 360 graus lentamente "vigiando o céu"
            if (baseGiratoria != null)
            {
                baseGiratoria.Rotate(0, 15f * Time.deltaTime, 0, Space.Self);
            }
        }
    }

    void ProcurarAlvoAereo()
    {
        // Se já tem um alvo válido dentro da área, mantém ele
        if (alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(transform.position, alvoAtual.position) <= alcanceArea)
                return; 
        }

        alvoAtual = null;

        // Pega TUDO na área sem limite de memória. Se usar NonAlloc com 50 de limite e raio de 900, 
        // o radar enche com 50 pedras/prédios e fica cego para os aviões inimigos!
        Collider[] todosAlvosNaArea = Physics.OverlapSphere(transform.position, alcanceArea);
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        foreach (Collider hit in todosAlvosNaArea)
        {
            
            // Filtro Principal: É uma aeronave? 
            // Retiramos a verificação de "altura" solta, pois algumas estruturas (como a Prefeitura) ou unidades 
            // terrestres altas possuíam a posição Y maior que a altura mínima, fazendo o Ares atirar no chão!
            string nomeBaixo = hit.name.ToLower();
            
            bool ehAereo = hit.GetComponentInParent<Helicoptero>() != null || 
                           hit.GetComponentInParent<ControleAviao>() != null ||
                           nomeBaixo.Contains("aviao") || 
                           nomeBaixo.Contains("heli") ||
                           nomeBaixo.Contains("caca") ||
                           nomeBaixo.Contains("caça") ||
                           nomeBaixo.Contains("jato") ||
                           hit.tag == "Areo" || 
                           hit.tag == "Aereo";

            if (!ehAereo) continue; // Pula unidades terrestres e prédios altos

            // Filtro Secundário: Fogo Amigo
            IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
            if (idAlvo == null) idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();
            
            if (idAlvo != null && idAlvo.teamID != minhaIdentidade.teamID && idAlvo.teamID != 0)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = hit.transform;
                }
            }
        }

        if (melhorAlvo != null)
        {
            alvoAtual = melhorAlvo;
        }
    }

    void MirarNoAlvo()
    {
        // 1. Gira a base horizontalmente (360) com velocidade extrema para caças
        if (baseGiratoria != null)
        {
            Vector3 direcaoBase = alvoAtual.position - baseGiratoria.position;
            direcaoBase.y = 0; // Trava o eixo Y para a base não inclinar ou capotar
            if (direcaoBase != Vector3.zero)
            {
                Quaternion rotacaoAlvoBase = Quaternion.LookRotation(direcaoBase);
                // Velocidade de tracking absurda (60f) para acompanhar jatos supersônicos
                baseGiratoria.rotation = Quaternion.Slerp(baseGiratoria.rotation, rotacaoAlvoBase, Time.deltaTime * 60f);
            }
        }

        // 2. Gira o cano verticalmente para olhar pro avião
        if (canoElevacao != null)
        {
            Vector3 direcaoCano = alvoAtual.position - canoElevacao.position;
            if (direcaoCano != Vector3.zero)
            {
                Quaternion rotacaoAlvoCano = Quaternion.LookRotation(direcaoCano);
                // Velocidade de tracking absurda (60f) para acompanhar jatos supersônicos
                canoElevacao.rotation = Quaternion.Slerp(canoElevacao.rotation, rotacaoAlvoCano, Time.deltaTime * 60f);
            }
        }
    }

    bool MirouComAcerto()
    {
        // SE FOR UM MÍSSIL GUIADO (ARES), BURLA A MIRA PERFEITA!
        // Como jatos a 150km/h quebram a matemática do Vector3.Lerp, nós trapaceamos
        // dando na Bateria Antiaerea permissão pra atirar de "olho fechado" logo que chegar perto angularmente.
        if (alvoAtual == null) return false;
        
        // 1. Verifica se a base horizontal chegou o suficiente na rotação
        if (baseGiratoria != null)
        {
            Vector3 direcaoPlanaAoAlvo = (alvoAtual.position - baseGiratoria.position);
            direcaoPlanaAoAlvo.y = 0;
            if (direcaoPlanaAoAlvo != Vector3.zero)
            {
                direcaoPlanaAoAlvo.Normalize();
                Vector3 baseForwardPlano = baseGiratoria.forward;
                baseForwardPlano.y = 0;
                baseForwardPlano.Normalize();
                
                // Tolerância gigantesca (45 GRAUS) para jatos velozes não ficarem fora da zona
                if (Vector3.Angle(baseForwardPlano, direcaoPlanaAoAlvo) > 45f) return false;
            }
        }
        
        // 2. Verifica se o cano vertical levantou o suficiente
        if (canoElevacao != null)
        {
            // O canoElevacao.forward é EXATAMENTE para onde o tiro vai sair
            Vector3 direcaoCanoIdeal = (alvoAtual.position - canoElevacao.position).normalized;
            
            // Tolerância gigante no cano também (45 graus)
            if (Vector3.Angle(canoElevacao.forward, direcaoCanoIdeal) > 45f) return false;
        }

        return true;
    }

    IEnumerator RotinaDeDisparo()
    {
        atirando = true;
        
        // Calcula o delay matemático em relação aos tiros por segundo. Ex: 5 tiros por segundo = 0.2s de intervalo
        float tempoEntreTiros = 1f / tirosPorSegundo;
        int disparosFeitos = 0;

        while (disparosFeitos < quantidadeDeDisparo && alvoAtual != null)
        {
            // Se o avião dar uma manobra brusca e escapar da mira, ele PAUSA a rajada esperando o cano virar e alinhar de novo
            while (alvoAtual != null && !MirouComAcerto())
            {
                yield return null;
            }
            
            if (alvoAtual == null) break;

            DispararMunicoes();
            disparosFeitos++;
            
            yield return new WaitForSeconds(tempoEntreTiros);
        }

        // Tempo de pausa para a torreta respirar (Cooldown da Rajada)
        yield return new WaitForSeconds(tempoPausaRajada);
        atirando = false;
    }

    void DispararMunicoes()
    {
        if (prefabProjetil == null) return;

        // Decide de qual cano a bala vai sair alternadamente
        Transform pontoSaida = transform;
        if (pontosDeDisparo != null && pontosDeDisparo.Length > 0)
        {
            if (pontosDeDisparo[indexPontoDisparo] != null)
            {
                pontoSaida = pontosDeDisparo[indexPontoDisparo];
            }
            indexPontoDisparo = (indexPontoDisparo + 1) % pontosDeDisparo.Length; // Alterna os canos cíclicamente
        }

        // Cria a bala
        GameObject bala = Instantiate(prefabProjetil, pontoSaida.position, pontoSaida.rotation);
        
        // Adiciona as leis da física no tiro
        Projetil p = bala.GetComponent<Projetil>();
        if (p == null) p = bala.AddComponent<Projetil>();

        p.SetDono(transform.root.gameObject);
        p.velocidade = velocidadeProjetil;
        
        // Aponta a bala EXATAMENTE para alvo (Mira Teleguiada)
        if (alvoAtual != null)
        {
            Vector3 direcao = (alvoAtual.position - pontoSaida.position).normalized;
            p.SetDirecao(direcao);
            
            // Ativa o modo Perseguidora Míssil!
            p.SetAlvo(alvoAtual);
            p.curvaDePerseguicao = 90f; // O míssil vira até 90 graus por segundo cassando o alvo alvo
        }
        else
        {
            p.SetDirecao(pontoSaida.forward);
        }

        // Efeito sonoro
        if (somDisparo != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Um leve randomizador para não ficar robótico
            audioSource.PlayOneShot(somDisparo, 0.8f);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Pinta a área do radar de Ciano para o level designer enxergar
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, alcanceArea);
    }
}
