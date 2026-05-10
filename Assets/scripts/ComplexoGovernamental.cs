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

    private IdentidadeUnidade identidade;
    private SistemaDeDanos sistemaDano;
    private ObjetivoFinalJogo objetivoFinal;

    private bool jaDerrotado = false;
    private int ultimoFrameAbertura = -1;

    private void Start()
    {
        identidade = GetComponent<IdentidadeUnidade>();
        sistemaDano = GetComponent<SistemaDeDanos>();

        if (identidade != null)
        {
            ehDoJogador = identidade.teamID == 1;
        }

        objetivoFinal = GetComponent<ObjetivoFinalJogo>();

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

        if (sistemaDano != null)
        {
            sistemaDano.OnMorte -= DecretarQuedaDoGoverno;
            sistemaDano.OnMorte += DecretarQuedaDoGoverno;
        }

        if (ehDoJogador && SistemaConsulado.Instancia == null)
        {
            gameObject.AddComponent<SistemaConsulado>();
        }

        if (ehDoJogador)
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

        if (!jaDerrotado && sistemaDano != null && sistemaDano.vidaAtual <= 0)
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

        if (ehDoJogador && !jaDerrotado)
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

        if (!ehDoJogador || jaDerrotado)
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
        if (!ehDoJogador || jaDerrotado)
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

        jaDerrotado = true;

        Debug.LogWarning($"[ALERTA MAXIMO] O Complexo Governamental da {nomeDoPais} caiu! O governo ruiu!");

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
}