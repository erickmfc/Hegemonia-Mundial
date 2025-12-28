using UnityEngine;

public class Fabrica : MonoBehaviour
{
    [Header("Tipo de Fábrica")]
    public bool ehQuartel; // Marque TRUE se for Tenda/Soldado. Desmarque se for Hangar/Tanque.

    [Header("Pontos de Spawn (Arraste aqui os filhos)")]
    public Transform pontoNascimento;
    public Transform pontoSaida;

    void Start()
    {
        Debug.Log($"Fabrica Iniciada no objeto: {gameObject.name}");

        // Assim que esse prédio nascer, ele procura o Gerente e se registra
        GerenteDeJogo gerente = FindObjectOfType<GerenteDeJogo>(); 
        
        // --- AUTOCORREÇÃO DE CONFIGURAÇÃO ---
        string meuNome = gameObject.name.ToLower();
        if(meuNome.Contains("hangar")) ehQuartel = false;
        if(meuNome.Contains("tenda") || meuNome.Contains("quartel")) ehQuartel = true;

        if (gerente != null)
        {
            if(pontoNascimento == null || pontoSaida == null)
            {
                Debug.LogError($"ERRO NA FÁBRICA ({gameObject.name}): Ponto de Nascimento ou Saída estão vazios! Configure no Prefab.");
            }

            if (ehQuartel)
            {
                // Eu sou um Quartel, atualize o ponto dos soldados!
                Debug.Log($"Registrando Tenda (Quartel) no Gerente...");
                gerente.AtualizarPontoQuartel(pontoNascimento, pontoSaida);
            }
            else
            {
                // Eu sou um Hangar, atualize o ponto dos tanques!
                Debug.Log($"Registrando Hangar no Gerente...");
                gerente.AtualizarPontoHangar(pontoNascimento, pontoSaida);
            }
        }
        else
        {
            Debug.LogError("Fabrica nao encontrou o GerenteDeJogo!");
        }
    }
}
