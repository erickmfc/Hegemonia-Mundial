using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum AudioChannel
{
    Geral,
    Musica,
    Efeitos,
    Ambiente,
    Voz
}

/// <summary>
/// Preferencias de audio persistentes e roteamento das fontes para os canais
/// Geral/Musica/Efeitos/Ambiente/Voz. Se o mixer ainda nao estiver disponivel,
/// existe um fallback seguro usando AudioListener e volume das fontes.
/// </summary>
[DefaultExecutionOrder(-8400)]
public sealed class AudioSettingsService : MonoBehaviour
{
    public static AudioSettingsService Instancia { get; private set; }

    private const string PrefPrefix = "hegemonia.audio.";
    private const float VolumePadrao = 1f;
    private const float DbSilencio = -80f;

    private readonly Dictionary<AudioChannel, float> volumes = new Dictionary<AudioChannel, float>();
    private readonly Dictionary<AudioChannel, bool> silenciados = new Dictionary<AudioChannel, bool>();
    private readonly Dictionary<AudioSource, AudioChannel> fontes = new Dictionary<AudioSource, AudioChannel>();
    private readonly Dictionary<AudioSource, float> volumesBaseFallback = new Dictionary<AudioSource, float>();
    private readonly Dictionary<AudioChannel, string> parametrosMixer = new Dictionary<AudioChannel, string>
    {
        { AudioChannel.Geral, "hegemonia_volume_geral" },
        { AudioChannel.Musica, "hegemonia_volume_musica" },
        { AudioChannel.Efeitos, "hegemonia_volume_efeitos" },
        { AudioChannel.Ambiente, "hegemonia_volume_ambiente" },
        { AudioChannel.Voz, "hegemonia_volume_voz" }
    };

