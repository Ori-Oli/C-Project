using System.Collections.Generic;
using UnityEngine;

public class DisabledComponentHolder : MonoBehaviour
{
    public List<MonoBehaviour> disabled = new List<MonoBehaviour>();

    public void Restore()
    {
        foreach (var mb in disabled)
        {
            if (mb != null)
            {
                mb.enabled = true;
            }
        }
        disabled.Clear();
        Destroy(this);
    }
}
