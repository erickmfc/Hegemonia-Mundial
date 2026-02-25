using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LancadorNaval : MonoBehaviour
{
    public enum ModoOperacao { Passivo, Manual, Automatico }

    [Header("Configurações do Lançador")]
    public ModoOperacao modoAtual = ModoOperacao.Passivo;
    public Transform cabecaRotativa; // Parte que gira (se houver)
    public Transform[] pontosDeSaida; // Onde os mísseis nascem (bocas do VLS)
    public GameObject prefabMissel; // O prefab que tem o script MisselNaval

    [Header("Configurações de Combate")]
    public int municaoTotal = 32;
    public int municaoMaxima = 32; // Limite para recarga
    public int tirosPorSalva = 4; // Quantos mísseis saem de uma vez
    public float intervaloEntreTiros = 0.5f; // Tempo entre mísseis da mesma salva
    public float tempoRecargaSalva = 5.0f; // Tempo entre salvas
    public float alcanceRadar = 500f;
    public AudioClip somDisparo;

    [Header("Configurações de Áudio")]
    [Range(0f, 1f)] public float volumeSom = 1.0f;
    [Range(0.1f, 3f)] public float pitchSom = 1.0f;
    public float distanciaSomMinima = 10f;
    public float distanciaSomMaxima = 500f;

    [Header("Tags de Alvos")]
    public List<string> tagsInimigas = new List<string> { "Inimigo", "Destrutivel" };
    
    // Estado interno
    private float tempoUltimoDisparo = 0f;
    private int indicePontoSaida = 0; // Para alternar entre as bocas do VLS
    private AudioSource audioSource;
    private Transform alvoAtual;

    // --- Identidade Própria ---
    private IdentidadeUnidade minhaIdentidade;
    private ControleUnidade meuControle;
    
    // Timer para atrasar a ativação do modo automático
    private float tempoParaAtivarAutomatico = 0f;

    // --- BANCO DE DADOS GLOBAL DE COMBATE DA FROTA ---
    // Compartilhado estaticamente por TODOS os navios! Impede que 5 navios atirem num barco que já vai morrer.
    private static Dictionary<Transform, float> bancoDanoProjetadoFrotas = new Dictionary<Transform, float>();

    void Start()
    {
        // Se Maxima não foi configurada ou menor que total inicial, ajusta
        if (municaoMaxima < municaoTotal) municaoMaxima = municaoTotal;

        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configurações iniciais do AudioSource para 3D
        audioSource.spatialBlend = 1.0f; // Torna o som 3D
        audioSource.minDistance = distanciaSomMinima;
        audioSource.maxDistance = distanciaSomMaxima;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;

        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>(); 
        if (minhaIdentidade == null) minhaIdentidade = GetComponent<IdentidadeUnidade>();

        // Cache do ControleUnidade para saber se estou selecionado
        meuControle = GetComponent<ControleUnidade>();
        if (meuControle == null) meuControle = GetComponentInParent<ControleUnidade>();
    }

    // --- SISTEMA DE RECARGA (USADO PELO PIER) ---
    public void Recarregar(int quantidade)
    {
        municaoTotal = Mathf.Min(municaoTotal + quantidade, municaoMaxima);
    }

    void Update()
    {
        // Atualiza configurações em tempo real se alteradas no Inspector
        if (audioSource != null)
        {
            audioSource.minDistance = distanciaSomMinima;
            audioSource.maxDistance = distanciaSomMaxima;
        }

        // 1. Controle de Modos (Tecla 'I')
        ChecarTrocaDeModo();

        // 2. Comportamento baseado no modo
        switch (modoAtual)
        {
            case ModoOperacao.Manual:
                ComportamentoManual();
                break;
            case ModoOperacao.Automatico:
                // Aguarda 3 segundos antes de iniciar o disparo (Safety Delay)
                if (Time.time >= tempoParaAtivarAutomatico)
                {
                    ComportamentoAutomatico();
                }
                break;
            case ModoOperacao.Passivo:
                // Não faz nada, descansa soldado
                break;
        }
    }

    void ChecarTrocaDeModo()
    {
        // VERIFICAÇÃO CRÍTICA: Só permite ação se ESTIVER SELECIONADO
        if (meuControle == null || !meuControle.selecionado) return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            // Avança para o próximo modo na lista (ciclo: 0 -> 1 -> 2 -> 0...)
            int proximo = (int)modoAtual + 1;
            if (proximo > 2) proximo = 0;
            
            ModoOperacao novoModo = (ModoOperacao)proximo;
            
            // Se for entrar em Automático, define o delay de 3 segundos
            if (novoModo == ModoOperacao.Automatico)
            {
                tempoParaAtivarAutomatico = Time.time + 3.0f;
            }
            
            modoAtual = novoModo;
            
            Debug.Log($"<color=cyan>[LANÇADOR]</color> Modo alterado para: {modoAtual}");
        }
    }

    // --- MODO MANUAL (Mouse Direito) ---
    void ComportamentoManual()
    {
        // Só permite atirar manualmente se ESTIVER SELECIONADO
        if (meuControle == null || !meuControle.selecionado) return;

        // Se clicar com botão direito
        if (Input.GetMouseButtonDown(1))
        {
            Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Raio atinge o chão?
            if (Physics.Raycast(raio, out hit, 1000f))
            {
                // Verifica se tem munição e se o tempo de recarga passou
                if (PodeAtirar())
                {
                    Debug.Log($"[MANUAL] Disparando em coordenadas: {hit.point}");
                    // Cria uma lista falsa com 1 posição nula para indicar disparo no chão
                    StartCoroutine(DispararSalvaManual(hit.point));
                }
            }
        }
    }

    IEnumerator DispararSalvaManual(Vector3 ponto)
    {
        tempoUltimoDisparo = Time.time;
        int misseisDisponiveisNaSalva = Mathf.Min(tirosPorSalva, municaoTotal);
        
        for (int i = 0; i < misseisDisponiveisNaSalva; i++)
        {
            DispararUnico(ponto, null);
            yield return new WaitForSeconds(intervaloEntreTiros);
        }
    }

    // --- MODO AUTOMÁTICO (Radar Inteligente) ---
    void ComportamentoAutomatico()
    {
        if (!PodeAtirar()) return;

        // 1. Escaneia a área em busca de TODOS os alvos válidos
        List<Transform> alvosValidos = BuscarTodosInimigos();

        if (alvosValidos.Count > 0)
        {
            // 2. Calcula distribuição de mísseis
            StartCoroutine(DispararSalvaInteligente(alvosValidos));
        }
    }

    bool PodeAtirar()
    {
        return Time.time > tempoUltimoDisparo + tempoRecargaSalva && municaoTotal > 0;
    }
    
    // Retorna lista de inimigos ordenados por proximidade
    List<Transform> BuscarTodosInimigos()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, alcanceRadar);
        int meuTime = (minhaIdentidade != null) ? minhaIdentidade.teamID : 1; 

        List<Transform> listaInimigos = new List<Transform>();

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            
            bool ehInimigo = false;

            IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
            if (idAlvo == null) idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();

            if (idAlvo != null)
            {
                if (idAlvo.teamID != 0 && idAlvo.teamID != meuTime) ehInimigo = true;
            }
            else
            {
                // Sem identidade, ignoramos.
                // O sistema de tags estava causando erros pois as tags não existem no projeto.
                // Agora confiamos 100% no componente IdentidadeUnidade adicionado pelo AnalistaExecutivo.
            }

            if (ehInimigo)
            {
                SistemaDeDanos vida = hit.GetComponent<SistemaDeDanos>();
                if (vida == null) vida = hit.GetComponentInParent<SistemaDeDanos>();
                
                // Só adiciona se tem sistema de danos e vida > 0
                if (vida != null && vida.vidaAtual > 0)
                {
                    listaInimigos.Add(hit.transform);
                }
            }
        }
        
        // Ordena por distância (mais perto primeiro)
        listaInimigos.Sort((a, b) => Vector3.Distance(transform.position, a.position).CompareTo(Vector3.Distance(transform.position, b.position)));
        
        return listaInimigos;
    }

    void RegistrarDanoProjetado(Transform alvo, float dano)
    {
        if (alvo == null) return;
        if (!bancoDanoProjetadoFrotas.ContainsKey(alvo)) bancoDanoProjetadoFrotas[alvo] = 0f;
        
        bancoDanoProjetadoFrotas[alvo] += dano;
        
        // O míssil expira da conta após 15 segundos se não acertar (Segurança)
        StartCoroutine(DecairDanoProjetado(alvo, dano, 15f));
    }

    IEnumerator DecairDanoProjetado(Transform alvo, float dano, float tempo)
    {
        yield return new WaitForSeconds(tempo);
        if (alvo != null && bancoDanoProjetadoFrotas.ContainsKey(alvo))
        {
            bancoDanoProjetadoFrotas[alvo] -= dano;
            if (bancoDanoProjetadoFrotas[alvo] < 0) bancoDanoProjetadoFrotas[alvo] = 0;
        }
    }

    IEnumerator DispararSalvaInteligente(List<Transform> alvos)
    {
        tempoUltimoDisparo = Time.time;
        int misseisDisponiveisNaSalva = Mathf.Min(tirosPorSalva, municaoTotal);
        
        // Simulação de dano do Míssil
        float danoMissel = 200f; 
        MisselNaval refMissel = prefabMissel.GetComponent<MisselNaval>();
        if (refMissel != null) danoMissel = refMissel.dano;

        for (int i = 0; i < misseisDisponiveisNaSalva; i++)
        {
            Transform alvoDaVez = null;

            // PROCURA O MELHOR ALVO: Um que esteja vivo e não tenha mísseis suficientes indo para matá-lo
            for (int j = 0; j < alvos.Count; j++)
            {
                Transform potencialAlvo = alvos[j];
                if (potencialAlvo == null) continue;

                SistemaDeDanos vidaScript = potencialAlvo.GetComponent<SistemaDeDanos>();
                if (vidaScript == null) vidaScript = potencialAlvo.GetComponentInParent<SistemaDeDanos>();

                if (vidaScript != null && vidaScript.vidaAtual > 0)
                {
                    float danoFuturo = 0f;
                    // Consulta a Rede Global de Combate (Todos os navios amigos informam aqui)
                    if (bancoDanoProjetadoFrotas.ContainsKey(potencialAlvo)) 
                    {
                        danoFuturo = bancoDanoProjetadoFrotas[potencialAlvo];
                    }

                    // Verifica: A vida real dele é MAIOR que os mísseis que já estão voando pra cabeça dele?
                    if (vidaScript.vidaAtual > danoFuturo)
                    {
                        // Bingo! Achamos um alvo precisando de mais porrada.
                        alvoDaVez = potencialAlvo;
                        
                        // Agenda o dano na nuvem militar para outros não focarem atoa
                        RegistrarDanoProjetado(potencialAlvo, danoMissel);
                        break;
                    }
                }
            }

            // Se varremos TUDO e não tem mais alvo sobrando, ou todo mundo já está "Virtualmente morto"
            if (alvoDaVez == null)
            {
                Debug.Log($"<color=green>[LANÇADOR]</color> Inimigos já possuem mísseis letais a caminho. Parando a salva para economizar munição.");
                break; // Cancela o resto da salva!
            }

            // Mira visual da torre para o alvo que vai atirar
            if (cabecaRotativa != null)
            {
                // Respeita a inclinação realista do navio sobre as ondas!
                Vector3 direcao = alvoDaVez.position - cabecaRotativa.position;
                Vector3 upDoNavio = cabecaRotativa.parent != null ? cabecaRotativa.parent.up : Vector3.up;
                
                // Projeta o alvo no "chão" do navio para a torre não focar pra cima/baixo torta
                Vector3 direcaoNoConves = Vector3.ProjectOnPlane(direcao, upDoNavio).normalized;

                if (direcaoNoConves != Vector3.zero)
                {
                    cabecaRotativa.rotation = Quaternion.LookRotation(direcaoNoConves, upDoNavio);
                }
            }

            // Dispara e vai diminuir munição original no método
            DispararUnico(alvoDaVez.position, alvoDaVez);
            
            yield return new WaitForSeconds(intervaloEntreTiros);
        }
    }

    void DispararUnico(Vector3 destino, Transform alvoFixo)
    {
        if (municaoTotal <= 0) return;
        municaoTotal--;

        // Pega o próximo ponto de saída (rodízio entre os tubos)
        Transform pontoDeSaida = transform; // Fallback
        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            pontoDeSaida = pontosDeSaida[indicePontoSaida];
            indicePontoSaida = (indicePontoSaida + 1) % pontosDeSaida.Length;
        }

        // Cria o míssil
        GameObject misselObj = Instantiate(prefabMissel, pontoDeSaida.position, pontoDeSaida.rotation);
        
        // Configura o míssil
        MisselNaval scriptMissel = misselObj.GetComponent<MisselNaval>();
        if (scriptMissel != null)
        {
            // Se tivermos um alvo fixo (Auto), atualizamos a posição, senão vai no chão (Manual)
            Vector3 alvoFinal = alvoFixo != null ? alvoFixo.position : destino;
            
            // Passamos tambem o Transform do alvoFixo para o missel poder perseguir (homing)
            scriptMissel.IniciarAtaque(alvoFinal, alvoFixo);
        }

        // Som
        if (somDisparo != null && audioSource != null)
        {
            // Aplica configurações de volume e pitch antes de tocar
            audioSource.volume = volumeSom;
            audioSource.pitch = pitchSom;
            audioSource.PlayOneShot(somDisparo);
        }
    }
    
    // Desenha o raio do radar no editor para facilitar ajuste
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);
    }

    void OnGUI()
    {
        // 1. Só mostra se estiver selecionado!
        if (meuControle == null || !meuControle.selecionado) return;
        
        if (MenuConstrucao.EstaAberto || MenuPier.EstaAberto) return;

        if (Camera.main == null) return;

        // Pega a posição do lançador na tela
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);

        // Só desenha se estiver na frente da câmera
        if (screenPos.z > 0)
        {
            // Ajusta eixo Y (Unity GUI é invertido em relação a coordenadas de tela)
            float y = Screen.height - screenPos.y;
            
            // Define estilo
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;

            string texto = "";

            // Define cor baseada no modo
            switch (modoAtual)
            {
                case ModoOperacao.Passivo: 
                    style.normal.textColor = Color.gray; 
                    texto = $"[{modoAtual}]\nMísseis: {municaoTotal}/{municaoMaxima}";
                    break;
                case ModoOperacao.Manual: 
                    style.normal.textColor = Color.yellow; 
                    texto = $"[{modoAtual}]\nMísseis: {municaoTotal}/{municaoMaxima}";
                    break;
                case ModoOperacao.Automatico: 
                    style.normal.textColor = Color.red;
                    // Se estiver no delay de armar
                    if (Time.time < tempoParaAtivarAutomatico)
                    {
                        float restante = tempoParaAtivarAutomatico - Time.time;
                        texto = $"[ARMANDO {restante:F1}s]\nMísseis: {municaoTotal}/{municaoMaxima}";
                        style.normal.textColor = Color.Lerp(Color.yellow, Color.red, Mathf.PingPong(Time.time * 5f, 1f));
                    }
                    else
                    {
                        texto = $"[{modoAtual}]\nMísseis: {municaoTotal}/{municaoMaxima}";
                    }
                    break;
            }

            // Cria mensagem final
            
            // Desenha sombra (hack simples)
            GUI.color = Color.black;
            GUI.Label(new Rect(screenPos.x - 51, y - 61, 100, 50), texto, style);
            
            // Desenha texto
            GUI.color = Color.white; // Reseta cor
            GUI.Label(new Rect(screenPos.x - 50, y - 60, 100, 50), texto, style);
        }
    }
}