    private AudioMixer mixer;
    private readonly Dictionary<AudioChannel, AudioMixerGroup> gruposMixer = new Dictionary<AudioChannel, AudioMixerGroup>();
    private float proximaVarredura;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GarantirInstancia();
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
        InicializarValores();
        CarregarMixer();
        SceneManager.sceneLoaded += AoCarregarCena;
        AplicarTodosOsVolumes();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < proximaVarredura)
        {
            return;
        }

        proximaVarredura = Time.unscaledTime + 2f;
        AudioRuntime.ConfigurarTodasAsFontesDaCena();
        LimparFontesDestruidas();
    }

    public static void RegistrarFonte(AudioSource fonte, AudioChannel canal)
    {
        if (fonte == null)
        {
            return;
        }

        AudioSettingsService servico = GarantirInstancia();
        servico.RegistrarFonteInterno(fonte, canal);
    }

    public static AudioMixerGroup ObterGrupo(AudioChannel canal)
    {
        AudioSettingsService servico = GarantirInstancia();
        if (servico == null)
        {
            return null;
        }

        servico.gruposMixer.TryGetValue(canal, out AudioMixerGroup grupo);
        return grupo;
    }

    public static float ObterVolume(AudioChannel canal)
    {
        AudioSettingsService servico = GarantirInstancia();
        return servico != null && servico.volumes.TryGetValue(canal, out float valor) ? valor : VolumePadrao;
    }

    public static bool EstaSilenciado(AudioChannel canal)
    {
        AudioSettingsService servico = GarantirInstancia();
        return servico != null && servico.silenciados.TryGetValue(canal, out bool silenciado) && silenciado;
    }

    public static void DefinirVolume(AudioChannel canal, float valor)
    {
        AudioSettingsService servico = GarantirInstancia();
        if (servico == null)
        {
            return;
        }

        servico.volumes[canal] = Mathf.Clamp01(valor);
        PlayerPrefs.SetFloat(PrefPrefix + canal + ".volume", servico.volumes[canal]);
        PlayerPrefs.Save();
        servico.AplicarVolume(canal);
    }

    public static void DefinirSilenciado(AudioChannel canal, bool silenciado)
    {
        AudioSettingsService servico = GarantirInstancia();
        if (servico == null)
        {
            return;
        }

        servico.silenciados[canal] = silenciado;
        PlayerPrefs.SetInt(PrefPrefix + canal + ".muted", silenciado ? 1 : 0);
        PlayerPrefs.Save();
        servico.AplicarVolume(canal);
    }

    public static void RestaurarPadroes()
    {
        AudioSettingsService servico = GarantirInstancia();
        if (servico == null)
        {
            return;
        }

        foreach (AudioChannel canal in System.Enum.GetValues(typeof(AudioChannel)))
        {
            servico.volumes[canal] = VolumePadrao;
            servico.silenciados[canal] = false;
            PlayerPrefs.DeleteKey(PrefPrefix + canal + ".volume");
            PlayerPrefs.DeleteKey(PrefPrefix + canal + ".muted");
        }

        PlayerPrefs.Save();
        servico.AplicarTodosOsVolumes();
    }

    private static AudioSettingsService GarantirInstancia()
    {
        if (Instancia != null)
        {
            return Instancia;
        }

        AudioSettingsService encontrada = FindFirstObjectByType<AudioSettingsService>();
        if (encontrada != null)
        {
            Instancia = encontrada;
            return encontrada;
        }

        GameObject objeto = new GameObject("AudioSettingsService");
        Instancia = objeto.AddComponent<AudioSettingsService>();
        return Instancia;
    }

    private void InicializarValores()
    {
        foreach (AudioChannel canal in System.Enum.GetValues(typeof(AudioChannel)))
        {
            volumes[canal] = PlayerPrefs.GetFloat(PrefPrefix + canal + ".volume", VolumePadrao);
            silenciados[canal] = PlayerPrefs.GetInt(PrefPrefix + canal + ".muted", 0) != 0;
        }
    }

    private void CarregarMixer()
    {
        mixer = Resources.Load<AudioMixer>("Audio/HegemoniaAudioMixer");
        if (mixer == null)
        {
            Debug.LogWarning("[Audio] HegemoniaAudioMixer nao encontrado; usando fallback de volume.");
            return;
        }

        RegistrarGrupo(AudioChannel.Geral, "Master");
        RegistrarGrupo(AudioChannel.Musica, "Musica");
        RegistrarGrupo(AudioChannel.Efeitos, "Efeitos");
        RegistrarGrupo(AudioChannel.Ambiente, "Ambiente");
        RegistrarGrupo(AudioChannel.Voz, "Voz");
    }

    private void RegistrarGrupo(AudioChannel canal, string nome)
    {
        AudioMixerGroup[] grupos = mixer.FindMatchingGroups(nome);
        if (grupos != null && grupos.Length > 0)
        {
            gruposMixer[canal] = grupos[0];
        }
    }

    private void RegistrarFonteInterno(AudioSource fonte, AudioChannel canal)
    {
        if (fonte == null)
        {
            return;
        }

        bool jaRegistrada = fontes.ContainsKey(fonte);
        fontes[fonte] = canal;
        if (!jaRegistrada && !volumesBaseFallback.ContainsKey(fonte))
        {
            volumesBaseFallback[fonte] = fonte.volume;
        }

        if (gruposMixer.TryGetValue(canal, out AudioMixerGroup grupo) && grupo != null)
        {
            fonte.outputAudioMixerGroup = grupo;
        }
        else if (!mixer)
        {
            fonte.volume = volumesBaseFallback[fonte] * ObterFator(canal);
        }
    }

    private void AplicarTodosOsVolumes()
    {
        foreach (AudioChannel canal in System.Enum.GetValues(typeof(AudioChannel)))
        {
            AplicarVolume(canal);
        }
    }

    private void AplicarVolume(AudioChannel canal)
    {
        float valor = ObterFator(canal);
        if (mixer != null && parametrosMixer.TryGetValue(canal, out string parametro))
        {
            mixer.SetFloat(parametro, LinearParaDecibeis(valor));
        }

        if (canal == AudioChannel.Geral && mixer == null)
        {
            AudioListener.volume = valor;
        }

        if (mixer == null)
        {
            foreach (KeyValuePair<AudioSource, AudioChannel> item in fontes)
            {
                if (item.Key == null || item.Value != canal)
                {
                    continue;
                }

                if (volumesBaseFallback.TryGetValue(item.Key, out float baseVolume))
                {
                    item.Key.volume = baseVolume * valor;
                }
            }
        }
    }

    private float ObterFator(AudioChannel canal)
    {
        float volume = volumes.TryGetValue(canal, out float valor) ? valor : VolumePadrao;
        bool mutado = silenciados.TryGetValue(canal, out bool silenciado) && silenciado;
        return mutado ? 0f : Mathf.Clamp01(volume);
    }

    private static float LinearParaDecibeis(float valor)
    {
        return valor <= 0.0001f ? DbSilencio : Mathf.Clamp(20f * Mathf.Log10(valor), DbSilencio, 0f);
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        proximaVarredura = 0f;
        StartCoroutine(RegistrarFontesDepoisDaCena());
    }

    private System.Collections.IEnumerator RegistrarFontesDepoisDaCena()
    {
        yield return null;
        AudioRuntime.ConfigurarTodasAsFontesDaCena();
    }

    private void LimparFontesDestruidas()
    {
        List<AudioSource> remover = null;
        foreach (AudioSource fonte in fontes.Keys)
        {
            if (fonte != null)
            {
                continue;
            }

            remover ??= new List<AudioSource>();
            remover.Add(fonte);
        }

        if (remover == null)
        {
            return;
        }

        for (int i = 0; i < remover.Count; i++)
        {
            fontes.Remove(remover[i]);
            volumesBaseFallback.Remove(remover[i]);
        }
    }
}
