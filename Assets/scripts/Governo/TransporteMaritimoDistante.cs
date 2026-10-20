using UnityEngine;

/// <summary>
/// Apoio leve para transportes maritimos automaticos: oculta apenas renderers
/// fora da area da camera e permite acelerar a navegacao distante sem parar a
/// simulacao, a logistica ou os componentes do navio.
/// </summary>
internal sealed class TransporteMaritimoDistante
{
    private const float DiasAntesDaAceleracao = 2f;
    private const float MultiplicadorDistante = 5f;
    private const float RaioCameraProxima = 800f;
    private const float RaioDestinoVisivel = 1200f;
    private const float IntervaloAtualizacaoCamera = 0.25f;
    private const float IntervaloBuscaCamera = 1f;

    private static Camera cameraPrincipal;
    private static float proximaBuscaCamera;

    private readonly Transform raiz;
    private readonly Renderer[] renderers;
    private readonly bool[] estadoOriginalRenderers;

    private float tempoCiclo;
    private float proximaAtualizacao;
    private float multiplicadorAtual = 1f;
    private bool cicloIniciado;
    private bool renderersVisiveis = true;

    public TransporteMaritimoDistante(Transform raiz)
    {
        this.raiz = raiz;
        renderers = raiz != null ? raiz.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
        estadoOriginalRenderers = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            estadoOriginalRenderers[i] = renderers[i] != null && renderers[i].enabled;
        }
    }

    public void IniciarCiclo()
    {
        tempoCiclo = 0f;
        cicloIniciado = true;
        multiplicadorAtual = 1f;
        proximaAtualizacao = 0f;
        DefinirRenderersVisiveis(true);
    }

    public void EncerrarCiclo()
    {
        cicloIniciado = false;
        tempoCiclo = 0f;
        multiplicadorAtual = 1f;
        DefinirRenderersVisiveis(true);
    }

    /// <param name="cicloAtivo">Se o transporte tem uma operacao automatica em andamento.</param>
    /// <param name="autonomo">Falso quando existe uma ordem externa que assume o controle.</param>
    /// <param name="emTransito">Verdadeiro somente durante um trecho de navegacao.</param>
    /// <param name="possuiDestino">Indica se ha um ponto de carga, descarga ou rota conhecido.</param>
    public float Atualizar(bool cicloAtivo, bool autonomo, bool emTransito, bool possuiDestino, Vector3 destino)
    {
        if (!cicloAtivo)
        {
            EncerrarCiclo();
            return 1f;
        }

        if (!cicloIniciado) IniciarCiclo();
        if (autonomo && !MenuPausaController.EstaPausado) tempoCiclo += Time.deltaTime;

        // Em atracacao, fila ou transferencia, a operacao permanece normal e visivel.
        if (!autonomo || !emTransito)
        {
            multiplicadorAtual = 1f;
            DefinirRenderersVisiveis(true);
            return 1f;
        }

        if (Time.unscaledTime < proximaAtualizacao) return multiplicadorAtual;
        proximaAtualizacao = Time.unscaledTime + IntervaloAtualizacaoCamera;

        Camera camera = ObterCameraPrincipal();
        if (camera == null || raiz == null)
        {
            // Sem camera confiavel, falha em modo seguro: manter o navio normal e visivel.
            multiplicadorAtual = 1f;
            DefinirRenderersVisiveis(true);
            return 1f;
        }

        Vector3 posicaoNavio = raiz.position;
        Vector3 posicaoCamera = camera.transform.position;
        Vector3 deltaCamera = posicaoNavio - posicaoCamera;
        deltaCamera.y = 0f;
        bool cameraProxima = deltaCamera.sqrMagnitude <= RaioCameraProxima * RaioCameraProxima;

        Vector3 viewport = camera.WorldToViewportPoint(posicaoNavio);
        bool dentroDaVisao = viewport.z > 0f
            && viewport.x >= -0.08f && viewport.x <= 1.08f
            && viewport.y >= -0.08f && viewport.y <= 1.08f;

        bool pertoDoDestino = !possuiDestino || DistanciaHorizontalSqr(posicaoNavio, destino)
            <= RaioDestinoVisivel * RaioDestinoVisivel;
        bool manterVisivel = cameraProxima || dentroDaVisao || pertoDoDestino;
        DefinirRenderersVisiveis(manterVisivel);

        float duracaoDia = GerenciadorTempo.Instancia != null
            ? Mathf.Max(1f, GerenciadorTempo.Instancia.duracaoDiaSegundos)
            : 30f;
        bool passouDoisDias = tempoCiclo >= duracaoDia * DiasAntesDaAceleracao;
        multiplicadorAtual = passouDoisDias && !manterVisivel ? MultiplicadorDistante : 1f;
        return multiplicadorAtual;
    }

    private static Camera ObterCameraPrincipal()
    {
        if (Time.unscaledTime >= proximaBuscaCamera)
        {
            cameraPrincipal = Camera.main;
            proximaBuscaCamera = Time.unscaledTime + IntervaloBuscaCamera;
        }

        return cameraPrincipal;
    }

    private void DefinirRenderersVisiveis(bool visiveis)
    {
        if (renderersVisiveis == visiveis) return;
        renderersVisiveis = visiveis;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            bool habilitado = visiveis && estadoOriginalRenderers[i];
            if (renderer.enabled != habilitado) renderer.enabled = habilitado;
        }
    }

    private static float DistanciaHorizontalSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
