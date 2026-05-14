using System.Collections.Generic;
using UnityEngine;

public class TrashPool : MonoBehaviour, ITrashPool
{
    public GameObject prefab;
    public int initialSize = 20;
    public Transform poolParent;

    private Queue<GameObject> items = new Queue<GameObject>();

    private void Awake()
    {
        if (poolParent == null)
        {
            poolParent = this.transform;
        }

        Prewarm();
    }

    public void Prewarm()
    {
        if (prefab == null) return;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject go = CreateNew();
            Return(go);
        }
    }

    private GameObject CreateNew()
    {
        GameObject go = Instantiate(prefab, poolParent);
        go.SetActive(false);

        TrashPooled pooled = go.GetComponent<TrashPooled>();
        if (pooled == null)
        {
            pooled = go.AddComponent<TrashPooled>();
        }
        pooled.pool = this;

        return go;
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = null;
        if (items.Count > 0)
        {
            go = items.Dequeue();
        }
        else
        {
            go = CreateNew();
        }

        go.transform.SetParent(null);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.SetActive(true);

        // reset common components
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true; // initially kinematic until we apply forces
            }

        return go;
    }

    public void Return(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(poolParent, worldPositionStays: false);

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

        items.Enqueue(go);
    }

    GameObject ITrashPool.Get(Vector3 position, Quaternion rotation) => Get(position, rotation);
    void ITrashPool.Return(GameObject go) => Return(go);
    GameObject ITrashPool.Prefab => prefab;
}
