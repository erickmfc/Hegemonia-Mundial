using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// -----------------------------------------------------
// CLASSE DE DADOS (Esta classe define O QUE será salvo)
// -----------------------------------------------------
[System.Serializable]
public class DadosDoJogo
{
    public int creditosJogador;
    public string mapaAtual;
    
    // Você pode armazenar desbloqueios, lista de unidades, etc.
    public List<string> itensDesbloqueados = new List<string>();

    // Posição de câmera ou de um Quartel General (QG)
    // Usamos float para não ter problemas de conversão em algumas versões da Unity
    public float qgPosX;
    public float qgPosY;
    public float qgPosZ;

    // Construtor inicial (O que acontece quando o cara inicia do ZERO)
    public DadosDoJogo()
    {
        creditosJogador = 5000; // Começa com 5000 por exemplo
        mapaAtual = "Mapa_1";
        itensDesbloqueados = new List<string>();
    }
}

// -----------------------------------------------------
// GERENCIADOR DE SAVE (Controla como o Arquivo é salvo)
// -----------------------------------------------------
public class SistemaSaveGame : MonoBehaviour
{
    public static SistemaSaveGame Instancia;

    public DadosDoJogo dadosAtuais;
    public bool exibirLogsNoConsole = false;

    private string caminhoDoArquivo;

    public static SistemaSaveGame GarantirInstancia()
    {
        if (Instancia != null)
        {
            return Instancia;
        }

        GameObject objetoSave = new GameObject("SistemaSaveGame");
        return objetoSave.AddComponent<SistemaSaveGame>();
    }

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        caminhoDoArquivo = Path.Combine(Application.persistentDataPath, "save_partida.json");
        InicializarDados();
    }

    private void InicializarDados()
    {
        if (PossuiSave())
        {
            CarregarJogo();
            return;
        }

        dadosAtuais = new DadosDoJogo();
    }

    public bool PossuiSave()
    {
        return File.Exists(caminhoDoArquivo);
    }

    public void SalvarJogo()
    {
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
        }

        RegistrarCenaAtual(SceneManager.GetActiveScene().name);

        string json = JsonUtility.ToJson(dadosAtuais, true);
        File.WriteAllText(caminhoDoArquivo, json);
        LogInfo("💾 Jogo salvo com sucesso em: " + caminhoDoArquivo);
    }

    public void CarregarJogo()
    {
        if (PossuiSave())
        {
            string json = File.ReadAllText(caminhoDoArquivo);
            dadosAtuais = JsonUtility.FromJson<DadosDoJogo>(json);
            if (dadosAtuais == null)
            {
                dadosAtuais = new DadosDoJogo();
            }
            LogInfo("📂 Jogo carregado com sucesso!");
        }
        else
        {
            dadosAtuais = new DadosDoJogo();
        }
    }

    public void IniciarNovoJogo(string cenaInicial = null)
    {
        dadosAtuais = new DadosDoJogo();
        RegistrarCenaAtual(string.IsNullOrWhiteSpace(cenaInicial) ? dadosAtuais.mapaAtual : cenaInicial);
        LogInfo("🆕 Novo jogo iniciado com dados reiniciados.");
    }

    public bool TentarCarregarJogo()
    {
        if (!PossuiSave())
        {
            return false;
        }

        CarregarJogo();
        return true;
    }

    public void RegistrarCenaAtual(string nomeCena)
    {
        if (dadosAtuais == null)
        {
            dadosAtuais = new DadosDoJogo();
        }

        if (!string.IsNullOrWhiteSpace(nomeCena))
        {
            dadosAtuais.mapaAtual = nomeCena;
        }
    }

    public string ObterCenaSalvaOuPadrao(string cenaPadrao)
    {
        if (dadosAtuais == null || string.IsNullOrWhiteSpace(dadosAtuais.mapaAtual))
        {
            return cenaPadrao;
        }

        return dadosAtuais.mapaAtual;
    }

    public void ApagarDados()
    {
        if (PossuiSave())
        {
            File.Delete(caminhoDoArquivo);
            dadosAtuais = new DadosDoJogo();
            LogInfo("🗑️ Save apagado do computador. Começando do zero!");
        }
        else
        {
            LogWarning("Ouve uma tentativa de apagar um save, mas não exisia nenhum arquivo.");
        }
    }

    private void LogInfo(string mensagem)
    {
        if (exibirLogsNoConsole)
        {
            Debug.Log(mensagem);
        }
    }

    private void LogWarning(string mensagem)
    {
        if (exibirLogsNoConsole)
        {
            Debug.LogWarning(mensagem);
        }
    }

    [ContextMenu("Forçar Salvar Jogo")]
    private void TesteSalvar() { SalvarJogo(); }

    [ContextMenu("Forçar Carregar Jogo")]
    private void TesteCarregar() { CarregarJogo(); }

    [ContextMenu("Forçar Apagar Save (Danger)")]
    private void TesteApagar() { ApagarDados(); }
}
