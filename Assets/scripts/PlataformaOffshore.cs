using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Hegemonia.AI.BrainMaster;

public class PlataformaOffshore : MonoBehaviour
{
    [Header("Proprietario")]
    [Tooltip("Time dono da plataforma. Zero usa a identidade da estrutura; nunca e inferido pela distancia.")]
    [SerializeField] private int ownerTeamId;

    public int OwnerTeamId
    {
        get { return ResolverOwnerTeamId(); }
        set
        {
            ownerTeamId = Mathf.Max(0, value);
            if (ownerTeamId <= 0) return;
            IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
            if (identidade == null) identidade = gameObject.AddComponent<IdentidadeUnidade>();
            identidade.teamID = ownerTeamId;
            identidade.tipoUnidade = TipoUnidade.Estrutura;
        }
    }
    [Header("Limites de Produção")]
    public int producaoMinima = 2315;
    public int producaoMaxima = 5000;

    [Header("Configuração Geológica")]
    public float sementeDoMapa = 100.5f; 
    public float escalaDasManchas = 0.1f;

    [Header("Armazenamento Interno")]
    public int petroleoArmazenado = 0;
    public int capacidadeArmazenamento = 50000; // Tanque

    [Header("Status Atual (Apenas Leitura)")]
    public int producaoAtualDestaPlataforma;
    public string qualidadeDoPoco;

    [Header("Feedback Visual")]
    public TextMeshPro textoProducao3D;

    [Header("Pontos de Navegação (Arraste os GameObjects aqui)")]
    public Transform pontoChegada;   // Onde o navio mira vindo do mar
    public Transform pontoAbastecer; // Onde o navio encosta (Dock)
    public Transform pontoSaida;     // Para onde vai ao sair

    [Header("Estado")]
    public bool ocupada = false;
    private NavioPetroleiro _petroleiroReservado;
    private NavioPetroleiro _petroleiroOcupante;
    private float _reservaPetroleiroAte;

    [Header("Fila de petroleiros")]
    [Tooltip("Quantidade de petroleiros que podem aguardar nesta plataforma. O primeiro opera; os demais ficam enfileirados.")]
    [SerializeField] private int capacidadeFilaPetroleiros = 8;
    [System.NonSerialized] private readonly List<NavioPetroleiro> _filaPetroleiros = new List<NavioPetroleiro>(8);

    public int PetroleirosNaFila
    {
        get
        {
            LimparFila();
            return _filaPetroleiros.Count;
        }
    }

    public bool PodeReceberPetroleiro(NavioPetroleiro petroleiro)
    {
        LimparFila();
        if (petroleiro == null) return false;
        if (_filaPetroleiros.Contains(petroleiro) || _petroleiroOcupante == petroleiro) return true;
        return _filaPetroleiros.Count < Mathf.Max(1, capacidadeFilaPetroleiros);
    }

    public bool EhOcupante(NavioPetroleiro petroleiro)
    {
        return petroleiro != null && _petroleiroOcupante == petroleiro;
    }

    private void LimparFila()
    {
        for (int i = _filaPetroleiros.Count - 1; i >= 0; i--)
        {
            NavioPetroleiro navio = _filaPetroleiros[i];
            if (navio == null || !navio.gameObject.activeInHierarchy)
                _filaPetroleiros.RemoveAt(i);
        }
        if (_petroleiroReservado == null || !_filaPetroleiros.Contains(_petroleiroReservado))
            _petroleiroReservado = _filaPetroleiros.Count > 0 ? _filaPetroleiros[0] : null;
    }

    [Header("Debug")]
    public bool debugLogs = false;

    public void TentarOcupar()
    {
        ocupada = true;
    }

    public bool TentarOcupar(NavioPetroleiro petroleiro)
    {
        LimparFila();
        if (petroleiro == null || (_petroleiroOcupante != null && _petroleiroOcupante != petroleiro))
        {
            return false;
        }

        // Apenas o primeiro da fila entra no ponto de abastecimento. Os demais
        // continuam reservados e aguardam sem disputar o mesmo ponto azul.
        if (_filaPetroleiros.Count > 0 && _filaPetroleiros[0] != petroleiro)
            return false;

        _petroleiroOcupante = petroleiro;
        ocupada = true;
        return true;
    }

    public void Liberar()
    {
        ocupada = false;
    }

    public void Liberar(NavioPetroleiro petroleiro)
    {
        if (_petroleiroOcupante == petroleiro)
        {
            _petroleiroOcupante = null;
            ocupada = false;
        }
        LimparFila();
    }

    public bool EstaReservadaPorOutro(NavioPetroleiro petroleiro)
    {
        LimparFila();
        if (_petroleiroReservado == null || _petroleiroReservado == petroleiro) return false;
        return Time.time <= _reservaPetroleiroAte && !PodeReceberPetroleiro(petroleiro);
    }

