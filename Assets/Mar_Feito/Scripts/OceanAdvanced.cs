using UnityEngine;

public class OceanAdvanced : MonoBehaviour
{
    // Estrutura interna para as ondas matematicas
    class Wave {
        public float frequency;
        public float amplitude;
        public float phase;
        public Vector2 direction;
        public Wave(float len, float spd, float amp, Vector2 dir) {
            frequency = (2 * Mathf.PI) / len;
            amplitude = amp;
            phase = frequency * spd;
            direction = dir.normalized;
        }
    }

    public Material ocean;
    public Light sun;
    
    // NB_WAVE = 5 (conforme o original)
    static Wave[] waves = {
        new Wave(99, 1.0f, 5.0f, new Vector2(1.0f, 0.2f)),
        new Wave(60, 1.2f, 0.8f, new Vector2(1.0f, 3.0f)),
        new Wave(20, 3.5f, 0.4f, new Vector2(2.0f, 4.0f)),
        new Wave(30, 2.0f, 0.4f, new Vector2(-1.0f, 0.0f)),
        new Wave(10, 3.0f, 0.05f, new Vector2(-1.0f, 1.2f))
    };

    void FixedUpdate() {
        if (ocean != null && sun != null) {
            // Atualiza a luz para o material nao ficar rosa/escuro
            ocean.SetVector("_WorldLightDir", -sun.transform.forward);
        }
    }

    // Stub para manter compatibilidade com scripts de Splash/Wake
    public void RegisterInteraction(Vector3 pos, float strength) {}

    // A FUNÇÃO QUE OS BARCOS USAM PARA BOIAR
    static public float GetWaterHeight(Vector3 p) {
        float height = 0;
        for (int i = 0; i < 5; i++) {
            height += waves[i].amplitude * Mathf.Sin(Vector2.Dot(waves[i].direction, new Vector2(p.x, p.z)) * waves[i].frequency + Time.time * waves[i].phase);
        }
        return height;
    }
}
