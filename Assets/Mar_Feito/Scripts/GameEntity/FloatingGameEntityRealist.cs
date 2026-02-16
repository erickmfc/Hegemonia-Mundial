using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ArchimedsLab;


[RequireComponent(typeof(MeshFilter))]
public class FloatingGameEntityRealist : GameEntity
{
  public Mesh buoyancyMesh;
  
  [Header("Estabilidade")]
  [Tooltip("Força que mantém o navio em pé (Antygaviti)")]
  public float Antygaviti = 5.0f;

  /* These 4 arrays are cache array, preventing some operations to be done each frame. */
  tri[] _triangles;
  tri[] worldBuffer;
  tri[] wetTris;
  tri[] dryTris;
  //These two variables will store the number of valid triangles in each cache arrays. They are different from array.Length !
  uint nbrWet, nbrDry;

  WaterSurface.GetWaterHeight realist = delegate (Vector3 pos)
  {
    const float eps = 0.1f;
    return (OceanAdvanced.GetWaterHeight(pos + new Vector3(-eps, 0F, -eps))
          + OceanAdvanced.GetWaterHeight(pos + new Vector3(eps, 0F, -eps))
          + OceanAdvanced.GetWaterHeight(pos + new Vector3(0F, 0F, eps))) / 3F;
  };

  protected override void Awake()
  {
    base.Awake();

    //By default, this script will take the render mesh to compute forces. You can override it, using a simpler mesh.
    Mesh m = buoyancyMesh == null ? GetComponent<MeshFilter>().mesh : buoyancyMesh;
    //Setting up the cache for the game. Here we use variables with a game-long lifetime.
    WaterCutter.CookCache(m, ref _triangles, ref worldBuffer, ref wetTris, ref dryTris);

    // FIX: Ensure all MeshColliders are convex to avoid "Concave Mesh Collider with Rigidbody" errors
    foreach (var mc in GetComponentsInChildren<MeshCollider>())
    {
        mc.convex = true;
    }
  }

  protected override void FixedUpdate()
  {
    base.FixedUpdate();
    if (rb.IsSleeping())
      return;
    /* It's strongly advised to call these in the FixedUpdate function to prevent some weird behaviors */

    //This will prepare static cache, modifying vertices using rotation and position offset.
    WaterCutter.CookMesh(transform.position, transform.rotation, ref _triangles, ref worldBuffer);

    /*
        Now mesh ae reprensented in World position, we can split the mesh, and split tris that are partially submerged.
        Here I use a very simple water model, already implemented in the DLL.
        You can give your own. See the example in Examples/CustomWater.
    */
    WaterCutter.SplitMesh(worldBuffer, ref wetTris, ref dryTris, out nbrWet, out nbrDry, realist);
    //This function will compute the forces depending on the triangles generated before.
    Archimeds.ComputeAllForces(wetTris, dryTris, nbrWet, nbrDry, speed, rb);
    
    // Aplicar a força de estabilização (Antygaviti)
    AplicarEstabilidade();
  }

  // --- LÓGICA ANTYGAVITI (Estabilidade do Barco) ---
  void AplicarEstabilidade() {
    if (rb == null) return;

    // 1. Calculamos o erro de rotação (o quanto o barco inclinou para o lado)
    // Comparamos o "cima" do navio com o "cima" absoluto do mundo
    Quaternion rotacaoDesejada = Quaternion.FromToRotation(transform.up, Vector3.up);

    // 2. Transformamos isso em uma força de giro (Torque)
    // Aqui usamos a variável "Antygaviti" que aparece no seu Inspector!
    float forcaEstabilidade = Antygaviti * 10f; 

    // 3. Aplicamos a força para puxar o mastro de volta para o céu
    rb.AddTorque(new Vector3(rotacaoDesejada.x, rotacaoDesejada.y, rotacaoDesejada.z) * forcaEstabilidade);

    // 4. Amortecimento Angular (Damping)
    // Isso impede que o barco fique balançando para sempre como uma mola
    rb.angularVelocity *= (1.0f - 0.1f); 
  }

#if UNITY_EDITOR
  //Some visualizations for this buyoancy script.
  protected override void OnDrawGizmos()
  {
    base.OnDrawGizmos();

    if (!Application.isPlaying)
      return;

    Gizmos.color = Color.blue;
    for (uint i = 0; i < nbrWet; i++)
    {
      Gizmos.DrawLine(wetTris[i].a, wetTris[i].b);
      Gizmos.DrawLine(wetTris[i].b, wetTris[i].c);
      Gizmos.DrawLine(wetTris[i].a, wetTris[i].c);
    }

    Gizmos.color = Color.yellow;
    for (uint i = 0; i < nbrDry; i++)
    {
      Gizmos.DrawLine(dryTris[i].a, dryTris[i].b);
      Gizmos.DrawLine(dryTris[i].b, dryTris[i].c);
      Gizmos.DrawLine(dryTris[i].a, dryTris[i].c);
    }
  }
#endif
}
