using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MapaGeralCameraPinsPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator PinsLimitamTresEQuartoQuintoSubstituemEmOrdemCircular()
    {
        Type controllerType = ResolveControllerType();
        GameObject controladorObjeto = new GameObject("MapaPinsLimiteTeste");
        controladorObjeto.SetActive(false);
        Component controlador = controladorObjeto.AddComponent(controllerType);

        try
        {
            MethodInfo registrar = controllerType.GetMethod("RegistrarMarcacaoCamera", Members);
            Assert.That(registrar, Is.Not.Null, "O registro interno dos pins precisa existir.");
            List<Vector3> pins = (List<Vector3>)controllerType
                .GetField("_marcacoesCamera", Members)
                .GetValue(controlador);

            Vector3 primeiro = new Vector3(10f, 0f, 11f);
            Vector3 segundo = new Vector3(20f, 0f, 21f);
            Vector3 terceiro = new Vector3(30f, 0f, 31f);
            Vector3 quarto = new Vector3(40f, 0f, 41f);
            Vector3 quinto = new Vector3(50f, 0f, 51f);

            registrar.Invoke(controlador, new object[] { primeiro });
            registrar.Invoke(controlador, new object[] { segundo });
            registrar.Invoke(controlador, new object[] { terceiro });
            Assert.That(pins, Is.EqualTo(new[] { primeiro, segundo, terceiro }));

            registrar.Invoke(controlador, new object[] { quarto });
            Assert.That(pins, Is.EqualTo(new[] { quarto, segundo, terceiro }),
                "O quarto pin deve substituir o primeiro sem exceder o limite de três.");

            registrar.Invoke(controlador, new object[] { quinto });
            Assert.That(pins, Is.EqualTo(new[] { quarto, quinto, terceiro }),
                "O quinto pin deve substituir o segundo, mantendo a ordem cíclica.");
            Assert.That(pins, Has.Count.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.Destroy(controladorObjeto);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator TeclaDeCicloVisitaPinsEmOrdemEFocoEncerraAcompanhamento()
    {
        Type controllerType = ResolveControllerType();
        GameObject controladorObjeto = new GameObject("MapaPinsCicloTeste");
        controladorObjeto.SetActive(false);
        Component controlador = controladorObjeto.AddComponent(controllerType);
        GameObject cameraObjeto = new GameObject("CameraPrincipalPinsTeste");
        Camera cameraPrincipal = cameraObjeto.AddComponent<Camera>();
        GameObject alvo = new GameObject("AlvoAcompanhadoPinsTeste");

        try
        {
            controllerType.GetField("cameraPrincipal", Members)
                .SetValue(controlador, cameraPrincipal);
            controllerType.GetField("_seguindoAlvo", Members)
                .SetValue(controlador, true);
            controllerType.GetField("_alvoSeguir", Members)
                .SetValue(controlador, alvo.transform);

            MethodInfo registrar = controllerType.GetMethod("RegistrarMarcacaoCamera", Members);
            MethodInfo avancar = controllerType.GetMethod("AvancarParaProximaMarcacaoCamera", Members);
            Assert.That(registrar, Is.Not.Null);
            Assert.That(avancar, Is.Not.Null);

            Vector3[] destinos =
            {
                new Vector3(100f, 0f, 110f),
                new Vector3(200f, 0f, 210f),
                new Vector3(300f, 0f, 310f)
            };
            foreach (Vector3 destino in destinos)
                registrar.Invoke(controlador, new object[] { destino });

            avancar.Invoke(controlador, null);
            Assert.That(cameraPrincipal.transform.position.x, Is.EqualTo(destinos[0].x));
            Assert.That(cameraPrincipal.transform.position.z, Is.EqualTo(destinos[0].z));
            Assert.That((bool)controllerType.GetField("_seguindoAlvo", Members).GetValue(controlador), Is.False,
                "Focar um pin precisa interromper o acompanhamento da unidade anterior.");
            Assert.That(controllerType.GetField("_alvoSeguir", Members).GetValue(controlador), Is.Null);

            avancar.Invoke(controlador, null);
            Assert.That(cameraPrincipal.transform.position.x, Is.EqualTo(destinos[1].x));
            Assert.That(cameraPrincipal.transform.position.z, Is.EqualTo(destinos[1].z));

            avancar.Invoke(controlador, null);
            Assert.That(cameraPrincipal.transform.position.x, Is.EqualTo(destinos[2].x));
            Assert.That(cameraPrincipal.transform.position.z, Is.EqualTo(destinos[2].z));

            avancar.Invoke(controlador, null);
            Assert.That(cameraPrincipal.transform.position.x, Is.EqualTo(destinos[0].x));
            Assert.That(cameraPrincipal.transform.position.z, Is.EqualTo(destinos[0].z),
                "Depois do terceiro pin, a navegação deve voltar ao primeiro.");
        }
        finally
        {
            UnityEngine.Object.Destroy(controladorObjeto);
            UnityEngine.Object.Destroy(cameraObjeto);
            UnityEngine.Object.Destroy(alvo);
        }

        yield return null;
    }

    private static Type ResolveControllerType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType("MapaGeralController");
            if (type != null) return type;
        }

        Assert.Fail("O tipo MapaGeralController precisa estar carregado no projeto.");
        return null;
    }
}
