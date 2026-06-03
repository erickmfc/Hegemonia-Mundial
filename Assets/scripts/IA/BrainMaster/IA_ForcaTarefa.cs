using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EstadoForcaTarefa { Agrupando, EmDeslocamento, Desembarque, CombateLivre }

public class IA_ForcaTarefa : MonoBehaviour
{
    public EstadoForcaTarefa estadoAtual = EstadoForcaTarefa.Agrupando;
    
    [Header("Unidades do Comboio")]
    public Transform unidadeAncora; // Ex: O USS Liberty Prime
    public List<Transform> escoltasNavais = new List<Transform>(); // Ex: USS Vindicator
    public List<Transform> escoltasAereas = new List<Transform>(); // Ex: Helicóptero Ray
    
    public Vector3 alvoInvasao; // A praia do jogador

    [Header("Efeitos Visuais (VFX)")]
    [Tooltip("Prefab do splash poligonal/partículas flat-shaded a ser instanciado no impacto com a areia.")]
    public GameObject prefabSplashInvasao;

    void Update()
    {
        if (unidadeAncora == null) 
        {
            // Se o transporte for destruído, a missão aborta ou as escoltas entram em frenesi
            estadoAtual = EstadoForcaTarefa.CombateLivre;
            LiberarEscoltas();
            return;
        }

        switch (estadoAtual)
        {
            case EstadoForcaTarefa.EmDeslocamento:
                MoverComboio();
                ChecarChegadaAoAlvo();
                break;
                
            case EstadoForcaTarefa.Desembarque:
                // Lógica para soltar os Tanques C1 e a Infantaria
                break;
        }
    }

    public void IniciarDeslocamento(Vector3 alvo)
    {
        alvoInvasao = alvo;
        estadoAtual = EstadoForcaTarefa.EmDeslocamento;
        
        // Dá o comando inicial para a âncora
        MoverNavio(unidadeAncora, alvoInvasao);

        // 1. Notifica o jogador através de um despacho formal do Alto Comando
        if (Hegemonia.UI.GerenciadorAlertasUI.Instancia != null)
        {
            Hegemonia.UI.GerenciadorAlertasUI.Instancia.MostrarAlerta(
                "Movimentação naval massiva detectada. Frota de invasão inimiga em rota de aproximação costeira!", 
                new Color(1f, 0.55f, 0f), // Amarelo/Laranja tático
                6f
            );
        }

        // 2. Adiciona o Sinalizador Tático holográfico em cima do navio capitânia (âncora)
        if (unidadeAncora != null && unidadeAncora.GetComponent<Hegemonia.UI.SinalizadorTatico>() == null)
        {
            unidadeAncora.gameObject.AddComponent<Hegemonia.UI.SinalizadorTatico>();
        }
    }

    void MoverComboio()
    {
        // 1. A âncora já foi mandada para o alvo no IniciarDeslocamento (ou re-enviada periodicamente se precisar)
        // MoverNavio(unidadeAncora, alvoInvasao);

        // 2. As escoltas navais seguem a âncora (formação V)
        for (int i = 0; i < escoltasNavais.Count; i++)
        {
            if (escoltasNavais[i] == null) continue;

            int side = (i % 2 == 0) ? 1 : -1;
            int row = (i / 2) + 1;
            
            Vector3 offset = new Vector3(side * 40f * row, 0, -30f * row); // Posições ao lado e atrás
            Vector3 posicaoEscolta = unidadeAncora.position + unidadeAncora.rotation * offset;
            
            MoverNavio(escoltasNavais[i], posicaoEscolta);
        }

        // 3. O helicóptero acompanha por cima
        foreach (Transform aerea in escoltasAereas)
        {
            if (aerea == null) continue;
            
            // Pausa a lógica nativa do helicóptero temporariamente para o script controlar o voo perfeitamente
            Helicoptero heli = aerea.GetComponent<Helicoptero>();
            if (heli != null) heli.estaVoando = false; 
            
            Vector3 posicaoCeu = unidadeAncora.position + new Vector3(0, 50, 0);
            
            // O helicóptero sempre olha para onde o navio âncora está olhando
            aerea.rotation = Quaternion.Lerp(aerea.rotation, unidadeAncora.rotation, Time.deltaTime * 5f);
            aerea.position = Vector3.Lerp(aerea.position, posicaoCeu, Time.deltaTime * 2f); // Suaviza o voo
        }
    }

    void MoverNavio(Transform navio, Vector3 destino)
    {
        NavMeshAgent agent = navio.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(destino);
        }
        else
        {
            ControleNavioRealista realista = navio.GetComponent<ControleNavioRealista>();
            if (realista != null)
            {
                realista.DefinirDestino(destino);
            }
        }
    }

    void ChecarChegadaAoAlvo()
    {
        float distancia = Vector3.Distance(unidadeAncora.position, alvoInvasao);
        if (distancia < 80f) // Chegou na costa
        {
            estadoAtual = EstadoForcaTarefa.Desembarque;
            IniciarInvasao();
        }
    }
    
    void IniciarInvasao()
    {
        Debug.Log("[IA_ForcaTarefa] Desembarque iniciado! Tropas focadas no combate livre.");
        
        // 1. Envia o alerta vermelho urgente do Alto Comando para o jogador
        if (Hegemonia.UI.GerenciadorAlertasUI.Instancia != null)
        {
            Hegemonia.UI.GerenciadorAlertasUI.Instancia.MostrarAlerta(
                "ALERTA VERMELHO: Desembarque anfíbio detectado! Forças inimigas invadindo as praias!", 
                new Color(1f, 0.15f, 0.15f), // Vermelho de perigo crítico
                6f
            );
        }

        // 2. Aciona o tremor de tela no momento exato do impacto e desembarque
        if (Hegemonia.UI.CameraShake.Instancia != null)
        {
            Hegemonia.UI.CameraShake.Instancia.Sacudir(2.5f, 1.3f);
        }

        // 3. Instancia o VFX/Splash se estiver configurado
        if (prefabSplashInvasao != null && unidadeAncora != null)
        {
            Instantiate(prefabSplashInvasao, unidadeAncora.position, Quaternion.identity);
        }

        LiberarEscoltas();
        
        // Simulação do desembarque: Ativar infantaria oculta, mudar modos de ataque
        NavioTransporteTropas transporte = unidadeAncora.GetComponent<NavioTransporteTropas>();
        if (transporte != null)
        {
            transporte.OrdemDesembarcarTerrestres(999, TipoUnidade.Estrutura); // Desembarca todas as tropas terrestres
        }
    }

    void LiberarEscoltas()
    {
        foreach (Transform aerea in escoltasAereas)
        {
            if (aerea == null) continue;
            Helicoptero heli = aerea.GetComponent<Helicoptero>();
            if (heli != null) 
            {
                heli.estaVoando = true; // Devolve o controle para o Helicóptero
                heli.modoCombateAtivo = true;
                heli.VoarEPousar(alvoInvasao + new Vector3(0, 0, 50)); // Manda flanquear a base!
            }
        }
        
        foreach (Transform naval in escoltasNavais)
        {
            if (naval == null) continue;
            // Libera as escoltas para avançar e atirar livremente
            MoverNavio(naval, alvoInvasao);
        }

        // Remove o sinalizador tático quando a frota se dispersar
        Hegemonia.UI.SinalizadorTatico sinalizador = GetComponentInChildren<Hegemonia.UI.SinalizadorTatico>();
        if (sinalizador != null)
        {
            Destroy(sinalizador);
        }
    }
}

