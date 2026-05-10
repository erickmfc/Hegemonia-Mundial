using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SaveableEntity : MonoBehaviour
{
    [SerializeField] private string uniqueId;
    [SerializeField] private string prefabKey;
    [SerializeField] private bool runtimeSpawned = true;

    public string UniqueId
    {
        get
        {
            GarantirIds();
            return uniqueId;
        }
        set
        {
            uniqueId = value;
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                uniqueId = Guid.NewGuid().ToString("N");
            }
        }
    }

    public string PrefabKey
    {
        get
        {
            GarantirIds();
            return prefabKey;
        }
        set
        {
            prefabKey = NormalizarPrefabKey(value);
        }
    }

    public bool RuntimeSpawned
    {
        get => runtimeSpawned;
        set => runtimeSpawned = value;
    }

    private void Awake()
    {
        GarantirIds();
    }

    private void OnValidate()
    {
        prefabKey = NormalizarPrefabKey(prefabKey);
    }

    public void GarantirIds()
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            uniqueId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(prefabKey))
        {
            prefabKey = NormalizarPrefabKey(gameObject.name);
        }
    }

    public static string NormalizarPrefabKey(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return string.Empty;
        }

        string key = nome.Trim();
        key = key.Replace("(Clone)", string.Empty).Trim();
        int cloneIndex = key.IndexOf(" Clone", StringComparison.OrdinalIgnoreCase);
        if (cloneIndex >= 0)
        {
            key = key.Substring(0, cloneIndex).Trim();
        }

        return key;
    }

    public static SaveableEntity Garantir(GameObject alvo, string prefabKeySugerida = null)
    {
        if (alvo == null)
        {
            return null;
        }

        SaveableEntity saveable = alvo.GetComponent<SaveableEntity>();
        if (saveable == null)
        {
            saveable = alvo.AddComponent<SaveableEntity>();
        }

        saveable.GarantirIds();
        if (!string.IsNullOrWhiteSpace(prefabKeySugerida))
        {
            saveable.PrefabKey = prefabKeySugerida;
        }

        return saveable;
    }
}
