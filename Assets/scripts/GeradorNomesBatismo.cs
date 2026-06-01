using UnityEngine;
using System.Collections.Generic;

public static class GeradorNomesBatismo
{
    private static readonly string[] NomesBase = new string[]
    {
        "Hack", "Viper", "Ghost", "Reaper", "Shadow", "Venom", "Wolf", "Hunter", 
        "Eagle", "Falcon", "Maverick", "Striker", "Blade", "Titan", "Raptor", "Cobra",
        "Apex", "Nova", "Onyx", "Talon", "Phoenix", "Savage", "Wraith", "Phantom"
    };

    private static readonly string[] Sobrenomes = new string[]
    {
        "Davis", "Smith", "Johnson", "Williams", "Jones", "Brown", "Miller", "Taylor",
        "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Garcia",
        "Martinez", "Robinson", "Clark", "Rodriguez", "Lewis", "Lee", "Walker", "Hall"
    };

    public static string GerarNome()
    {
        string nome = NomesBase[Random.Range(0, NomesBase.Length)];
        string sobrenome = Sobrenomes[Random.Range(0, Sobrenomes.Length)];
        return $"{nome} {sobrenome}";
    }
}
