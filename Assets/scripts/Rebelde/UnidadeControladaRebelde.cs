using System;
using UnityEngine;

public enum TipoUnidadeRebelde { Aeronave, Helicoptero, Navio }

[DisallowMultipleComponent]
public sealed class UnidadeControladaRebelde : MonoBehaviour
{
    [NonSerialized] private RebeldeControlador controlador;
    [NonSerialized] private TipoUnidadeRebelde categoria;
    [NonSerialized] private int indice;

    public void Configurar(RebeldeControlador dono, TipoUnidadeRebelde tipo, int posicao)
    {
        controlador = dono;
        categoria = tipo;
        indice = posicao;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && controlador != null) controlador.RegistrarPerda(categoria, indice);
    }
}
