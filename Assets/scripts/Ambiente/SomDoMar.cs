using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SomDoMar : MonoBehaviour
{
    public Transform cameraPrincipal;
    public AudioClip clipeDoMar;
    public float nivelDaAgua = 0f;
    public float alturaMinima = 5f;
    public float alturaMaxima = 80f;

    [Range(0f, 1f)]
    public float volumeGeral = 0.8f;
    public bool tocarAutomaticamente = true;
    public bool exigirAguaNaCamera = true;
    public float margemAgua = 2f;

    private AudioSource fonte;
    private static AudioSource fonteAmbienteAtiva;

    private void Awake()
    {
        fonte = GetComponent<AudioSource>();
        if (clipeDoMar != null) fonte.clip = clipeDoMar;
        fonte.loop = true;
        fonte.playOnAwake = false;
        fonte.spatialBlend = 0f;
        fonte.dopplerLevel = 0f;
        fonte.priority = Mathf.Min(fonte.priority, 96);
        AudioSettingsService.RegistrarFonte(fonte, AudioChannel.Ambiente);

        // A cena MD História possui mais de um tile de água. O som é 2D e
        // global, portanto tocar uma fonte por tile duplica o volume e pode
        // causar eco. Mantém apenas uma fonte ambiental ativa.
        if (fonteAmbienteAtiva == null)
        {
            fonteAmbienteAtiva = fonte;
        }
        else if (fonteAmbienteAtiva != fonte)
        {
            fonte.enabled = false;
            fonte.volume = 0f;
        }
    }

    private void OnEnable()
    {
        CameraController.CameraMudouArea += AoMudarAreaDaCamera;
        if (cameraPrincipal == null)
        {
            CameraController controlador = FindFirstObjectByType<CameraController>();
            if (controlador != null) cameraPrincipal = controlador.transform;
        }
        if (cameraPrincipal != null) AoMudarAreaDaCamera(cameraPrincipal.position);
        GarantirReproducao();
    }

    private void OnDisable()
    {
        CameraController.CameraMudouArea -= AoMudarAreaDaCamera;
        if (fonteAmbienteAtiva == fonte)
        {
            fonteAmbienteAtiva = null;
        }
    }

    private void AoMudarAreaDaCamera(Vector3 posicao)
    {
        if (fonte == null) return;
        if (exigirAguaNaCamera && !RegistroSuperficieMapa.TryGetAltura(posicao, TipoSuperficieMapa.Agua, out _, margemAgua))
        {
            fonte.volume = 0f;
            return;
        }
        float alturaSobreAgua = Mathf.Max(0f, posicao.y - nivelDaAgua);
        float fatorAltura = Mathf.InverseLerp(alturaMaxima, alturaMinima, alturaSobreAgua);
        fonte.volume = fatorAltura * fatorAltura * volumeGeral;
    }

    private void GarantirReproducao()
    {
        if (fonte == null) return;
        // Uma fonte pode ficar desabilitada quando o objeto de ambiente é
        // desativado por uma cena/painel. Play() nesse estado gera spam no
        // console e não produz som; aguarde o próximo OnEnable.
        if (!fonte.enabled || !fonte.gameObject.activeInHierarchy) return;
        if (fonte.clip == null && clipeDoMar != null) fonte.clip = clipeDoMar;
        if (tocarAutomaticamente && fonte.clip != null && !fonte.isPlaying) fonte.Play();
    }
}
