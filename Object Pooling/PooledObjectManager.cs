using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm pooled objelerin lifecycle'ını yöneten singleton manager
/// GetComponent çağrılarını ortadan kaldırır
/// </summary>
public class PooledObjectManager : MonoBehaviour
{
    public static PooledObjectManager Instance { get; private set; }
    
    // Aktif objeleri takip eden liste
    private List<PooledObject> activeObjects = new List<PooledObject>(100);
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Pool'dan obje al ve aktive et
    /// </summary>
    public GameObject Spawn(PoolObjectType type, Vector3 position, Quaternion rotation, float lifetime = 0f)
    {
        GameObject obj = ObjectPooling.Instance.Get(type);
        if (obj == null) return null;

        obj.transform.SetPositionAndRotation(position, rotation);
        
        // PooledObject component'ini al (cache'lenmiş)
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            pooledObj = obj.AddComponent<PooledObject>();
        }
        
        pooledObj.Activate(type, lifetime);
        
        // Aktif listeye ekle
        if (!activeObjects.Contains(pooledObj))
        {
            activeObjects.Add(pooledObj);
        }
        
        return obj;
    }
    public void GetActiveObjects(PoolObjectType type, List<GameObject> outList)
    {
        // Dışarıdan gelen listeyi temizle
        outList.Clear();

        // Ana aktif obje listesini dolaş
        foreach (PooledObject obj in activeObjects)
        {
            // Tipi eşleşiyorsa listeye ekle
            if (obj != null && obj.poolType == type)
            {
                outList.Add(obj.gameObject);
            }
        }
    }

    /// <summary>
    /// Parent ile spawn
    /// </summary>
    public GameObject Spawn(PoolObjectType type, Vector3 position, Quaternion rotation, Transform parent, float lifetime = 0f)
    {
        GameObject obj = Spawn(type, position, rotation, lifetime);
        if (obj != null)
        {
            obj.transform.SetParent(parent);
        }
        return obj;
    }

    /// <summary>
    /// Particle effect spawn (lifetime otomatik hesaplanır)
    /// </summary>
    public GameObject SpawnParticle(PoolObjectType type, Vector3 position, Quaternion rotation)
    {
        return Spawn(type, position, rotation, 0f);
    }

    /// <summary>
    /// Her frame aktif objeleri kontrol et
    /// </summary>
    private void LateUpdate()
    {
        for (int i = activeObjects.Count - 1; i >= 0; i--)
        {
            if (activeObjects[i] == null)
            {
                activeObjects.RemoveAt(i);
                continue;
            }

            // Objenin süresi doldu mu kontrol et
            if (activeObjects[i].ShouldReturn())
            {
                activeObjects[i].ReturnToPool();
                activeObjects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Manuel olarak objeyi pool'a döndür
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        PooledObject pooledObj = obj.GetComponent<PooledObject>();
        if (pooledObj != null)
        {
            pooledObj.ReturnToPool();
            activeObjects.Remove(pooledObj);
        }
    }

    /// <summary>
    /// Belirli bir tipteki tüm aktif objeleri pool'a döndür
    /// </summary>
    public void ReturnAllOfType(PoolObjectType type)
    {
        for (int i = activeObjects.Count - 1; i >= 0; i--)
        {
            if (activeObjects[i].poolType == type)
            {
                activeObjects[i].ReturnToPool();
                activeObjects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Debug: Aktif obje sayısını göster
    /// </summary>
    public int GetActiveCount() => activeObjects.Count;
}