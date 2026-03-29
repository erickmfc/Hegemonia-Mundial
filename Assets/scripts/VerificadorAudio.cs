using UnityEngine;

/// <summary>
/// Script de verificação do sistema de áudio
/// Cole este script na câmera principal para diagnosticar problemas
/// </summary>
public class VerificadorAudio : MonoBehaviour
{
    public bool executarNoStart = false;
    public bool permitirTeclaVerificacao = false;

    void Start()
    {
        if (executarNoStart)
            VerificarSistemaAudio();
    }

    void VerificarSistemaAudio()
    {
        Debug.Log("===== VERIFICAÇÃO DO SISTEMA DE ÁUDIO =====");
        
        // 1. Verifica AudioListener
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Debug.LogError("❌ PROBLEMA: Nenhum AudioListener encontrado na cena!");
            Debug.LogError("   SOLUÇÃO: Adicione um AudioListener na Main Camera");
        }
        else
        {
            Debug.Log($"✅ AudioListener encontrado em: {listener.gameObject.name}");
            Debug.Log($"   Posição: {listener.transform.position}");
        }
        
        // 2. Verifica volume global
        Debug.Log($"🔊 Volume Global do Unity: {AudioListener.volume}");
        if (AudioListener.volume < 0.1f)
        {
            Debug.LogWarning("⚠️ Volume global está muito baixo!");
        }
        
        // 3. Conta AudioSources na cena
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        Debug.Log($"🎵 Total de AudioSources na cena: {sources.Length}");
        
        int tocando = 0;
        foreach (var source in sources)
        {
            if (source.isPlaying)
            {
                tocando++;
                Debug.Log($"   ▶️ Tocando: {source.gameObject.name} - Clip: {source.clip?.name}");
            }
        }
        Debug.Log($"   {tocando} AudioSources estão tocando agora");
        
        // 4. Verifica SomUnidade
        SomUnidade[] somUnidades = FindObjectsByType<SomUnidade>(FindObjectsSortMode.None);
        Debug.Log($"🚁 Total de componentes SomUnidade: {somUnidades.Length}");
        
        foreach (var som in somUnidades)
        {
            Debug.Log($"   Unidade: {som.gameObject.name}");
            Debug.Log($"   - Som Motor: {(som.somMotor != null ? som.somMotor.name : "NENHUM")}");
            Debug.Log($"   - Volume: {som.volumeMotor}");
        }
        
        Debug.Log("==============================================");
    }
    
    // Permite verificar novamente apertando a tecla V
    void Update()
    {
        if (permitirTeclaVerificacao && Input.GetKeyDown(KeyCode.V))
        {
            VerificarSistemaAudio();
        }
    }
}
