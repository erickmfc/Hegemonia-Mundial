using UnityEngine;

/// <summary>
/// Reata a arma ao osso da mão depois que o Animator começa a mover o personagem.
/// Alguns prefabs trazem a arma presa ao root do modelo, o que funciona na pose de
/// edição, mas faz a arma ficar parada/flutuando durante a animação.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArmaNaMaoRuntime : MonoBehaviour
{
    private Transform arma;
    private Transform mao;
    private bool tentouAnexar;

    private void Start()
    {
        RepararAgora();
    }

    private void LateUpdate()
    {
        if (arma == null || mao == null || arma.parent != mao)
        {
            RepararAgora();
        }
    }

    public void RepararAgora()
    {
        if (tentouAnexar && arma != null && mao != null && arma.parent == mao)
        {
            return;
        }

        Animator animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return;
        }

        mao = EncontrarMao(animator);
        arma = EncontrarArma(transform);
        tentouAnexar = true;

        if (mao == null || arma == null || arma == transform)
        {
            return;
        }

        if (arma.parent == mao)
        {
            return;
        }

        // Mantém exatamente o encaixe feito no prefab e passa a acompanhar a mão.
        // O SetParent(..., true) preserva posição, rotação e escala no primeiro frame.
        arma.SetParent(mao, true);
    }

    private static Transform EncontrarMao(Animator animator)
    {
        if (animator.isHuman)
        {
            Transform maoDireita = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (maoDireita != null) return maoDireita;

            Transform maoEsquerda = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (maoEsquerda != null) return maoEsquerda;
        }

        return EncontrarPorNome(animator.transform, "righthand", "right_hand", "mixamorigrighthand", "hand_r");
    }

    private static Transform EncontrarArma(Transform raiz)
    {
        Transform[] transforms = raiz.GetComponentsInChildren<Transform>(true);

        // Primeiro procura o wrapper M16, que é o objeto que deve ser movido inteiro.
        for (int i = 0; i < transforms.Length; i++)
        {
            string nome = Normalizar(transforms[i].name);
            if (nome == "m16" || nome == "rifle" || nome == "arma")
            {
                return transforms[i];
            }
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            string nome = Normalizar(transforms[i].name);
            if (nome.Contains("rifle") || nome.Contains("weapon") || nome.Contains("fuzil") || nome.Contains("assault"))
            {
                Transform candidato = transforms[i];
                while (candidato.parent != null && candidato.parent != raiz && PareceParteDaArma(candidato.parent.name))
                {
                    candidato = candidato.parent;
                }
                return candidato;
            }
        }

        return null;
    }

    private static bool PareceParteDaArma(string nome)
    {
        string normalizado = Normalizar(nome);
        return normalizado.Contains("m16") || normalizado.Contains("rifle") || normalizado.Contains("weapon") || normalizado.Contains("assault");
    }

    private static Transform EncontrarPorNome(Transform raiz, params string[] nomes)
    {
        Transform[] transforms = raiz.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            string nome = Normalizar(transforms[i].name);
            for (int j = 0; j < nomes.Length; j++)
            {
                if (nome == nomes[j] || nome.Contains(nomes[j])) return transforms[i];
            }
        }
        return null;
    }

    private static string Normalizar(string valor)
    {
        return string.IsNullOrEmpty(valor) ? string.Empty : valor.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty).Replace(":", string.Empty);
    }
}
