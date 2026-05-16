using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// O coração da nação. A Prefeitura ou Complexo Governamental.
/// A destruição deste complexo deve resultar no fim do jogo.
/// Este script deve ser anexado ao prédio principal da cidade.
/// </summary>
public class ComplexoGovernamental : MonoBehaviour
{
    private static readonly string[] TermosInvalidosInfraestrutura =
    {
        "plataforma",
        "platform",
        "offshore",
        "petroleo",
        "oil",
        "pier",
        "porto",
        "dock",
        "estaleiro",
        "shipyard"
    };

    [Header("Informações do Estado")]
    public string nomeDoPais = "República Federativa";
    public bool ehDoJogador = true;

    [Header("Sistema de Estado")]
    [Tooltip("Nível de Influência e Poder do Governo")]
    public int nivelDoGoverno = 1;

    [Header("Atalho")]
    public KeyCode teclaAbrirGoverno = KeyCode.X;

    [Header("Eventos")]
    public UnityEvent aoSerDestruido;
    public UnityEvent aoAbrirMenuGestao;

    [Header("Validação da Sede")]
    [Tooltip("Se ativo, o script recusa decretar queda do governo quando estiver anexado em infraestrutura como Plataforma, Pier ou Estaleiro.")]
    public bool validarEstruturaGovernamental = true;
    [Tooltip("Permite forçar este objeto como sede oficial mesmo que o nome pareça genérico.")]
    public bool forcarComoSedeGovernamental = false;

    private IdentidadeUnidade identidade;
    private SistemaDeDanos sistemaDano;
    private ObjetivoFinalJogo objetivoFinal;

    private bool jaDerrotado = false;
    private bool podeDecretarQuedaDoGoverno = true;
    private int ultimoFrameAbertura = -1;

    private void Start()
    {
        identidade = GetComponent<IdentidadeUnidade>();
        sistemaDano = GetComponent<SistemaDeDanos>();

        if (identidade != null)
        {
            ehDoJogador = identidade.teamID == 1;
        }

        AtualizarValidacaoGovernamental();

        if (podeDecretarQuedaDoGoverno && ehDoJogador && SistemaConsulado.Instancia == null)
        {
            gameObject.AddComponent<SistemaConsulado>();
        }

        if (podeDecretarQuedaDoGoverno && ehDoJogador)
        {
            MenuGoverno.GarantirInstancia();
        }
    }

    private void OnDestroy()
    {
        if (sistemaDano != null)
        {
            sistemaDano.OnMorte -= DecretarQuedaDoGoverno;
        }
    }

    private void Update()
    {
        // Nota: a tecla X é tratada globalmente pelo MenuGoverno.Update()
        // Não duplicar aqui para evitar abrir e fechar no mesmo frame

        if (!jaDerrotado && podeDecretarQuedaDoGoverno && sistemaDano != null && sistemaDano.vidaAtual <= 0)
        {
            DecretarQuedaDoGoverno();
        }
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (podeDecretarQuedaDoGoverno && ehDoJogador && !jaDerrotado)
        {
            AbrirMenuGestaoDeEstado();
        }
    }

    public void AbrirMenuGestaoDeEstado()
    {
        if (ultimoFrameAbertura == Time.frameCount)
        {
            return;
        }

        ultimoFrameAbertura = Time.frameCount;

        if (!podeDecretarQuedaDoGoverno || !ehDoJogador || jaDerrotado)
        {
            return;
        }

        Debug.Log($"[Complexo Governamental] Abrindo o painel central da {nomeDoPais}!");

        MenuGoverno.GarantirInstancia();

        aoAbrirMenuGestao?.Invoke();

        if (MenuGoverno.Instancia != null)
        {
            MenuGoverno.Instancia.AlternarMenu(true);
        }
        else
        {
            Debug.LogWarning("[Complexo Governamental] Não foi possível encontrar ou criar o MenuGoverno.");
        }
    }

    public void FecharMenuGestaoDeEstado()
    {
        if (MenuGoverno.Instancia != null)
        {
            MenuGoverno.Instancia.AlternarMenu(false);
        }
    }

    public void AlternarMenuGestaoDeEstado()
    {
        if (!podeDecretarQuedaDoGoverno || !ehDoJogador || jaDerrotado)
        {
            return;
        }

        MenuGoverno.GarantirInstancia();

        if (MenuGoverno.Instancia != null)
        {
            MenuGoverno.Instancia.AlternarMenu(!MenuGoverno.EstaAberto);
        }
    }

    public void DecretarQuedaDoGoverno()
    {
        if (jaDerrotado)
        {
            return;
        }

        if (!podeDecretarQuedaDoGoverno)
        {
            Debug.LogWarning($"[Complexo Governamental] Ignorando queda do governo em '{gameObject.name}' porque o objeto foi classificado como infraestrutura nao governamental.", this);
            return;
        }

        jaDerrotado = true;

        Debug.LogWarning($"[ALERTA MAXIMO] O Complexo Governamental da {nomeDoPais} caiu! Objeto='{gameObject.name}' team={(identidade != null ? identidade.teamID : -1)} vida={(sistemaDano != null ? sistemaDano.vidaAtual.ToString("0.0") : "n/d")}");

        aoSerDestruido?.Invoke();

        if (MenuGoverno.Instancia != null && ehDoJogador)
        {
            MenuGoverno.Instancia.AlternarMenu(false);
        }

        if (objetivoFinal != null)
        {
            objetivoFinal.RegistrarDestruicao();
        }
        else
        {
            SistemaFimDeJogo.RegistrarResultado(
                TipoObjetivoFinal.Prefeitura,
                ehDoJogador,
                nomeDoPais,
                "Prefeitura"
            );
        }

        if (ehDoJogador)
        {
            Debug.LogError("GAME OVER: Você perdeu sua prefeitura principal!");
        }
        else
        {
            Debug.Log("VITÓRIA: Um governo inimigo foi neutralizado!");
        }
    }

    private void AtualizarValidacaoGovernamental()
    {
        podeDecretarQuedaDoGoverno = ResolverSePodeDecretarQuedaDoGoverno();

        objetivoFinal = GetComponent<ObjetivoFinalJogo>();
        if (podeDecretarQuedaDoGoverno)
        {
            if (objetivoFinal == null)
            {
                objetivoFinal = gameObject.AddComponent<ObjetivoFinalJogo>();
            }

            objetivoFinal.Configurar(
                TipoObjetivoFinal.Prefeitura,
                ehDoJogador,
                nomeDoPais,
                "Prefeitura",
                false
            );
        }

        if (sistemaDano != null)
        {
            sistemaDano.OnMorte -= DecretarQuedaDoGoverno;
            if (podeDecretarQuedaDoGoverno)
            {
                sistemaDano.OnMorte += DecretarQuedaDoGoverno;
            }
        }

        if (!podeDecretarQuedaDoGoverno)
        {
            Debug.LogError($"[Complexo Governamental] '{gameObject.name}' recebeu o componente, mas foi bloqueado como sede governamental. Plataforma, Pier e Estaleiro nao podem decretar vitoria/derrota.", this);
        }
    }

    private bool ResolverSePodeDecretarQuedaDoGoverno()
    {
        if (forcarComoSedeGovernamental || !validarEstruturaGovernamental)
        {
            return true;
        }

        string nomeNormalizado = (gameObject.name + " " + nomeDoPais).ToLowerInvariant();
        for (int i = 0; i < TermosInvalidosInfraestrutura.Length; i++)
        {
            if (nomeNormalizado.Contains(TermosInvalidosInfraestrutura[i]))
            {
                return false;
            }
        }

        return true;
    }
}
