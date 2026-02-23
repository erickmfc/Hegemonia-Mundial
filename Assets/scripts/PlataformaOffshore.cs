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

    public void TentarOcupar()
    {
        ocupada = true;
    }

    public void Liberar()
    {
        ocupada = false;
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
