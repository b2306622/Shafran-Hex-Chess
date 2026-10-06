using System.Collections.Generic;
using UnityEngine;

public class ResourcesControllers : MonoBehaviour
{
    private static ResourcesControllers _instance;

    private Material _blackCellMaterial;
    private Material _whiteCellMaterial;
    private Material _grayCellMaterial;

    private readonly Dictionary<string, GameObject> _prefabCache =
        new Dictionary<string, GameObject>();

    public static ResourcesControllers Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ResourcesControllers>();

                if (_instance == null)
                {
                    GameObject go = new GameObject(nameof(ResourcesControllers));
                    _instance = go.AddComponent<ResourcesControllers>();
                }
            }

            return _instance;
        }
    }

    public Material BlackCellMaterial =>
        _blackCellMaterial ??= Resources.Load<Material>("Materials/BlackCell");

    public Material WhiteCellMaterial =>
        _whiteCellMaterial ??= Resources.Load<Material>("Materials/WhiteCell");

    public Material GrayCellMaterial =>
        _grayCellMaterial ??= Resources.Load<Material>("Materials/GrayCell");

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public GameObject GetGameObject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogError("[ResourcesControllers] Resource path is empty.");
            return null;
        }

        if (_prefabCache.TryGetValue(path, out GameObject prefab))
        {
            return prefab;
        }

        prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError(
                $"[ResourcesControllers] Cannot load prefab: {path}"
            );

            return null;
        }

        _prefabCache[path] = prefab;
        return prefab;
    }
}