using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace Hegemonia.UI
{
    public class GerenciadorAlertasUI : MonoBehaviour
    {
        public static GerenciadorAlertasUI Instancia { get; private set; }

        [Header("Configurações do Alerta")]
        public float velocidadeEscrita = 0.03f;
        public Color corPadraoAlerta = new Color(1f, 0.2f, 0.2f); // Vermelho militar
        public float duracaoExibicao = 5f;

        private Canvas _canvasObj;
        private RectTransform _painelAlerta;
        private TextMeshProUGUI _textoAlerta;
        private Queue<AlertaInfo> _filaAlertas = new Queue<AlertaInfo>();
        private bool _exibindoAlerta = false;

        private struct AlertaInfo
        {
            public string mensagem;
            public Color cor;
            public float duracao;
        }

        private void Awake()
        {
            if (Instancia == null)
            {
                Instancia = this;
                DontDestroyOnLoad(gameObject);
                InicializarUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Exibe um alerta na tela. Adiciona à fila se já houver um alerta rodando.
        /// </summary>
        public void MostrarAlerta(string mensagem, Color cor, float duracao = 5f)
        {
            _filaAlertas.Enqueue(new AlertaInfo { mensagem = mensagem, cor = cor, duracao = duracao });
            if (!_exibindoAlerta)
            {
                StartCoroutine(ProcessarFilaAlertas());
            }
        }

        private IEnumerator ProcessarFilaAlertas()
        {
            _exibindoAlerta = true;

            while (_filaAlertas.Count > 0)
            {
                AlertaInfo alerta = _filaAlertas.Dequeue();
                yield return StartCoroutine(ExibirAlertaRotina(alerta.mensagem, alerta.cor, alerta.duracao));
            }

            _exibindoAlerta = false;
        }

        private IEnumerator ExibirAlertaRotina(string msg, Color cor, float duracao)
        {
            // Garantir que a UI está criada
            if (_textoAlerta == null) InicializarUI();

            _painelAlerta.gameObject.SetActive(true);
            _textoAlerta.color = cor;
            _textoAlerta.text = "";

            // Efeito de digitação (typewriter)
            string textoCompleto = ":: SYSTEM NOTICE ::\n" + msg.ToUpper();
            for (int i = 0; i <= textoCompleto.Length; i++)
            {
                _textoAlerta.text = textoCompleto.Substring(0, i);
                yield return new WaitForSeconds(velocidadeEscrita);
            }

            // Aguarda o tempo de exibição
            yield return new WaitForSeconds(duracao);

            // Fade out simples (opcional: piscar no final)
            float elapsed = 0f;
            float fadeTime = 0.5f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                _textoAlerta.color = new Color(cor.r, cor.g, cor.b, alpha);
                yield return null;
            }

            _painelAlerta.gameObject.SetActive(false);
        }

        private void InicializarUI()
        {
            // Tenta achar um Canvas existente, se não, cria um
            Canvas existingCanvas = FindFirstObjectByType<Canvas>();
            GameObject canvasGO;

            if (existingCanvas != null)
            {
                canvasGO = existingCanvas.gameObject;
            }
            else
            {
                canvasGO = new GameObject("AlertaCanvas");
                _canvasObj = canvasGO.AddComponent<Canvas>();
                _canvasObj.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Criar Painel de Fundo do Alerta
            GameObject panelGO = new GameObject("PainelAlertaAltoComando");
            panelGO.transform.SetParent(canvasGO.transform, false);
            _painelAlerta = panelGO.AddComponent<RectTransform>();

            // Posicionamento no topo centralizado
            _painelAlerta.anchorMin = new Vector2(0.5f, 1f);
            _painelAlerta.anchorMax = new Vector2(0.5f, 1f);
            _painelAlerta.pivot = new Vector2(0.5f, 1f);
            _painelAlerta.anchoredPosition = new Vector2(0f, -60f); // 60 pixels abaixo do topo
            _painelAlerta.sizeDelta = new Vector2(650f, 100f);

            // Adicionar imagem de fundo translúcida (estilo militar escuro)
            var img = panelGO.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.02f, 0.05f, 0.1f, 0.85f); // Azul escuro translúcido

            // Borda ciano fina
            var outline = panelGO.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.16f, 0.65f, 0.88f, 0.5f); // Ciano
            outline.effectDistance = new Vector2(1f, -1f);

            // Criar o Texto com TMPro
            GameObject textGO = new GameObject("TextoAlerta");
            textGO.transform.SetParent(panelGO.transform, false);
            _textoAlerta = textGO.AddComponent<TextMeshProUGUI>();

            RectTransform textRect = _textoAlerta.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20f, -20f); // Margem interna de 10px

            _textoAlerta.fontSize = 13f;
            _textoAlerta.alignment = TextAlignmentOptions.Center;
            _textoAlerta.fontStyle = FontStyles.Bold;
            _textoAlerta.textWrappingMode = TextWrappingModes.Normal;

            panelGO.SetActive(false);
        }
    }
}
