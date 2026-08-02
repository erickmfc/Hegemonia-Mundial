using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OceanAdvanced : MonoBehaviour
{
  class Wave
  {
    public float waveLength { get; private set; }
    public float speed { get; private set; }
    public float amplitude { get; private set; }
    public float sharpness { get; private set; }
    public float frequency { get; private set; }
    public float phase { get; private set; }
    public Vector2 direction { get; private set; }

    public Wave(float waveLength, float speed, float amplitude, float sharpness, Vector2 direction)
    {
      this.waveLength = waveLength;
      this.speed = speed;
      this.amplitude = amplitude;
      this.sharpness = sharpness;
      this.direction = direction.normalized;
      frequency = (2 * Mathf.PI) / waveLength;
      phase = frequency * speed;
    }
  };

  public Material ocean;
  public Light sun;
 
  private int interaction_id = 0;
  private Vector4[] interactions = new Vector4[NB_INTERACTIONS];
  // Mantém as propriedades do oceano em instâncias pertencentes aos
  // renderers. Nunca altera o Material compartilhado do projeto.
  private readonly List<Renderer> renderersRenderizados = new List<Renderer>();
  private readonly List<Material[]> materiaisRenderizados = new List<Material[]>();
  private Vector4 ultimaDirecaoLuz;
  private Vector4 ultimaCorLuz;
  private bool luzInicializada;

  
  const int NB_WAVE = 5;
  const int NB_INTERACTIONS = 64;
  static Wave[] waves =
  {
    new Wave(99, 1.0f, 2.8f, 0.9f, new Vector2(1.0f,  0.2f)),
    new Wave(60, 1.2f, 1.6f, 0.5f, new Vector2(1.0f,  3.0f)),
    new Wave(20, 3.5f, 0.8f, 0.8f, new Vector2(2.0f,  4.0f)),
    new Wave(30, 2.0f, 0.8f, 0.4f, new Vector2(-1.0f, 0.0f)),
    new Wave(10, 3.0f, 0.1f, 0.9f,new Vector2(-1.0f, 1.2f))
  };

  void Awake()
  {
    // O campo antigo "ocean" pode apontar para Mar_Novo.mat, enquanto o
    // MeshRenderer da cena usa Sea.mat. Inicializa o material efetivamente
    // renderizado, mas em uma instância local para não contaminar assets
    // compartilhados nem outros objetos que usem o mesmo shader.
    RegistrarMateriaisRenderizados();

    if (renderersRenderizados.Count == 0)
    {
      enabled = false;
      Debug.LogWarning("[OceanAdvanced] Material do oceano ausente; componente desativado para manter a campanha executavel.");
      return;
    }

    if (sun == null)
      sun = FindFirstObjectByType<Light>();

    if (sun == null)
    {
      enabled = false;
      Debug.LogWarning("[OceanAdvanced] Luz solar ausente; componente desativado para manter a campanha executavel.");
      return;
    }

    Vector4[] v_waves = new Vector4[NB_WAVE];
    Vector4[] v_waves_dir = new Vector4[NB_WAVE];
    for (int i = 0; i < NB_WAVE; i++)
    {
      v_waves[i] = new Vector4(waves[i].frequency, waves[i].amplitude, waves[i].phase, waves[i].sharpness);
      v_waves_dir[i] = new Vector4(waves[i].direction.x, waves[i].direction.y, 0, 0);
    }

    for (int i = 0; i < NB_INTERACTIONS; i++)
      interactions[i].w = 500.0F;

    AplicarParametrosDoOceano(v_waves, v_waves_dir);
  }

  void FixedUpdate()
  {
    if (sun == null || materiaisRenderizados.Count == 0)
      return;

    Vector4 direcaoLuz = -sun.transform.forward;
    Vector4 corLuz = new Vector4(sun.color.r, sun.color.g, sun.color.b, 0.0F);
    if (luzInicializada &&
        (direcaoLuz - ultimaDirecaoLuz).sqrMagnitude < 0.000001f &&
        (corLuz - ultimaCorLuz).sqrMagnitude < 0.000001f)
      return;

    AplicarLuz(direcaoLuz, corLuz);
  }

  public void RegisterInteraction(Vector3 pos, float strength)
  {
    interactions[interaction_id].x = pos.x;
    interactions[interaction_id].y = pos.z;
    interactions[interaction_id].z = strength;
    interactions[interaction_id].w = Time.time;
    AplicarInteracoes();
    interaction_id = (interaction_id + 1) % NB_INTERACTIONS;
  }

  private void RegistrarMateriaisRenderizados()
  {
    renderersRenderizados.Clear();
    materiaisRenderizados.Clear();

    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
    for (int i = 0; i < renderers.Length; i++)
    {
      Renderer renderer = renderers[i];
      if (renderer == null) continue;

      Material[] materiaisCompartilhados = renderer.sharedMaterials;
      bool rendererDoOceano = false;
      for (int m = 0; m < materiaisCompartilhados.Length; m++)
      {
        Material material = materiaisCompartilhados[m];
        if (material == ocean ||
            (material != null && material.shader != null &&
             material.shader.name == "Nature/OceanAdvanced"))
        {
          rendererDoOceano = true;
          break;
        }
      }

      if (!rendererDoOceano) continue;

      // renderer.materials cria cópias por renderer e deixa os assets .mat
      // originais intactos. O oceano possui poucos renderers, então o custo
      // é fixo e elimina a disputa entre cenas/câmeras.
      renderersRenderizados.Add(renderer);
      materiaisRenderizados.Add(renderer.materials);
    }
  }

  private void AplicarLuz(Vector4 direcaoLuz, Vector4 corLuz)
  {
    ultimaDirecaoLuz = direcaoLuz;
    ultimaCorLuz = corLuz;
    luzInicializada = true;

    for (int i = 0; i < materiaisRenderizados.Count; i++)
    {
      Material[] materiais = materiaisRenderizados[i];
      if (materiais == null) continue;
      for (int m = 0; m < materiais.Length; m++)
      {
        Material material = materiais[m];
        if (material == null) continue;
        material.SetVector("world_light_dir", direcaoLuz);
        material.SetVector("sun_color", corLuz);
      }
    }
  }

  private void AplicarParametrosDoOceano(Vector4[] v_waves, Vector4[] v_waves_dir)
  {
    Vector4 direcaoLuz = -sun.transform.forward;
    Vector4 corLuz = new Vector4(sun.color.r, sun.color.g, sun.color.b, 0.0F);

    for (int i = 0; i < materiaisRenderizados.Count; i++)
    {
      Material[] materiais = materiaisRenderizados[i];
      if (materiais == null) continue;
      for (int m = 0; m < materiais.Length; m++)
      {
        Material material = materiais[m];
        if (material == null) continue;

        material.SetVectorArray("waves_p", v_waves);
        material.SetVectorArray("waves_d", v_waves_dir);
        material.SetVectorArray("interactions", interactions);
        material.SetVector("world_light_dir", direcaoLuz);
        material.SetVector("sun_color", corLuz);
      }
    }

    ultimaDirecaoLuz = direcaoLuz;
    ultimaCorLuz = corLuz;
    luzInicializada = true;
  }

  private void AplicarInteracoes()
  {
    for (int i = 0; i < materiaisRenderizados.Count; i++)
    {
      Material[] materiais = materiaisRenderizados[i];
      if (materiais == null) continue;
      for (int m = 0; m < materiais.Length; m++)
      {
        Material material = materiais[m];
        if (material != null) material.SetVectorArray("interactions", interactions);
      }
    }
  }


  static public float GetWaterHeight(Vector3 p)
  {
    float height = 0;
    for (int i = 0; i < NB_WAVE; i++)
      height += waves[i].amplitude * Mathf.Sin(Vector2.Dot(waves[i].direction, new Vector2(p.x, p.z)) * waves[i].frequency + Time.time * waves[i].phase);
    return height;
  }
}
