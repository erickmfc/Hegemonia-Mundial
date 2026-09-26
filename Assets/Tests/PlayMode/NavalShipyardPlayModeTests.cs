using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Run after opening Assets/Game/Environment/NavalShipyard/Scenes/NavalShipyard_Preview.unity
/// with Tools > Hegemonia Global > Naval Shipyard Builder > Play Preview.
/// </summary>
public sealed class NavalShipyardPlayModeTests
{
    [UnityTest]
    public IEnumerator IsolatedPreviewKeepsTheShipyardModularAndMetricInPlayMode()
    {
        Scene active = SceneManager.GetActiveScene();
        Assert.That(active.name, Does.StartWith("NavalShipyard_Preview"),
            "Abra a cena de preview pelo Naval Shipyard Builder antes de rodar este teste.");

        GameObject visualRoot = GameObject.Find("MilitaryNavalShipyard");
        Assert.IsNotNull(visualRoot, "Raiz visual MilitaryNavalShipyard ausente da cena de preview.");
        Assert.IsTrue(visualRoot.activeInHierarchy, "Raiz visual desativada no Play Mode.");

        string[] requiredGroups =
        {
            "Concrete_Base", "DryDock", "Main_Hangar", "Maintenance_Hangars", "Workshops",
            "Administration", "Cranes", "Piers", "Storage", "Utilities", "Pipes",
            "Lighting", "Security", "Roads", "Details", "Environment"
        };
        foreach (string groupName in requiredGroups)
            Assert.IsNotNull(visualRoot.transform.Find(groupName), "Grupo ausente: " + groupName);

        Transform platform = visualRoot.transform.Find("Concrete_Base/Ground__ConcreteBase");
        Assert.IsNotNull(platform, "Prefab modular do terreno de concreto ausente.");
        Renderer[] platformRenderers = platform.GetComponentsInChildren<Renderer>(true);
        Assert.GreaterOrEqual(platformRenderers.Length, 16, "A plataforma não tem seus módulos renderizados.");

        Bounds localBounds = default(Bounds);
        bool hasBounds = false;
        foreach (Renderer renderer in platformRenderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                point = visualRoot.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                if (!hasBounds)
                {
                    localBounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(point);
                }
            }
        }

        Assert.IsTrue(hasBounds, "Não foi possível medir os módulos do terreno.");
        Assert.That(localBounds.size.x, Is.InRange(495f, 505f), "Comprimento local da plataforma fora dos 500 m.");
        Assert.That(localBounds.size.z, Is.InRange(335f, 345f), "Largura local da plataforma fora dos 340 m.");
        Assert.GreaterOrEqual(platform.GetComponentsInChildren<Collider>(true).Length, 16,
            "A base modular não recebeu colliders de piso.");

        Transform docks = visualRoot.transform.Find("DryDock");
        Transform westDock = docks.Find("Dock__DryDock_West");
        Transform eastDock = docks.Find("Dock__DryDock_East");
        Assert.IsNotNull(westDock, "Doca seca oeste ausente.");
        Assert.IsNotNull(eastDock, "Doca seca leste ausente.");
        Assert.GreaterOrEqual(westDock.GetComponentsInChildren<Collider>(true).Length, 40,
            "Doca oeste sem piso, laterais e apoios colisionáveis.");
        Assert.GreaterOrEqual(eastDock.GetComponentsInChildren<Collider>(true).Length, 40,
            "Doca leste sem piso, laterais e apoios colisionáveis.");

        Transform cranes = visualRoot.transform.Find("Cranes");
        Transform gantry = cranes.Find("Shipyard_GantryCrane_West_1/GantryCrane");
        Assert.IsNotNull(gantry, "Hierarquia de animação do guindaste ausente.");
        foreach (string part in new[] { "LeftLeg", "RightLeg", "UpperBeam", "RailWheels", "OperatorCabin", "Trolley", "Cable", "Hook" })
            Assert.IsNotNull(gantry.Find(part), "Parte modular do guindaste ausente: " + part);

        Assert.GreaterOrEqual(visualRoot.GetComponentsInChildren<LODGroup>(true).Length, 5,
            "Os hangares e edifícios grandes não receberam LODs.");

        Physics.SyncTransforms();
        Vector3 up = visualRoot.transform.up;
        RaycastHit hit;
        Vector3 yardProbe = visualRoot.transform.TransformPoint(new Vector3(0f, 35f, 120f));
        Assert.IsTrue(Physics.Raycast(yardProbe, -up, out hit, 50f), "O pátio não possui piso colisionável.");

        Vector3 westDockProbe = visualRoot.transform.TransformPoint(new Vector3(-88f, 35f, -79f));
        Assert.IsTrue(Physics.Raycast(westDockProbe, -up, out hit, 50f), "O fundo da doca oeste não é colisionável.");
        Assert.IsTrue(hit.collider.transform.IsChildOf(westDock),
            "O raio central da doca atingiu uma peça fora da doca ou uma laje que fechou o vão.");

        yield return null;

        Assert.IsTrue(visualRoot != null && visualRoot.activeInHierarchy,
            "O estaleiro deixou de existir depois do primeiro frame em Play Mode.");
    }
}
