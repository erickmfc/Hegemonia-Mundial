using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Hegemonia.Menus
{
    public class GerenteDeComandos : MonoBehaviour
    {
        public static GerenteDeComandos Instance;

        [Header("Configurações UI")]
        public Font fontePadrao;
        public Vector2 tamanhoBotao = new Vector2(160, 40);
        public int espacamento = 5;

        private GameObject canvasPrincipal;
        private GameObject painelBotoes;
        private List<GameObject> botoesAtuais = new List<GameObject>();

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // Garante uma fonte
            if (fontePadrao == null) fontePadrao = Font.CreateDynamicFontFromOSFont("Arial", 14);
        }

        void Start()
        {
            VerificarInterface();
        }

        // Chamado para limpar o menu (ex: ao deselecionar unidades)
        public void LimparComandos()
        {
            foreach (GameObject btn in botoesAtuais)
            {
                Destroy(btn);
            }
            botoesAtuais.Clear();
            if (painelBotoes != null) painelBotoes.SetActive(false);
        }

        // Chamado para adicionar um botão de comando específico
        public void RegistrarComando(string titulo, System.Action acaoClique, Color corBotao)
        {
            VerificarInterface();
            painelBotoes.SetActive(true);

            // Cria o Botão
            GameObject btnObj = new GameObject("Btn_" + titulo);
            btnObj.transform.SetParent(painelBotoes.transform, false);

            // Background Image
            Image img = btnObj.AddComponent<Image>();
            img.color = corBotao;

            // Button Component
            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => acaoClique());

            // Layout Element (opcional, mas bom se usar LayoutGroup)
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = tamanhoBotao.y;
            le.preferredHeight = tamanhoBotao.y;

            // Texto
            GameObject txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);
            
            Text txt = txtObj.AddComponent<Text>();
            txt.text = titulo;
            txt.font = fontePadrao;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 14;
            
            RectTransform rtTxt = txt.rectTransform;
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.offsetMin = Vector2.zero; 
            rtTxt.offsetMax = Vector2.zero;

            botoesAtuais.Add(btnObj);
        }

        void VerificarInterface()
        {
            if (canvasPrincipal == null)
            {
                // Tenta achar ou cria
                canvasPrincipal = GameObject.Find("Canvas_Comandos");
                if (canvasPrincipal == null)
                {
                    canvasPrincipal = new GameObject("Canvas_Comandos");
                    Canvas c = canvasPrincipal.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasPrincipal.AddComponent<CanvasScaler>();
                    canvasPrincipal.AddComponent<GraphicRaycaster>();
                }
            }

            if (painelBotoes == null)
            {
                painelBotoes = new GameObject("Painel_Botoes_Comando");
                painelBotoes.transform.SetParent(canvasPrincipal.transform, false);

                RectTransform rt = painelBotoes.AddComponent<RectTransform>();
                // Posiciona no canto superior direito
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-10, -10);
                rt.sizeDelta = new Vector2(tamanhoBotao.x + 20, 0); // Altura auto pelo Content Size Fitter

                // Imagem de fundo simples
                Image img = painelBotoes.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.5f);

                // Organizador Vertical
                VerticalLayoutGroup vlg = painelBotoes.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.spacing = espacamento;
                vlg.childControlHeight = true;
                vlg.childForceExpandHeight = false;

                ContentSizeFitter csf = painelBotoes.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                painelBotoes.SetActive(false);
            }
        }
    }
}
