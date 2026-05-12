using System;
using UnityEngine;

public class TrashCanStatus : MonoBehaviour
{
    [Header("Grid")]
    public Vector2Int gridPosition = new Vector2Int(-1, -1);

    [Header("Capacity")]
    [Min(1)] public int capacity = 10;
    [Min(0)] public int currentAmount = 0;

    [Header("Visual State")]
    public bool tintWhenFull = true;
    public Color fullColor = new Color(1f, 0.05f, 0.03f, 1f);

    public bool IsFull => currentAmount >= capacity;
    public bool IsReserved { get; private set; }

    public event Action<TrashCanStatus> FullChangedToTrue;

    private bool fullEventSent;
    private Renderer[] renderers;
    private Color[] originalColors;

    public void Initialize(Vector2Int position)
    {
        gridPosition = position;
        fullEventSent = IsFull;
        IsReserved = false;
        CacheRenderers();
        UpdateVisualState();
    }

    public void AddTrash(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        bool wasFull = IsFull;
        currentAmount = Mathf.Min(capacity, currentAmount + amount);

        if (!wasFull && IsFull && !fullEventSent)
        {
            fullEventSent = true;
            UpdateVisualState();
            FullChangedToTrue?.Invoke(this);
        }
        else if (wasFull != IsFull)
        {
            UpdateVisualState();
        }
    }

    public void MarkReserved(bool reserved)
    {
        IsReserved = reserved;
    }

    public void Empty()
    {
        currentAmount = 0;
        fullEventSent = false;
        IsReserved = false;
        UpdateVisualState();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    private void UpdateVisualState()
    {
        if (!tintWhenFull)
        {
            return;
        }

        if (renderers == null || originalColors == null)
        {
            CacheRenderers();
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].material.color = IsFull ? fullColor : originalColors[i];
        }
    }
}
