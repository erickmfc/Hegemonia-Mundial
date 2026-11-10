using System;
using UnityEngine;
using UnityEngine.UIElements;
using Hegemonia.RTS;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Controlador UI Toolkit para o mini pop-up de identificação de contato de radar.
    /// Exibe dados essenciais em tempo real, diferencia dados de radar do navio vs Boeing E-3,
    /// e permite interrogar, advertir e travar o alvo.
    /// </summary>
    public sealed class ContatoIdentificacaoUIController : MonoBehaviour
    {
        public static ContatoIdentificacaoUIController Instancia { get; private set; }

        [SerializeField] private UIDocument documentoUI;
        [SerializeField] private bool abrirAoIniciarParaTeste = false;

        private VisualElement root;
        private VisualElement popBody;
        private VisualElement colAltitude;
        private Label lblTitulo;
        private Label lblFonteSensor;
        private Label lblPrecisao;
        private Label lblDesignacao;
        private Label lblVelocidade;
        private Label lblDistancia;
        private Label lblAltitude;
        private Label lblFeed;
        private Button btnMinimizar;
        private Button btnFechar;
        private Button btnIdentificar;
        private Button btnRadio;
        private Button btnDisparo;
        private Button btnTravar;

        private IdentidadeUnidade contatoAtual;
        private bool detectadoPorBoeing = false;
        private bool minimizado = false;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(this);
                return;
            }
            Instancia = this;

            if (documentoUI == null)
            {
                documentoUI = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            InicializarUI();
            if (abrirAoIniciarParaTeste)
            {
                AbrirAlertaTeste();
            }
            else
            {
                Ocultar();
            }
        }

        private void InicializarUI()
        {
            if (documentoUI == null) return;
            root = documentoUI.rootVisualElement;
            if (root == null) return;

            popBody = root.Q<VisualElement>("pop-body");
            colAltitude = root.Q<VisualElement>("col-altitude");
            lblTitulo = root.Q<Label>("lbl-titulo");
            lblFonteSensor = root.Q<Label>("lbl-fonte-sensor");
            lblPrecisao = root.Q<Label>("lbl-precisao");
            lblDesignacao = root.Q<Label>("lbl-designacao");
            lblVelocidade = root.Q<Label>("lbl-velocidade");
            lblDistancia = root.Q<Label>("lbl-distancia");
            lblAltitude = root.Q<Label>("lbl-altitude");
            lblFeed = root.Q<Label>("lbl-feed");

            btnMinimizar = root.Q<Button>("btn-minimizar");
            btnFechar = root.Q<Button>("btn-fechar");
            btnIdentificar = root.Q<Button>("btn-identificar");
            btnRadio = root.Q<Button>("btn-radio");
            btnDisparo = root.Q<Button>("btn-disparo");
            btnTravar = root.Q<Button>("btn-travar");

            if (btnMinimizar != null) btnMinimizar.clicked += AlternarMinimizacao;
            if (btnFechar != null) btnFechar.clicked += Ocultar;
            if (btnIdentificar != null) btnIdentificar.clicked += AcaoIdentificar;
            if (btnRadio != null) btnRadio.clicked += AcaoRadio;
            if (btnDisparo != null) btnDisparo.clicked += AcaoDisparoAdvertencia;
            if (btnTravar != null) btnTravar.clicked += AcaoTravarAlvo;
        }

        public void ExibirContato(IdentidadeUnidade alvo, bool viaBoeingE3, float distanciaNmi, float velocidadeKts)
        {
            contatoAtual = alvo;
            detectadoPorBoeing = viaBoeingE3;

            if (root == null) InicializarUI();
            if (root != null) root.style.display = DisplayStyle.Flex;

            if (lblFonteSensor != null)
            {
                lblFonteSensor.text = viaBoeingE3
                    ? "SENSOR: BOEING E-3 SENTRY (AWACS)"
                    : "SENSOR: RADAR DE SUPERFÍCIE (NAVIO)";
            }

            if (lblPrecisao != null)
            {
                lblPrecisao.text = viaBoeingE3
                    ? "TELEMETRIA: DOPPLER ALTA PRECISÃO"
                    : "TELEMETRIA: ESTIMADA (HORIZONTE NAVAL)";
                lblPrecisao.style.color = viaBoeingE3 ? new Color(0.35f, 0.9f, 0.45f) : new Color(1f, 0.75f, 0.2f);
            }

            if (colAltitude != null)
            {
                colAltitude.style.display = viaBoeingE3 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (lblDesignacao != null)
            {
                lblDesignacao.text = alvo != null ? $"ECO #{alvo.GetInstanceID() % 1000:D3} (NÃO IDENTIFICADO)" : "ECO-042 (DESCONHECIDO)";
            }

            if (lblVelocidade != null)
            {
                lblVelocidade.text = viaBoeingE3 ? $"{velocidadeKts:0} KTS (REAL)" : $"~{velocidadeKts:0} KTS (ESTIMADA)";
            }

            if (lblDistancia != null)
            {
                lblDistancia.text = $"{distanciaNmi:0.0} NMI";
            }

            if (lblFeed != null)
            {
                lblFeed.text = "> Contato na área de varredura. Pressione [IDENTIFIQUE-SE] para chamada IFF.";
                lblFeed.style.color = new Color(0.49f, 0.88f, 0.92f);
            }
        }

        public void AbrirAlertaTeste()
        {
            ExibirContato(null, true, 12.4f, 18f);
        }

        public void Ocultar()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
        }

        private void AlternarMinimizacao()
        {
            minimizado = !minimizado;
            if (popBody != null)
            {
                popBody.style.display = minimizado ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (btnMinimizar != null)
            {
                btnMinimizar.text = minimizado ? "□" : "—";
            }
        }

        private void AcaoIdentificar()
        {
            if (SistemaIdentificacaoIncidenteNaval.Instancia != null)
            {
                RespostaInterrogacao resposta = SistemaIdentificacaoIncidenteNaval.Instancia.ProcessarInterrogacao(
                    contatoAtual, out string msg, out Color cor);

                if (lblFeed != null)
                {
                    lblFeed.text = msg;
                    lblFeed.style.color = cor;
                }
            }
            else
            {
                if (lblFeed != null)
                {
                    lblFeed.text = "> Chamada transmitida no Canal 16... Embarcação confirmou: NAVIO COMERCIAL / PETROLEIRO.";
                    lblFeed.style.color = Color.green;
                }
            }
        }

        private void AcaoRadio()
        {
            if (lblFeed != null)
            {
                lblFeed.text = "> Mensagem transmitida na frequência de emergência marítima: 'Altere seu rumo imediatamente'.";
                lblFeed.style.color = new Color(0.4f, 0.85f, 1f);
            }
        }

        private void AcaoDisparoAdvertencia()
        {
            if (lblFeed != null)
            {
                lblFeed.text = "> DISPARO DE ADVERTÊNCIA EFETUADO! O contato reduziu velocidade e iniciou manobra de desvio.";
                lblFeed.style.color = new Color(1f, 0.75f, 0.2f);
            }
        }

        private void AcaoTravarAlvo()
        {
            if (lblFeed != null)
            {
                lblFeed.text = "> [SISTEMA DE ARMAS TRAVADO] ATENÇÃO: Disparar contra navio sem confirmação hostil causará CRISE DIPLOMÁTICA / GUERRA!";
                lblFeed.style.color = new Color(1f, 0.3f, 0.3f);
            }
        }
    }
}
