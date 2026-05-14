using UnityEngine;

public interface ITrashPool
{
    GameObject Get(Vector3 position, Quaternion rotation);
    void Return(GameObject go);
    GameObject Prefab { get; }
}
