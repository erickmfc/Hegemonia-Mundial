using UnityEngine;
using System.Collections;
using TMPro;

public class PlataformaOffshore : MonoBehaviour
{
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

    [Header("Debug")]
    public bool debugLogs = false;

    public void TentarOcupar()
    {
        ocupada = true;
    }

    public bool TentarOcupar(NavioPetroleiro petroleiro)
    {
        if (petroleiro == null || (_petroleiroOcupante != null && _petroleiroOcupante != petroleiro))
        {
            return false;
        }

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
    }

    public bool EstaReservadaPorOutro(NavioPetroleiro petroleiro)
    {
        if (_petroleiroReservado == null || _petroleiroReservado == petroleiro)
        {
            return false;
        }

        if (Time.time > _reservaPetroleiroAte)
        {
            _petroleiroReservado = null;
            _reservaPetroleiroAte = 0f;
            return false;
        }

        return true;
    }

    public bool TentarReservar(NavioPetroleiro petroleiro, float duracaoSegundos = 90f)
    {
        if (petroleiro == null || EstaReservadaPorOutro(petroleiro))
        {
            return false;
        }

        _petroleiroReservado = petroleiro;
        _reservaPetroleiroAte = Time.time + Mathf.Max(5f, duracaoSegundos);
        return true;
    }

    public void LiberarReserva(NavioPetroleiro petroleiro)
    {
        if (_petroleiroReservado == petroleiro)
        {
            _petroleiroReservado = null;
            _reservaPetroleiroAte = 0f;
        }
    }

    public int DrenarPetroleo(int quantidadeSolicitada)
    {
        int quantidadeFinal = Mathf.Min(petroleoArmazenado, quantidadeSolicitada);
        petroleoArmazenado -= quantidadeFinal;
        return quantidadeFinal;
    }

    void Awake()
    {
        // Pontos de navegação removidos
    }

    void Start()
    {
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
