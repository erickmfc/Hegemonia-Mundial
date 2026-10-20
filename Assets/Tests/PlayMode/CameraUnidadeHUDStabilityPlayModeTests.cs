using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CameraUnidadeHUDStabilityPlayModeTests
{
    [UnityTest]
    public IEnumerator FollowCameraIgnoresUnitPitchAndRollButTracksHeading()
    {
        GameObject unitObject = new GameObject("camera follow stability test unit");
        GameObject cameraObject = new GameObject("camera follow stability test camera");
        try
        {
            System.Type unitType = System.Type.GetType("ControleUnidade, Assembly-CSharp", false);
            System.Type cameraFollowType = System.Type.GetType("CameraUnidadeHUD, Assembly-CSharp", false);
            Assert.That(unitType, Is.Not.Null, "ControleUnidade must be present in Assembly-CSharp.");
            Assert.That(cameraFollowType, Is.Not.Null, "CameraUnidadeHUD must be present in Assembly-CSharp.");

            Component unit = unitObject.AddComponent(unitType);
            Camera camera = cameraObject.AddComponent<Camera>();
            Component cameraFollow = cameraObject.AddComponent(cameraFollowType);
            MethodInfo defineTarget = cameraFollowType.GetMethod("DefinirTarget", new[] { unitType });
            Assert.That(defineTarget, Is.Not.Null, "CameraUnidadeHUD must expose DefinirTarget(ControleUnidade).");

            camera.enabled = true;
            defineTarget.Invoke(cameraFollow, new object[] { unit });
            camera.enabled = false;

            for (int i = 0; i < 3; i++)
                yield return null;

            Vector3 settledPosition = cameraObject.transform.position;
            unitObject.transform.rotation = Quaternion.Euler(35f, 0f, 25f);
            yield return null;

            Assert.That(Vector3.Distance(cameraObject.transform.position, settledPosition), Is.LessThan(0.01f),
                "Pitch and roll should not shake the normal follow-camera orbit.");

            unitObject.transform.rotation = Quaternion.Euler(35f, 30f, 25f);
            yield return null;
            yield return null;

            Assert.That(Vector3.Distance(cameraObject.transform.position, settledPosition), Is.GreaterThan(0.05f),
                "The camera should continue following the unit's heading.");
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(unitObject);
        }
    }
}
