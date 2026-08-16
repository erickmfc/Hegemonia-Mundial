using System;
using UnityEngine;

public static class AudioRuntime
{
    private enum Categoria
    {
        Geral,
        Terrestre,
        Armamento,
        Naval,
        Aereo,
        Musica,
        Ambiente,
        Voz
    }

    public static void ConfigurarHierarquia(GameObject raiz)
    {
        if (raiz == null) return;

        AudioSource[] fontes = raiz.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < fontes.Length; i++)
        {
            AudioSource fonte = fontes[i];
            if (fonte == null || fonte.GetComponentInParent<Canvas>() != null || fonte.GetComponentInParent<SomDoMar>() != null)
            {
                continue;
            }

            ConfigurarFonteGenerica(fonte);
        }

        SincronizarMotorAereoComEstado(raiz);
    }

    public static void DefinirMotorAereo(GameObject aeronave, bool ligado)
    {
        if (aeronave == null) return;

        AudioSource[] fontes = aeronave.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < fontes.Length; i++)
        {
            AudioSource fonte = fontes[i];
            if (fonte == null || !EhFonteDeMotor(fonte)) continue;

            fonte.mute = !ligado;
            if (ligado && fonte.clip != null && fonte.loop && !fonte.isPlaying)
            {
                fonte.Play();
            }
            else if (!ligado && fonte.isPlaying)
            {
                fonte.Stop();
            }
        }
    }

    public static void ConfigurarFonteDeArmamento(AudioSource fonte)
    {
        ConfigurarFonte(fonte, Categoria.Armamento);
        if (fonte != null) fonte.volume = 0.8f;
    }

    public static void ConfigurarFonteDeTiro(AudioSource fonte)
    {
        ConfigurarFonteDeArmamento(fonte);
        if (fonte == null) return;
        fonte.maxDistance = 250f;
        fonte.volume = 0.8f;
    }

    public static void ConfigurarFonteDeMissel(AudioSource fonte)
    {
        ConfigurarFonteDeArmamento(fonte);
        if (fonte == null) return;
        fonte.maxDistance = 300f;
        fonte.volume = 0.8f;
    }

    public static void ConfigurarTodasAsFontesDaCena()
    {
        AudioSource[] fontes = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < fontes.Length; i++)
        {
            ConfigurarFonteGenerica(fontes[i]);
        }
    }

    public static void ConfigurarFonteGenerica(AudioSource fonte)
    {
        if (fonte == null)
        {
            return;
        }

        Transform origem = fonte.transform;
        if (origem.GetComponentInParent<SomDoMar>() != null)
        {
            ConfigurarFonte(fonte, Categoria.Ambiente);
            return;
        }

        if (origem.GetComponentInParent<MusicPlaylistController>() != null
            || origem.GetComponentInParent<IntroVideoController>() != null)
        {
            ConfigurarFonte(fonte, Categoria.Musica);
            return;
        }

        // Fontes criadas em runtime por lançadores/projéteis também passam
        // por esta rotina quando o serviço de áudio revalida a cena. Preserve
        // a regra de alcance maior dos mísseis, em vez de rebaixá-las para o
        // limite genérico de 250 m dos tiros convencionais.
        if (EhFonteDeMissel(fonte))
        {
            ConfigurarFonteDeMissel(fonte);
            return;
        }

        ConfigurarFonte(fonte, ResolverCategoria(origem));
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 posicao, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        GameObject objeto = new GameObject("AudioOneShot_" + clip.name);
        objeto.transform.position = posicao;
        AudioSource fonte = objeto.AddComponent<AudioSource>();
        fonte.clip = clip;
        fonte.volume = volume;
        fonte.playOnAwake = false;
        fonte.spatialBlend = 1f;
        fonte.loop = false;
        ConfigurarFonteDeArmamento(fonte);
        fonte.Play();
        UnityEngine.Object.Destroy(objeto, clip.length + 0.1f);
    }

    private static void SincronizarMotorAereoComEstado(GameObject raiz)
    {
        ControleAviao aviao = raiz.GetComponent<ControleAviao>();
        if (aviao != null)
        {
            bool ligado = aviao.estadoAtual != ControleAviao.EstadoAviao.ReservaHangar
                && aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio;
            DefinirMotorAereo(raiz, ligado);
            return;
        }

        C700TransporteAereo transporte = raiz.GetComponent<C700TransporteAereo>();
        if (transporte != null) DefinirMotorAereo(raiz, !transporte.EstaNoSolo);
    }

    private static void ConfigurarFonte(AudioSource fonte, Categoria categoria)
    {
        if (fonte == null) return;

        fonte.spatialBlend = categoria == Categoria.Musica || categoria == Categoria.Ambiente ? 0f : 1f;
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.dopplerLevel = 0f;
        fonte.spread = categoria == Categoria.Aereo ? 55f : 20f;
        fonte.minDistance = ObterDistanciaMinima(categoria);
        fonte.maxDistance = ObterDistanciaMaxima(categoria);
        fonte.priority = Mathf.Min(fonte.priority, ObterPrioridade(categoria));
        if (categoria == Categoria.Armamento)
        {
            fonte.volume = 0.8f;
        }
        AudioSettingsService.RegistrarFonte(fonte, ConverterCategoria(categoria));
    }

    private static AudioChannel ConverterCategoria(Categoria categoria)
    {
        switch (categoria)
        {
            case Categoria.Musica: return AudioChannel.Musica;
            case Categoria.Ambiente: return AudioChannel.Ambiente;
            case Categoria.Voz: return AudioChannel.Voz;
            default: return AudioChannel.Efeitos;
        }
    }

    private static Categoria ResolverCategoria(Transform origem)
    {
        if (origem.GetComponentInParent<ControleAviao>() != null
            || origem.GetComponentInParent<ControleAviaoCaca>() != null
            || origem.GetComponentInParent<ControleAviaoComercial>() != null
            || origem.GetComponentInParent<CacaVooRealista>() != null
            || origem.GetComponentInParent<C700TransporteAereo>() != null
            || origem.GetComponentInParent<Helicoptero>() != null)
        {
            return Categoria.Aereo;
        }

        if (origem.GetComponentInParent<ControleNavioRealista>() != null
            || origem.GetComponentInParent<ControleSubmarino>() != null
            || origem.GetComponentInParent<LancadorNaval>() != null)
        {
            return Categoria.Naval;
        }

        if (origem.GetComponentInParent<SistemaDeTiro>() != null
            || origem.GetComponentInParent<TorretaAntiaerea>() != null
            || origem.GetComponentInParent<ControleTorreta>() != null
            || origem.GetComponentInParent<ControleTorretaModular>() != null
            || origem.GetComponentInParent<SistemaAntiMissil>() != null
            || origem.GetComponentInParent<LancadorMLRS>() != null)
        {
            return Categoria.Armamento;
        }

        return origem.GetComponentInParent<ControleUnidade>() != null ? Categoria.Terrestre : Categoria.Geral;
    }

    private static bool EhFonteDeMotor(AudioSource fonte)
    {
        if (!fonte.loop) return false;
        string texto = ((fonte.gameObject.name ?? string.Empty) + " "
            + (fonte.clip != null ? fonte.clip.name : string.Empty)).ToLowerInvariant();
        return texto.Contains("motor") || texto.Contains("engine") || texto.Contains("turbina")
            || texto.Contains("helice") || texto.Contains("rotor") || texto.Contains("jato") || texto.Contains("jet");
    }

    private static bool EhFonteDeMissel(AudioSource fonte)
    {
        Transform origem = fonte != null ? fonte.transform : null;
        if (origem == null) return false;
        if (EhFonteDeMotor(fonte)) return false;

        return origem.GetComponentInParent<LancadorNaval>() != null
            || origem.GetComponentInParent<SistemaAntiMissil>() != null
            || origem.GetComponentInParent<LancadorMLRS>() != null
            || origem.GetComponentInParent<LancadorMisseis>() != null
            || origem.GetComponentInParent<LancadorMisselCaca>() != null
            || origem.GetComponentInParent<MisselNaval>() != null
            || origem.GetComponentInParent<MisselSubmarino>() != null
            || origem.GetComponentInParent<MisselCaca>() != null
            || origem.GetComponentInParent<MisselEstrategicoLongoAlcance>() != null
            || origem.GetComponentInParent<MisselICBM>() != null
            || origem.GetComponentInParent<MisselLeopardAutomatico>() != null;
    }

    private static float ObterDistanciaMaxima(Categoria categoria)
    {
        switch (categoria)
        {
            case Categoria.Aereo: return 150f;
            case Categoria.Naval: return 250f;
            case Categoria.Armamento: return 250f;
            case Categoria.Terrestre: return 50f;
            default: return 50f;
        }
    }

    private static float ObterDistanciaMinima(Categoria categoria)
    {
        return Mathf.Clamp(ObterDistanciaMaxima(categoria) * 0.06f, 3f, 9f);
    }

    private static int ObterPrioridade(Categoria categoria)
    {
        switch (categoria)
        {
            case Categoria.Armamento: return 40;
            case Categoria.Aereo: return 48;
            case Categoria.Naval: return 56;
            case Categoria.Terrestre: return 72;
            default: return 96;
        }
    }
}

[Obsolete("Use AudioRuntime. Este componente existe apenas para limpar cenas antigas.")]
[DisallowMultipleComponent]
public sealed class GerenciadorAudio3DGlobal : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
        Destroy(this);
    }
}
