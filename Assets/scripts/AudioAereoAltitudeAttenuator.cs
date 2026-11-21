using UnityEngine;

/// <summary>
/// Reduz em 50% o volume de fontes aéreas quando a aeronave voa acima de 150 m de altitude.
/// </summary>
public sealed class AudioAereoAltitudeAttenuator : MonoBehaviour
{
    private const float AltitudeLimiteMetros = 150f;
    private const float MultiplicadorAcimaLimite = 0.5f;
    private const float IntervaloAtualizacaoSegundos = 0.15f;

    private AudioSource fonte;
    private float volumeBase = 1f;
    private bool volumeBaseRegistrado;
    private float proximaAtualizacao;
    private bool atenuacaoAtiva;

    public void VincularFonte(AudioSource alvo)
    {
        if (alvo == null) return;
        fonte = alvo;
        if (!volumeBaseRegistrado)
        {
            volumeBase = Mathf.Max(0.01f, fonte.volume);
            volumeBaseRegistrado = true;
        }
        AtualizarVolume(true);
    }

    private void Awake()
    {
        if (fonte == null) fonte = GetComponent<AudioSource>();
        if (fonte != null && !volumeBaseRegistrado)
        {
            volumeBase = Mathf.Max(0.01f, fonte.volume);
            volumeBaseRegistrado = true;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < proximaAtualizacao) return;
        proximaAtualizacao = Time.unscaledTime + IntervaloAtualizacaoSegundos;
        AtualizarVolume(false);
    }

    private void AtualizarVolume(bool forcar)
    {
        if (fonte == null) return;

        float nivelMar = NavalPlacementResolver.ResolveSeaLevel();
        float altitude = transform.position.y - nivelMar;
        bool deveAtenuar = altitude > AltitudeLimiteMetros;

        if (!deveAtenuar && !atenuacaoAtiva)
        {
            volumeBase = fonte.volume;
            return;
        }

        if (forcar || deveAtenuar != atenuacaoAtiva)
        {
            atenuacaoAtiva = deveAtenuar;
            fonte.volume = volumeBase * (atenuacaoAtiva ? MultiplicadorAcimaLimite : 1f);
        }
    }
}
