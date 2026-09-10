using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faz a animação visual das hélices do UH-60 da Guarda Costeira.
    /// O modelo importado não possui nomes de Transform compatíveis com Helicoptero,
    /// então a descoberta usa os materiais de pás e a posição da geometria sem alterar
    /// a navegação. O conjunto mais afastado do centro é tratado como rotor de cauda.
/// </summary>
public sealed class GuardaCosteiraRotorVisual : MonoBehaviour
{
    public Transform rotorPrincipal;
    public Transform rotorTraseiro;
    public bool descobrirPorMaterial = true;
    public bool girarSomenteEmVoo = true;
    public float velocidadePrincipal = 1200f;
    public float velocidadeTraseira = 1200f;
    public int eixoPrincipal = 1;
    public int eixoTraseiro = 0;

    private Helicoptero helicoptero;
    private readonly List<Transform> rotoresPrincipais = new List<Transform>();
    private readonly List<Transform> rotoresTraseiros = new List<Transform>();
    private bool descobertaConcluida;

    public bool RotorPrincipalEncontrado => rotorPrincipal != null || rotoresPrincipais.Count > 0;
    public bool RotorTraseiroEncontrado => rotorTraseiro != null || rotoresTraseiros.Count > 0;
    public float RotacaoAplicadaTotal { get; private set; }

    private void Awake()
    {
        helicoptero = GetComponent<Helicoptero>();
        DescobrirRotores();
    }

    private void Start()
    {
        DescobrirRotores();
    }

    private void Update()
    {
        if (!descobertaConcluida && Time.frameCount % 30 == 0)
        {
            DescobrirRotores();
        }

        if (girarSomenteEmVoo && helicoptero != null && !helicoptero.estaVoando)
        {
            return;
        }

        Girar(rotorPrincipal, velocidadePrincipal, eixoPrincipal);
        Girar(rotorTraseiro, velocidadeTraseira, eixoTraseiro);

        foreach (Transform rotor in rotoresPrincipais)
        {
            if (rotor != rotorPrincipal) Girar(rotor, velocidadePrincipal, eixoPrincipal);
        }

        foreach (Transform rotor in rotoresTraseiros)
        {
            if (rotor != rotorTraseiro) Girar(rotor, velocidadeTraseira, eixoTraseiro);
        }
    }

    private void Girar(Transform rotor, float velocidade, int eixo)
    {
        if (rotor == null || velocidade <= 0f) return;

        Vector3 eixoLocal = eixo == 0 ? Vector3.right : eixo == 2 ? Vector3.forward : Vector3.up;
        float delta = velocidade * Time.deltaTime;
        rotor.Rotate(eixoLocal, delta, Space.Self);
        RotacaoAplicadaTotal += Mathf.Abs(delta);
    }

    private void DescobrirRotores()
    {
        if (!descobrirPorMaterial) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        List<Transform> candidatos = new List<Transform>();

        foreach (Renderer rendererAtual in renderers)
        {
            if (rendererAtual == null || rendererAtual.sharedMaterials == null) continue;

            bool temMaterialDePas = false;
            foreach (Material material in rendererAtual.sharedMaterials)
            {
                if (material == null) continue;
                string nome = material.name.ToLowerInvariant();
                temMaterialDePas |= nome.Contains("blades1") || nome.Contains("blades2");
            }

            if (temMaterialDePas) AdicionarUnico(candidatos, rendererAtual.transform);
        }

        rotoresPrincipais.Clear();
        rotoresTraseiros.Clear();

        Transform candidatoCauda = null;
        float maiorDistanciaDoCentro = float.MinValue;
        foreach (Transform candidato in candidatos)
        {
            Renderer rendererCandidato = candidato != null ? candidato.GetComponent<Renderer>() : null;
            Vector3 centroLocal = rendererCandidato != null ? rendererCandidato.localBounds.center : candidato.localPosition;
            float distancia = centroLocal.sqrMagnitude;
            if (distancia > maiorDistanciaDoCentro)
            {
                maiorDistanciaDoCentro = distancia;
                candidatoCauda = candidato;
            }
        }

        foreach (Transform candidato in candidatos)
        {
            if (candidato == candidatoCauda) AdicionarUnico(rotoresTraseiros, candidato);
            else AdicionarUnico(rotoresPrincipais, candidato);
        }

        if (rotorPrincipal == null && rotoresPrincipais.Count > 0) rotorPrincipal = rotoresPrincipais[0];
        if (rotorTraseiro == null && rotoresTraseiros.Count > 0) rotorTraseiro = rotoresTraseiros[0];
        descobertaConcluida = RotorPrincipalEncontrado || RotorTraseiroEncontrado;
    }

    private static void AdicionarUnico(List<Transform> lista, Transform item)
    {
        if (item != null && !lista.Contains(item)) lista.Add(item);
    }
}
