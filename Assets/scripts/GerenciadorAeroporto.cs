using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorAeroporto : MonoBehaviour
{
    [Header("Hierarquia do Aeroporto (Vincular do Inspector)")]
    [Tooltip("Grupo pai contendo as marcações 'Parada' a 'Parada 4'")]
    public Transform patio;
    
    [Tooltip("Grupo contendo 'Preparacao' e 'Pronto'")]
    public Transform hangarAviao;
    
    [Tooltip("Grupo de waypoints de decolagem: alinhamento -> decolagem -> voo...")]
    public Transform decolagem;
    
    [Tooltip("Grupo de waypoints de pouso OBRIGATÓRIOS (Decida)")]
    public Transform decida;

    [Header("Gestão de Frota e Status")]
    public List<ControleAviao> avioesNoPatio = new List<ControleAviao>();
    public List<ControleAviao> avioesNoHangar = new List<ControleAviao>();

    [Header("Interface (UI)")]
    public GameObject menuAeroportoUI;
    
    private bool menuAtivo = false;
    private int abaAtual = 0; 
    [HideInInspector] public ControleAviao aviaoSelecionadoParaMissao;

    // Listas internas de Waypoints lidas no Awake
    [HideInInspector] public List<Transform> waypointsPatio = new List<Transform>();
    [HideInInspector] public Transform wpPreparacao;
    [HideInInspector] public Transform wpPronto;
    [HideInInspector] public List<Transform> waypointsDecolagem = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida = new List<Transform>();
    
    [HideInInspector] public Transform wpAndadar;
    [HideInInspector] public Transform wpAnalise;

    void Awake()
    {
        if (patio != null)
        {
            foreach (Transform filho in patio)
                if (filho.name.ToLower().Contains("parada")) waypointsPatio.Add(filho);
        }

        if (hangarAviao != null)
        {
            wpPreparacao = hangarAviao.Find("Preparacao");
            wpPronto = hangarAviao.Find("Pronto");
        }

        if (decolagem != null)
        {
            foreach (Transform filho in decolagem) waypointsDecolagem.Add(filho);
        }

        if (decida != null)
        {
            foreach (Transform filho in decida) waypointsDecida.Add(filho);
            // Como o objeto no Unity está do inicio (Freiada) ao fim (Alinhando)
            // e o avião entra pelo Alinhando, invertemos a lista inteira!
            waypointsDecida.Reverse();
        }

        // Tenta achar Andadar e Analise (em qualquer lugar dentro do Aeroporto)
        Transform[] todasAsTags = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in todasAsTags)
        {
            if (t.name.ToLower() == "andadar") wpAndadar = t;
            if (t.name.ToLower() == "analise") wpAnalise = t;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            menuAtivo = !menuAtivo;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(menuAtivo);
        }

        // Aguarda clique no MAPA para mandar o avião (Radar)
        if (aviaoSelecionadoParaMissao != null && aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
        {
            // Clique Botão Direito
            if (Input.GetMouseButtonDown(1))
            {
                // Tenta impedir clique se mouse estiver em cima de UI nativa da engine (opcional)
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(r, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                    aviaoSelecionadoParaMissao.IniciarMissaoCompleta(hit.point);
                    Debug.Log($"[Aeroporto] Coordenadas recebidas! {aviaoSelecionadoParaMissao.gameObject.name} decolando para: {hit.point}");
                    
                    // Limpa a seleção para não combar acidentalmente depois
                    aviaoSelecionadoParaMissao = null;
                }
            }
        }
    }

    public void ComprarAviao(GameObject prefabDeAeronave)
    {
        Vector3 posSpawn = (wpPreparacao != null) ? wpPreparacao.position : transform.position;
        GameObject aeronaveNascente = Instantiate(prefabDeAeronave, posSpawn, Quaternion.identity);

        ControleAviao controleDaNave = aeronaveNascente.GetComponent<ControleAviao>();
        if (controleDaNave == null) controleDaNave = aeronaveNascente.AddComponent<ControleAviao>();

        controleDaNave.aeroportoOrigem = this;
        StartCoroutine(RotinaRecebimento(controleDaNave));
    }

    private IEnumerator RotinaRecebimento(ControleAviao aviao)
    {
        // Vai devagarzinho do Hangar até a frente do Hangar
        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.MoverInterpolado(wpPronto.position, aviao.velocidadeSolo));
        }

        if (avioesNoPatio.Count < waypointsPatio.Count)
        {
            Transform vagaDesignada = ObterPrimeiraVagaLivre();
            aviao.vagaRetorno = vagaDesignada;
            avioesNoPatio.Add(aviao);
            
            // Vai devagarzinho pra Vaga do Pátio
            yield return StartCoroutine(aviao.MoverInterpolado(vagaDesignada.position, aviao.velocidadeSolo));
            
            aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        }
        else
        {
            avioesNoHangar.Add(aviao);
            aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
            aviao.gameObject.SetActive(false); 
        }
    }

    private Transform ObterPrimeiraVagaLivre()
    {
        avioesNoPatio.RemoveAll(a => a == null); 
        List<Transform> restritas = new List<Transform>();
        foreach (var av in avioesNoPatio) 
            if (av != null && av.vagaRetorno != null) restritas.Add(av.vagaRetorno);

        foreach (var wp in waypointsPatio)
            if (!restritas.Contains(wp)) return wp;
        return null;
    }

    void OnGUI()
    {
        if (!menuAtivo) return;
        if (menuAeroportoUI != null && menuAeroportoUI.activeInHierarchy) return;

        float xMenu = (Screen.width / 2f) - 350f - (Screen.width * 0.1f);
        if (xMenu < 10f) xMenu = 10f; // Previne que o painel saia da tela
        Rect telaDeMenu = new Rect(xMenu, Screen.height / 2f - 250f, 700f, 500f);
        GUI.Box(telaDeMenu, "CENTRO DE CONTROLE TÁTICO & AEROPORTO");

        GUILayout.BeginArea(new Rect(telaDeMenu.x + 15, telaDeMenu.y + 35, telaDeMenu.width - 30, telaDeMenu.height - 45));
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✈️ Aba Comercial", GUILayout.Height(35))) { abaAtual = 0; aviaoSelecionadoParaMissao = null; }
        if (GUILayout.Button("🎖️ Aba Militar", GUILayout.Height(35))) { abaAtual = 1; aviaoSelecionadoParaMissao = null; }
        GUILayout.EndHorizontal();

        GUILayout.Space(25);

        if (abaAtual == 0)
        {
            GUILayout.Label("<size=18><b>OPERAÇÕES COMERCIAIS / LOGÍSTICA</b></size>");
            if(GUILayout.Button("[TESTE] Comprar Avião e Mandar pro Pátio", GUILayout.Height(40)))
            {
                GameObject fakeObj = GameObject.CreatePrimitive(PrimitiveType.Cube); 
                fakeObj.name = "Caça_Comprado";
                fakeObj.transform.localScale = new Vector3(3, 1, 3);
                ComprarAviao(fakeObj);
            }
        }
        else if (abaAtual == 1)
        {
            GUILayout.Label("<size=18><b>FROTA AÉREA E TÁTICA</b></size>");
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical("box", GUILayout.Width(320));
            GUILayout.Label($"<b>PÁTIO FÍSICO ({avioesNoPatio.Count})</b>");
            foreach (var a in avioesNoPatio)
            {
                if (a == null) continue;
                string corEst = (a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio) ? "green" : "red";
                if (GUILayout.Button($"✈️ {a.gameObject.name} [<color={corEst}>{a.estadoAtual}</color>]", GUILayout.Height(30)))
                    aviaoSelecionadoParaMissao = a;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box", GUILayout.Width(320));
            GUILayout.Label($"<b>HANGAR ({avioesNoHangar.Count})</b>");
            for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
            {
                var h = avioesNoHangar[i];
                if (h == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"🔒 {h.gameObject.name}", GUILayout.Width(170));
                if (GUILayout.Button("⮂ TROCAR", GUILayout.Height(25)))
                {
                    if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
                    {
                        TrocarAvioesLogicaGeral(h, aviaoSelecionadoParaMissao);
                        aviaoSelecionadoParaMissao = null; 
                        break; 
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            
            if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
            {
                GUILayout.Label($"<b>PAINEL DE ORDENS: {aviaoSelecionadoParaMissao.gameObject.name}</b>");
                
                if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    if (aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
                    {
                        GUILayout.Label("<color=yellow>⚠️ MODO ALVO ATIVO! Feche o Menu e Clique no mapa com o Botão Direito.</color>");
                        if (GUILayout.Button("❌ Cancelar Ordem", GUILayout.Height(30)))
                        {
                            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                        }
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(40))) ExecutarModoRadar();
                        if (GUILayout.Button("🛡️ Patrulha Aérea", GUILayout.Height(40))) ExecutarModoRadar();
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(40))) ExecutarModoRadar();
                        GUILayout.EndHorizontal();
                    }
                }
                else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                {
                    GUILayout.Label("<color=cyan>Aeronave civil/militar operando no espaço aéreo.</color>");
                    
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("🎯 ALTERAR ALVO/DESTINO", GUILayout.Height(50))) 
                    {
                        ExecutarModoRadar();
                    }

                    if (GUILayout.Button("🔙 ABORTAR E RETORNAR À BASE", GUILayout.Height(50)))
                    {
                        aviaoSelecionadoParaMissao.ComandoRetornarBase();
                        aviaoSelecionadoParaMissao = null;
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                     GUILayout.Label($"<color=orange>Aeronave em trânsito: {aviaoSelecionadoParaMissao.estadoAtual}...</color>");
                     GUI.enabled = false;
                     GUILayout.Button("Aguarde a manobra de pista...", GUILayout.Height(40));
                     GUI.enabled = true;
                }
            }
        }
        GUILayout.EndArea();
    }

    private void ExecutarModoRadar()
    {
        if (aviaoSelecionadoParaMissao != null)
        {
            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
            menuAtivo = false;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
            Debug.Log($"[Aeroporto] Modo Missão Ativado. Fechando painel. Dê a ordem com o clique Direito!");
        }
    }

    private void TrocarAvioesLogicaGeral(ControleAviao modeloSubsaturado, ControleAviao hangarASeAfastar)
    {
        if (hangarASeAfastar.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) return;

        Transform vagaOcupadaLivre = hangarASeAfastar.vagaRetorno;
        Vector3 xyzVaga = hangarASeAfastar.transform.position;
        Quaternion anguloVaga = hangarASeAfastar.transform.rotation;

        avioesNoPatio.Remove(hangarASeAfastar);
        avioesNoHangar.Remove(modeloSubsaturado);

        hangarASeAfastar.gameObject.SetActive(false);
        hangarASeAfastar.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
        hangarASeAfastar.vagaRetorno = null; 
        avioesNoHangar.Add(hangarASeAfastar);

        modeloSubsaturado.gameObject.SetActive(true);
        modeloSubsaturado.transform.position = xyzVaga;
        modeloSubsaturado.transform.rotation = anguloVaga;
        modeloSubsaturado.vagaRetorno = vagaOcupadaLivre; 
        modeloSubsaturado.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        avioesNoPatio.Add(modeloSubsaturado);
    }
}
