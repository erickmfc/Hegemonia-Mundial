using UnityEngine;

public static class MovimentoFallbackTransicional
{
    public static bool TrySetNavDestination(GameObject unidade, Vector3 destino, float warpRaio = 25f)
    {
        _ = warpRaio;

        if (unidade == null)
        {
            return false;
        }

        ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
        if (controle != null)
        {
            return controle.EmitirOrdemMover(destino);
        }

        Debug.LogError(
            $"[MovimentoFallbackTransicional] {unidade.name} tentou mover sem ControleUnidade. Adicione a fachada oficial ao prefab ou marque o objeto como nao-controlavel.",
            unidade);
        return false;
    }
}
