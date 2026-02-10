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

    void Start()
    {
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
                ComportamentoAutomatico();
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
            
            modoAtual = (ModoOperacao)proximo;
            
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
                // Fallback de tags para alvos sem identidade
                if (!hit.CompareTag("Player") && !hit.CompareTag("Aliado"))
                {
                    string[] tagsHostis = new string[] { "Inimigo", "Enemy", "Inimigos", "Destrutivel" };
                    foreach(string t in tagsHostis) { try { if(hit.CompareTag(t)) ehInimigo = true; } catch {} }
                    foreach(string t in tagsInimigas) { try { if(hit.CompareTag(t)) ehInimigo = true; } catch {} }
                }
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

    IEnumerator DispararSalvaInteligente(List<Transform> alvos)
    {
        tempoUltimoDisparo = Time.time;
        int misseisDisponiveisNaSalva = Mathf.Min(tirosPorSalva, municaoTotal);
        
        // Simulação de dano do Míssil
        float danoMissel = 200f; 
        MisselNaval refMissel = prefabMissel.GetComponent<MisselNaval>();
        if (refMissel != null) danoMissel = refMissel.dano;

        int indiceAlvoAtual = 0;
        Dictionary<Transform, float> danoProjetado = new Dictionary<Transform, float>();

        for (int i = 0; i < misseisDisponiveisNaSalva; i++)
        {
            // Validação de segurança para o índice
            if (indiceAlvoAtual >= alvos.Count) indiceAlvoAtual = 0;

            Transform alvoDaVez = null;
            if (alvos.Count > 0) alvoDaVez = alvos[indiceAlvoAtual];
            
            // Se o alvo atual morreu ou sumiu, tenta achar outro na lista
            if (alvoDaVez == null) 
            {
                bool achouNovo = false;
                for(int j=0; j < alvos.Count; j++) 
                {
                    if (alvos[j] != null) 
                    { 
                        alvoDaVez = alvos[j]; 
                        indiceAlvoAtual = j; 
                        achouNovo = true; 
                        break; 
                    }
                }
                
                // Se TODOS da lista morreram, faz um re-scan rápido
                if(!achouNovo) 
                {
                     // Pequena pausa antes de re-scanear para dar tempo de destruir objs
                    yield return new WaitForEndOfFrame(); 
                    alvos = BuscarTodosInimigos(); // Atualiza a lista
                    danoProjetado.Clear(); // Reseta projeção para nova lista
                    indiceAlvoAtual = 0;
                    
                    if (alvos.Count > 0) alvoDaVez = alvos[0];
                    else break; // Não tem mais ninguém vivo, economiza munição
                }
            }

            // Mira visual
            if (cabecaRotativa != null && alvoDaVez != null)
            {
                Vector3 direcao = alvoDaVez.position - cabecaRotativa.position;
                direcao.y = 0;
                cabecaRotativa.rotation = Quaternion.LookRotation(direcao);
            }

            // Dispara
            if (alvoDaVez != null)
            {
                DispararUnico(alvoDaVez.position, alvoDaVez);
            }
            
            // Lógica de Troca de Alvo (Evitar Overkill)
            if (alvoDaVez != null)
            {
                float danoJaCausado = 0f;
                if (danoProjetado.ContainsKey(alvoDaVez)) danoJaCausado = danoProjetado[alvoDaVez];
                
                SistemaDeDanos vidaScript = alvoDaVez.GetComponent<SistemaDeDanos>();
                if (vidaScript == null) vidaScript = alvoDaVez.GetComponentInParent<SistemaDeDanos>();

                if (vidaScript != null)
                {
                    danoProjetado[alvoDaVez] = danoJaCausado + danoMissel;
                    
                    // Se o dano projetado já mata, muda para o próximo da lista
                    if (danoProjetado[alvoDaVez] >= vidaScript.vidaAtual)
                    {
                        indiceAlvoAtual++;
                    }
                }
            }
            
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

            // Define cor baseada no modo
            switch (modoAtual)
            {
                case ModoOperacao.Passivo: style.normal.textColor = Color.gray; break;
                case ModoOperacao.Manual: style.normal.textColor = Color.yellow; break;
                case ModoOperacao.Automatico: style.normal.textColor = Color.red; break;
            }

            // Cria mensagem
            string texto = $"[{modoAtual}]\nMísseis: {municaoTotal}";
            
            // Desenha sombra (hack simples)
            GUI.color = Color.black;
            GUI.Label(new Rect(screenPos.x - 51, y - 61, 100, 50), texto, style);
            
            // Desenha texto
            GUI.color = Color.white; // Reseta cor
            GUI.Label(new Rect(screenPos.x - 50, y - 60, 100, 50), texto, style);
        }
    }
}
