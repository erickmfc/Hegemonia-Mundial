using UnityEngine;
using System.Collections; // Necessário para usar Coroutines (Temporizadores)

public class SiloNuclear : MonoBehaviour
{
    [Header("Configurações")]
    public Transform pontoDeSaida; // ONDE o míssil vai aparecer (Ajuste esse ponto no Unity para ficar na boca do silo)
    public string nomeDoSilo = "Silo Alpha";
    
    [Header("Sequência de Lançamento")]
    public float tempoDePreparacao = 3.0f; // Tempo que o míssil fica parado no silo antes de subir
    public float tempoDeRecarga = 5.0f;    // Tempo para poder lançar outro

    [Header("Status")]
    public bool prontoParaLancar = true;

    private MenuMisseis menuManager;

    void Start()
    {
        menuManager = FindObjectOfType<MenuMisseis>();
    }

    void OnMouseDown()
    {
        // Ao clicar no prédio, abre o menu se estiver pronto
        if (menuManager != null && prontoParaLancar)
        {
            menuManager.AbrirMenuParaSilo(this);
        }
    }

    // Chamado pelo MenuMisseis quando escolhemos o alvo
    public void DispararMissel(GameObject prefabMissel, Vector3 alvo)
    {
        if (!prontoParaLancar) return;

        // Inicia a sequência (Aparecer -> Esperar -> Decolar)
        StartCoroutine(SequenciaDeLancamento(prefabMissel, alvo));
    }

    IEnumerator SequenciaDeLancamento(GameObject prefabMissel, Vector3 alvo)
    {
        prontoParaLancar = false; // Bloqueia o silo

        // 1. INSTANCIA O MÍSSIL (Aparece no jogo, mas parado)
        // Ele vai nascer exatamente na posição e rotação do 'pontoDeSaida' do Silo.
        // DICA: Ajuste o 'pontoDeSaida' para ficar na base do silo, e o Pivot do Míssil para ser na cauda.
        GameObject misselInstanciado = Instantiate(prefabMissel, pontoDeSaida.position, pontoDeSaida.rotation);
        
        Debug.Log(nomeDoSilo + ": Portas abertas. Míssil posicionado. Ignição em " + tempoDePreparacao + "s...");

        // 2. ESPERA (Fase de preparação visual)
        yield return new WaitForSeconds(tempoDePreparacao);

        // 3. DECOLAGEM
        MisselICBM scriptMissel = misselInstanciado.GetComponent<MisselICBM>();
        if (scriptMissel != null)
        {
            scriptMissel.IniciarLancamento(alvo);
            Debug.Log(nomeDoSilo + ": Lançamento confirmado!");
        }

        // 4. COOLDOWN (Recarga)
        yield return new WaitForSeconds(tempoDeRecarga);
        prontoParaLancar = true;
        Debug.Log(nomeDoSilo + ": Pronto para novo lançamento.");
    }
}
