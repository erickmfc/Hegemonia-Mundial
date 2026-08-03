using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Playlist simples e persistente. Qualquer AudioClip colocado em
/// Resources/Audio/Musicas passa a ser elegivel sem alterar codigo.
/// </summary>
[DefaultExecutionOrder(-8300)]
public sealed class MusicPlaylistController : MonoBehaviour
{
    public static MusicPlaylistController Instancia { get; private set; }

    private readonly List<AudioClip> faixas = new List<AudioClip>();
    private AudioSource fonte;
    private int ultimaFaixa = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instancia != null)
        {
            return;
        }

        GameObject objeto = new GameObject("MusicPlaylistController");
        objeto.AddComponent<MusicPlaylistController>();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
        CarregarFaixas();
    }

    private void Start()
    {
        CriarFonteSeNecessario();
        TocarProximaFaixa();
    }

    private void Update()
    {
        if (fonte == null || faixas.Count == 0)
        {
            return;
        }

        if (!fonte.isPlaying)
        {
            TocarProximaFaixa();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    public static void RecarregarFaixas()
    {
        if (Instancia == null)
        {
            return;
        }

        Instancia.CarregarFaixas();
        Instancia.CriarFonteSeNecessario();
        if (Instancia.fonte != null && !Instancia.fonte.isPlaying)
        {
            Instancia.TocarProximaFaixa();
        }
    }

    private void CarregarFaixas()
    {
        faixas.Clear();
        AudioClip[] carregadas = Resources.LoadAll<AudioClip>("Audio/Musicas");
        if (carregadas == null)
        {
            return;
        }

        for (int i = 0; i < carregadas.Length; i++)
        {
            if (carregadas[i] != null)
            {
                faixas.Add(carregadas[i]);
            }
        }
    }

    private void CriarFonteSeNecessario()
    {
        if (fonte != null || faixas.Count == 0)
        {
            return;
        }

        fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = false;
        fonte.spatialBlend = 0f;
        fonte.priority = 16;
        AudioSettingsService.RegistrarFonte(fonte, AudioChannel.Musica);
    }

    private void TocarProximaFaixa()
    {
        if (fonte == null || faixas.Count == 0)
        {
            return;
        }

        int indice = faixas.Count == 1
            ? 0
            : UnityEngine.Random.Range(0, faixas.Count);
        if (faixas.Count > 1 && indice == ultimaFaixa)
        {
            indice = (indice + 1) % faixas.Count;
        }

        ultimaFaixa = indice;
        fonte.clip = faixas[indice];
        fonte.Play();
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AudioSettingsService.RegistrarFonte(fonte, AudioChannel.Musica);
    }
}
