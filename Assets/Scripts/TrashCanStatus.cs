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
    public Color normalColor = new Color(1f, 0.85f, 0f, 1f);
    public Color fullColor = new Color(1f, 0.05f, 0.03f, 1f);

    [Header("References")]
    public TrashCanFillSensor fillSensor;

    public bool IsFull => (fillSensor != null && (fillSensor.IsFullSignaled || fillSensor.IsFull)) || currentAmount >= capacity;
    public bool IsReserved { get; private set; }

    public event Action<TrashCanStatus> FullChangedToTrue;

    private bool fullEventSent;
    private Renderer[] renderers;
    private Color[] originalColors;
    private bool sensorEventsBound;
    private bool isInitialized = false;

    public void Initialize(Vector2Int position)
    {
        gridPosition = position;
        fullEventSent = IsFull;
        IsReserved = false;

        if (fillSensor == null)
        {
            fillSensor = GetComponentInChildren<TrashCanFillSensor>();
        }

        BindSensorEvents();
        fullEventSent = IsFull;

        CacheRenderers();
        UpdateVisualState();

        // Front 요소 알파값을 0으로 설정 (쓰레기가 보이도록)
        SetFrontElementAlpha(0f);

        Debug.Log($"[TrashCanStatus] 초기화 완료 - gridPosition: {gridPosition}, fillSensor: {(fillSensor != null ? "연결됨" : "없음")}");

        // Mark fully initialized to allow event handlers to update visuals
        isInitialized = true;
    }

    private void OnDisable()
    {
        UnbindSensorEvents();
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
        Debug.Log($"[TrashCanStatus] 비우기 시작 - gridPosition: {gridPosition}", gameObject);

        currentAmount = 0;
        fullEventSent = false;
        IsReserved = false;

        // 쓰레기 센서의 추적 객체들도 함께 제거
        if (fillSensor != null)
        {
            int removedCount = fillSensor.ClearTrackedTrash();
            fillSensor.ClearFullSignal();
            Debug.Log($"[TrashCanStatus] 센서 쓰레기 제거 완료 - 제거된 객체: {removedCount}", gameObject);
        }

        Debug.Log($"[TrashCanStatus] 색상 복원됨", gameObject);
        UpdateVisualState();
    }

    private void BindSensorEvents()
    {
        if (fillSensor == null || sensorEventsBound)
        {
            return;
        }

        fillSensor.onFullSignaled.AddListener(HandleSensorFullSignaled);
        fillSensor.onBecameFull.AddListener(HandleSensorBecameFull);
        fillSensor.onEmptied.AddListener(HandleSensorEmptied);
        sensorEventsBound = true;
    }

    private void UnbindSensorEvents()
    {
        if (!sensorEventsBound || fillSensor == null)
        {
            return;
        }

        fillSensor.onFullSignaled.RemoveListener(HandleSensorFullSignaled);
        fillSensor.onBecameFull.RemoveListener(HandleSensorBecameFull);
        fillSensor.onEmptied.RemoveListener(HandleSensorEmptied);
        sensorEventsBound = false;
    }

    private void HandleSensorBecameFull()
    {
        HandleSensorFullSignaled();
    }

    private void HandleSensorFullSignaled()
    {
        if (!isInitialized)
        {
            // Ignore sensor events during initialization; initial state already captured.
            return;
        }

        if (fullEventSent)
        {
            return;
        }

        fullEventSent = true;
        Debug.Log($"[TrashCanStatus] 🔴 쓰레기통 가득 찼습니다! 색상 변경됨", gameObject);
        UpdateVisualState();
        FullChangedToTrue?.Invoke(this);
    }

    private void HandleSensorEmptied()
    {
        if (!isInitialized)
        {
            // Ignore emptied event during initialization to avoid overwriting prefab visuals.
            return;
        }

        if (!fullEventSent && currentAmount == 0)
        {
            UpdateVisualState();
            return;
        }

        fullEventSent = false;
        Debug.Log($"[TrashCanStatus] ⚪ 쓰레기통 비워졌습니다! 색상 복원됨", gameObject);
        UpdateVisualState();
    }

    private void SetFrontElementAlpha(float alpha)
    {
        // 모든 자식 오브젝트에서 "Front" 이름을 가진 것을 찾아서 투명화
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        foreach (Transform child in allChildren)
        {
            if (child.name.Contains("Front") || child.name.Contains("front"))
            {
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        Material material = new Material(renderer.material);
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;
                        renderer.material = material;
                        Debug.Log($"[TrashCanStatus] {child.name} 알파값 설정: {alpha}", gameObject);
                    }
                }
            }
        }
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

        bool shouldBeFull = IsFull;
        Color targetColor = shouldBeFull ? fullColor : normalColor;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            Color rendererColor = targetColor;
            rendererColor.a = renderers[i].material.color.a;
            renderers[i].material.color = rendererColor;
        }

        Debug.Log($"[TrashCanStatus] 색상 업데이트 - 가득참: {shouldBeFull}, 색상: {targetColor}", gameObject);
    }
}
