using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Owns reusable runtime instances, keeping inactive objects under the pool root.</summary>
    public sealed class GameObjectPool : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> available = new();
        private readonly Dictionary<GameObject, GameObject> sourceByInstance = new();

        public GameObject Rent(GameObject prefab)
        {
            if (prefab == null)
            {
                throw new System.ArgumentNullException(nameof(prefab));
            }

            if (!available.TryGetValue(prefab, out Queue<GameObject> instances))
            {
                instances = new Queue<GameObject>();
                available.Add(prefab, instances);
            }

            GameObject instance;
            if (instances.Count > 0)
            {
                instance = instances.Dequeue();
            }
            else
            {
                instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                sourceByInstance.Add(instance, prefab);
            }

            instance.transform.SetParent(null, false);
            instance.SetActive(true);
            return instance;
        }

        public void Return(GameObject instance)
        {
            if (instance == null || !sourceByInstance.TryGetValue(instance, out GameObject prefab))
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            available[prefab].Enqueue(instance);
        }
    }
}
