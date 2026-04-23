using UnityEngine;

public enum TipoObjetivoFinal
{
    Prefeitura,
    Presidente
}

[DisallowMultipleComponent]
public class ObjetivoFinalJogo : MonoBehaviour
{
    [Header("Configuração do Alvo")]
    [SerializeField] private TipoObjetivoFinal tipoObjetivo = TipoObjetivoFinal.Prefeitura;
    [SerializeField] private bool pertenceAoJogador = true;
    [SerializeField] private string nomeDaNacao = "República Federativa";
    [SerializeField] private string nomeExibicaoObjetivo = string.Empty;
    [SerializeField] private bool escutaMorteAutomatica = false;

    private SistemaDeDanos sistemaDano;
    private bool destruicaoRegistrada;

    private void Awake()
    {
        sistemaDano = GetComponent<SistemaDeDanos>();
        AtualizarAssinatura();
    }

    private void OnEnable()
    {
        AtualizarAssinatura();
    }

    private void OnDisable()
    {
        Desassinar();
    }

    private void OnDestroy()
    {
        Desassinar();
    }

    public void Configurar(TipoObjetivoFinal novoTipo, bool alvoPertenceAoJogador, string novaNacao, string novoNomeExibicao = null, bool autoEscutaMorte = false)
    {
        tipoObjetivo = novoTipo;
        pertenceAoJogador = alvoPertenceAoJogador;
        escutaMorteAutomatica = autoEscutaMorte;
        destruicaoRegistrada = false;

        if (!string.IsNullOrWhiteSpace(novaNacao))
        {
            nomeDaNacao = novaNacao;
        }

        if (!string.IsNullOrWhiteSpace(novoNomeExibicao))
        {
            nomeExibicaoObjetivo = novoNomeExibicao;
        }

        AtualizarAssinatura();
    }

    public void RegistrarDestruicao()
    {
        if (destruicaoRegistrada || SistemaFimDeJogo.PartidaEncerrada)
        {
            return;
        }

        destruicaoRegistrada = true;

        string nomeObjetivo = string.IsNullOrWhiteSpace(nomeExibicaoObjetivo)
            ? tipoObjetivo.ToString()
            : nomeExibicaoObjetivo.Trim();

        SistemaFimDeJogo.RegistrarResultado(tipoObjetivo, pertenceAoJogador, nomeDaNacao, nomeObjetivo);
    }

    private void AtualizarAssinatura()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (sistemaDano == null)
        {
            sistemaDano = GetComponent<SistemaDeDanos>();
        }

        if (sistemaDano == null)
        {
            return;
        }

        sistemaDano.OnMorte -= RegistrarDestruicao;

        if (escutaMorteAutomatica)
        {
            sistemaDano.OnMorte += RegistrarDestruicao;
        }
    }

    private void Desassinar()
    {
        if (sistemaDano != null)
        {
            sistemaDano.OnMorte -= RegistrarDestruicao;
        }
    }
}