    public bool TentarReservar(NavioPetroleiro petroleiro, float duracaoSegundos = 90f)
    {
        LimparFila();
        if (!PodeReceberPetroleiro(petroleiro)) return false;

        if (!_filaPetroleiros.Contains(petroleiro))
            _filaPetroleiros.Add(petroleiro);
        _petroleiroReservado = _filaPetroleiros[0];
        _reservaPetroleiroAte = Time.time + Mathf.Max(5f, duracaoSegundos);
        return true;
    }

    public void LiberarReserva(NavioPetroleiro petroleiro)
    {
        if (petroleiro != null) _filaPetroleiros.Remove(petroleiro);
        LimparFila();
        _reservaPetroleiroAte = _petroleiroReservado != null ? Time.time + 90f : 0f;
    }

    public int DrenarPetroleo(int quantidadeSolicitada)
    {
        int quantidadeFinal = Mathf.Min(petroleoArmazenado, quantidadeSolicitada);
        petroleoArmazenado -= quantidadeFinal;
        return quantidadeFinal;
    }

    void Awake()
    {
        GarantirIdentidadeEDano();
        GarantirPontosDeNavegacao();
    }

    private void GarantirIdentidadeEDano()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null) identidade = gameObject.AddComponent<IdentidadeUnidade>();
        identidade.tipoUnidade = TipoUnidade.Estrutura;
        int time = ResolverOwnerTeamId();
        if (time > 0) identidade.teamID = time;

        SistemaDeDanos danos = GetComponent<SistemaDeDanos>();
        if (danos == null) danos = gameObject.AddComponent<SistemaDeDanos>();
        danos.ehEstrutura = true;
        if (danos.vidaMaxima < 1000f) danos.vidaMaxima = 10000f;
    }

    private int ResolverOwnerTeamId()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null && identidade.teamID > 0) return identidade.teamID;
        if (ownerTeamId > 0) return ownerTeamId;
        return 1;
    }

    private void GarantirPontosDeNavegacao()
    {
        if (pontoChegada == null)
            pontoChegada = CriarPontoLogistico("PontoChegadaPetroleiro", new Vector3(0f, 0f, -18f));
        if (pontoAbastecer == null)
            pontoAbastecer = CriarPontoLogistico("PontoAbastecerPetroleiro", new Vector3(0f, 0f, -8f));
        if (pontoSaida == null || pontoSaida == pontoAbastecer)
            pontoSaida = CriarPontoLogistico("PontoSaidaPetroleiro", new Vector3(0f, 0f, 18f));
    }

    private Transform CriarPontoLogistico(string nome, Vector3 posicaoLocal)
    {
        GameObject ponto = new GameObject(nome);
        ponto.transform.SetParent(transform, false);
        ponto.transform.localPosition = posicaoLocal;
        ponto.transform.localRotation = Quaternion.identity;
        return ponto.transform;
    }
    /*
    {
        // Pontos de navegação removidos
    }

    }
    */

    void Start()
    {
        // Plataformas offshore devem compartilhar exatamente o mesmo plano
        // d'água usado pelos navios. Prefabs antigos vinham com Y de terra e
        // ficavam visualmente flutuando (ou enterrados) após a construção.
        Vector3 nivelado = transform.position;
        nivelado.y = NavalPlacementResolver.ResolveSeaLevel();
        transform.position = nivelado;

        CalcularPotencialDoLocal();
        StartCoroutine(CicloDeProducao());
    }

    void CalcularPotencialDoLocal()
    {
        float xCoord = transform.position.x * escalaDasManchas + sementeDoMapa;
        float zCoord = transform.position.z * escalaDasManchas + sementeDoMapa;
        float riquezaDoSolo = Mathf.PerlinNoise(xCoord, zCoord);

        producaoAtualDestaPlataforma = (int)Mathf.Lerp(producaoMinima, producaoMaxima, riquezaDoSolo);

        if (riquezaDoSolo < 0.3f) qualidadeDoPoco = "Poço Pobre (Mínimo)";
        else if (riquezaDoSolo < 0.7f) qualidadeDoPoco = "Poço Comum";
        else qualidadeDoPoco = "Poço RICO! (Ouro Negro)";

        AtualizarTextoVisual();
        if (debugLogs)
            Debug.Log($"[Plataforma] Qualidade: {riquezaDoSolo}. Produção: {producaoAtualDestaPlataforma}");
    }

    IEnumerator CicloDeProducao()
    {
        WaitForSeconds espera = new WaitForSeconds(1.0f);
        while (true)
        {
            // Produz e guarda no tanque interno em vez de dar direto ao jogador
            if (petroleoArmazenado < capacidadeArmazenamento)
            {
                petroleoArmazenado += producaoAtualDestaPlataforma;
                if (petroleoArmazenado > capacidadeArmazenamento) 
                    petroleoArmazenado = capacidadeArmazenamento;
                
                AtualizarTextoVisual();
            }
            yield return espera;
        }
    }

    void AtualizarTextoVisual()
    {
        if (textoProducao3D != null)
        {
            textoProducao3D.text = $"Prod: +{producaoAtualDestaPlataforma}/s\nEstoque: {petroleoArmazenado}/{capacidadeArmazenamento}";
        }
    }

    // Função de drenagem removida (Petroleiro desativado)

    // Gizmos removidos
}
