using System.Collections.Generic;
using UnityEngine;

public class ObjectPooling : MonoBehaviour
{
    [Header("Pool Configuration")]
    [SerializeField] private List<PoolMapping> poolMappings = new List<PoolMapping>();
    
    private Dictionary<PoolObjectType, PoolData> poolDict = new Dictionary<PoolObjectType, PoolData>();
    
    public static ObjectPooling Instance { get; private set; }

    // Pool data structure
    private class PoolData
    {
        public GameObject prefab;
        public List<GameObject> availableObjects = new List<GameObject>();
        public List<GameObject> activeObjects = new List<GameObject>();
        public Transform container;
        public int totalCreated;
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializePools();
    }

    private void InitializePools()
    {
        foreach (var mapping in poolMappings)
        {
            if (mapping.prefab == null || mapping.type == PoolObjectType.None)
            {
                Debug.LogWarning($"Pool mapping eksik: {mapping.type}");
                continue;
            }

            // Container oluştur
            mapping.containerName = $"{mapping.type}Container";
            Transform container = new GameObject(mapping.containerName).transform;
            container.SetParent(transform);

            // Pool data oluştur
            var poolData = new PoolData
            {
                prefab = mapping.prefab,
                container = container
            };

            // İlk objeleri oluştur
            for (int i = 0; i < mapping.initialSize; i++)
            {
                GameObject obj = CreateNewPoolObject(mapping.prefab, container);
                poolData.availableObjects.Add(obj);
                poolData.totalCreated++;
            }

            poolDict[mapping.type] = poolData;
        }
    }

    private GameObject CreateNewPoolObject(GameObject prefab, Transform container)
    {
        GameObject obj = Instantiate(prefab, container);
        obj.SetActive(false);
        
        
        // PooledObject component ekle
        if (obj.GetComponent<PooledObject>() == null)
        {
            obj.AddComponent<PooledObject>();
        }
        
        return obj;
    }

    /// <summary>
    /// Pool'dan obje al - ENUM kullanımı
    /// </summary>
    public GameObject Get(PoolObjectType type)
    {
        if (type == PoolObjectType.None)
        {
            Debug.LogError("PoolObjectType.None kullanılamaz!");
            return null;
        }

        if (!poolDict.ContainsKey(type))
        {
            Debug.LogError($"Pool'da {type} bulunamadı!");
            return null;
        }

        PoolData pool = poolDict[type];

        // Mevcut objelerden kullanılabilir olanı bul
        if (pool.availableObjects.Count > 0)
        {
            GameObject obj = pool.availableObjects[0];
            pool.availableObjects.RemoveAt(0);
            pool.activeObjects.Add(obj);
            obj.SetActive(true);
            return obj;
        }

        // Yoksa yeni oluştur
        GameObject newObj = CreateNewPoolObject(pool.prefab, pool.container);
        pool.totalCreated++;
        pool.activeObjects.Add(newObj);
        newObj.SetActive(true);
        
        Debug.Log($"{type} için yeni obje oluşturuldu. Toplam: {pool.totalCreated}");
        return newObj;
    }

    /// <summary>
    /// Objeyi pool'a geri döndür
    /// </summary>
    public void ReturnToPool(GameObject obj, PoolObjectType type)
    {
        if (!poolDict.ContainsKey(type)) return;

        PoolData pool = poolDict[type];
        
        if (pool.activeObjects.Contains(obj))
        {
            pool.activeObjects.Remove(obj);
            pool.availableObjects.Add(obj);
        }
        
        obj.transform.SetParent(pool.container);
        obj.SetActive(false);
    }

    /// <summary>
    /// Tüm aktif objeleri pool'a geri döndür
    /// </summary>
    public void ReturnAllToPool(PoolObjectType type)
    {
        if (!poolDict.ContainsKey(type))
        {
            Debug.LogWarning($"Pool bulunamadı: {type}");
            return;
        }

        PoolData pool = poolDict[type];
        
        var activeObjectsCopy = new List<GameObject>(pool.activeObjects);
        
        foreach (var obj in activeObjectsCopy)
        {
            ReturnToPool(obj, type);
        }
    }

    /// <summary>
    /// String-based (deprecated)
    /// </summary>
    [System.Obsolete("String-based Get kullanmayın! Enum-based Get kullanın.")]
    public GameObject Get(string prefabName)
    {
        Debug.LogWarning($"String-based pool kullanımı deprecated: {prefabName}");
        return null;
    }

    /// <summary>
    /// Debug bilgileri
    /// </summary>
    public void LogPoolStats()
    {
        foreach (var kvp in poolDict)
        {
            Debug.Log($"{kvp.Key}: Active={kvp.Value.activeObjects.Count}, " +
                     $"Available={kvp.Value.availableObjects.Count}, " +
                     $"Total={kvp.Value.totalCreated}");
        }
    }
}