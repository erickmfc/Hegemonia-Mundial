using UnityEngine;

/// <summary>
/// Efeitos de propulsao dos misseis Classe S. Mantem os sistemas desligados
/// enquanto a municao esta guardada no pool e limpa o rastro ao ser reutilizada.
/// </summary>
[DisallowMultipleComponent]
public sealed class MisselClasseSVFX : MonoBehaviour
{
    [Header("Efeitos do motor")]
    [SerializeField] private ParticleSystem chamaExterna;
    [SerializeField] private ParticleSystem nucleoChama;
    [SerializeField] private ParticleSystem fumacaExaustao;
    [SerializeField] private ParticleSystem clariaoIgnicao;
    [SerializeField] private TrailRenderer linhaFumaca;

    private bool motorAtivo;

    private void Awake()
    {
        LimparEfeitos();
    }

    private void OnEnable()
    {
        LimparEfeitos();
    }

    private void OnDisable()
    {
        LimparEfeitos();
    }

    /// <summary>Inicia chama, nuvem de exaustao e rastro no instante da ignicao.</summary>
    public void IniciarPropulsao()
    {
        if (motorAtivo) return;
        motorAtivo = true;

        if (linhaFumaca != null)
        {
            linhaFumaca.Clear();
            linhaFumaca.enabled = true;
            linhaFumaca.emitting = true;
        }

        Reproduzir(chamaExterna);
        Reproduzir(nucleoChama);
        Reproduzir(fumacaExaustao);

        if (clariaoIgnicao != null)
        {
            clariaoIgnicao.Clear(true);
            clariaoIgnicao.Emit(24);
            clariaoIgnicao.Play(true);
        }
    }

    private void Reproduzir(ParticleSystem sistema)
    {
        if (sistema == null) return;
        sistema.Clear(true);
        sistema.Play(true);
    }

    private void LimparEfeitos()
    {
        motorAtivo = false;
        Parar(chamaExterna);
        Parar(nucleoChama);
        Parar(fumacaExaustao);
        Parar(clariaoIgnicao);

        if (linhaFumaca != null)
        {
            linhaFumaca.emitting = false;
            linhaFumaca.Clear();
            linhaFumaca.enabled = false;
        }
    }

    private static void Parar(ParticleSystem sistema)
    {
        if (sistema != null)
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
