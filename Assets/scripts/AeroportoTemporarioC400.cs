using UnityEngine;

/// <summary>
/// Controla o prazo do aeroporto temporario usando o calendario do jogo.
/// A selecao da estrutura abre uma interface simples para ajustar a duracao.
/// </summary>
[DisallowMultipleComponent]
public sealed class AeroportoTemporarioC400 : MonoBehaviour
{
    [Min(1)] public int diasAteDestruir = 10;
    public bool prazoAtivo = true;
    public int diaExpiracao;

    private string campoDias = "10";
    private bool prazoInicializado;

    public int DiasRestantes
    {
        get
        {
            GerenciadorTempo tempo = GerenciadorTempo.Instancia;
            return tempo == null || !prazoAtivo ? Mathf.Max(0, diasAteDestruir) : Mathf.Max(0, diaExpiracao - tempo.totalDias);
        }
    }

    private void Start()
    {
        GerenciadorTempo.GarantirInstancia();
        if (!prazoInicializado)
        {
            diaExpiracao = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) + Mathf.Max(1, diasAteDestruir);
            prazoAtivo = true;
            prazoInicializado = true;
        }
        campoDias = Mathf.Max(1, DiasRestantes).ToString();
        InscreverNoCalendario();
        VerificarExpiracao();
    }

    private void OnEnable()
    {
        if (GerenciadorTempo.Instancia != null) InscreverNoCalendario();
    }

    private void OnDisable()
    {
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada -= VerificarExpiracao;
    }

    public void DefinirPrazoEmDias(int dias)
    {
        diasAteDestruir = Mathf.Clamp(dias, 1, 365);
        GerenciadorTempo.GarantirInstancia();
        diaExpiracao = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) + diasAteDestruir;
        prazoAtivo = true;
        prazoInicializado = true;
        campoDias = diasAteDestruir.ToString();
        VerificarExpiracao();
    }

    public void RestaurarPrazo(bool ativo, int diasConfigurados, int diaFim)
    {
        diasAteDestruir = Mathf.Clamp(diasConfigurados, 1, 365);
        prazoAtivo = ativo;
        diaExpiracao = Mathf.Max(1, diaFim);
        prazoInicializado = ativo;
        campoDias = Mathf.Max(1, diasAteDestruir).ToString();
    }

    private void InscreverNoCalendario()
    {
        if (GerenciadorTempo.Instancia == null) return;
        GerenciadorTempo.Instancia.OnDataAlterada -= VerificarExpiracao;
        GerenciadorTempo.Instancia.OnDataAlterada += VerificarExpiracao;
    }

    private void VerificarExpiracao()
    {
        GerenciadorTempo tempo = GerenciadorTempo.Instancia;
        if (!prazoAtivo || tempo == null || tempo.totalDias < diaExpiracao) return;
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle == null || !controle.selecionado || !enabled) return;

        Rect area = new Rect(24f, Screen.height - 238f, 310f, 208f);
        GUILayout.BeginArea(area, "AEROPORTO TEMPORARIO", GUI.skin.window);
        GUILayout.Label("Prazo configuravel pelo calendario do jogo.");
        GUILayout.Label(prazoAtivo ? "Restam " + DiasRestantes + " dia(s)." : "Prazo pausado.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-1")) campoDias = Mathf.Max(1, DiasDoCampo() - 1).ToString();
        campoDias = GUILayout.TextField(campoDias, GUILayout.MinWidth(80f));
        if (GUILayout.Button("+1")) campoDias = Mathf.Min(365, DiasDoCampo() + 1).ToString();
        GUILayout.EndHorizontal();
        GUILayout.Label("Dias a partir de hoje (1 a 365)");
        if (GUILayout.Button("APLICAR PRAZO")) DefinirPrazoEmDias(DiasDoCampo());
        GUILayout.EndArea();
    }

    private int DiasDoCampo()
    {
        return int.TryParse(campoDias, out int dias) ? Mathf.Clamp(dias, 1, 365) : Mathf.Max(1, diasAteDestruir);
    }
}
