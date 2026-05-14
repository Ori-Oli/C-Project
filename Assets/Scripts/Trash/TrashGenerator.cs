using UnityEngine;

public class TrashGenerator : MonoBehaviour
{
    [Header("Trash 생성 위치")]
    public Transform trashGenerateL;
    public Transform trashGenerateR;
    
    [Header("Trash 프리팹")]
    public GameObject trashPrefab;
    public TrashPool trashPool; // optional: assign a TrashPool to use pooling
    public bool autoCreatePool = true;
    public int autoPoolInitialSize = 30;
    [Header("Profiling")]
    public bool enableSpawnProfiling = true;
    public int profilingWindow = 100;

    private int profileCount = 0;
    private float profileAccum = 0f;

    [Header("쓰레기통 연동")]
    public TrashCanFillSensor trashCanFillSensor;
    [Range(0f, 1f)] public float stopSpawnFillRatio = 0.8f;
    
    [Header("생성 설정")]
    public float spawnInterval = 0.5f; // 생성 간격 (초)
    public float spawnRandomRange = 0.2f; // 생성 간격 랜덤 범위
    
    [Header("생성 위치 변동")]
    public float positionOffsetX = 0.5f; // X축 오프셋 범위
    public float positionOffsetZ = 0.3f; // Z축 오프셋 범위
    
    [Header("굴러다니기 설정")]
    public float minForceX = -1.5f; // X축 최소 힘
    public float maxForceX = 1.5f; // X축 최대 힘
    public float forceY = 0f; // Y축 힘 (중력에 의해 처리됨)
    public float minForceZ = -3f; // Z축 최소 힘
    public float maxForceZ = -0.5f; // Z축 최대 힘
    
    private float spawnTimer = 0f;
    private float nextSpawnTime = 0f;

    void Start()
    {
        if (trashCanFillSensor == null)
        {
            trashCanFillSensor = GetComponentInParent<TrashCanFillSensor>();
        }

        if (trashCanFillSensor == null)
        {
            trashCanFillSensor = FindAnyObjectByType<TrashCanFillSensor>();
        }

        if (trashPool == null && trashPrefab != null)
        {
            TrashPool existing = FindAnyObjectByType<TrashPool>();
            if (existing != null && existing.prefab == trashPrefab)
            {
                trashPool = existing;
            }
            else if (autoCreatePool && trashPrefab != null)
            {
                GameObject poolGO = new GameObject($"TrashPool_{trashPrefab.name}");
                var pool = poolGO.AddComponent<TrashPool>();
                pool.prefab = trashPrefab;
                pool.initialSize = autoPoolInitialSize;
                poolGO.transform.SetParent(this.transform, false);
                trashPool = pool;
            }
        }

        // pooling not configured here

        // 첫 생성 시간 설정
        nextSpawnTime = Random.Range(0f, spawnInterval);
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;
        
        if (spawnTimer >= nextSpawnTime && CanSpawnTrash())
        {
            StartCoroutine(SpawnTrashCoroutine());

            // 다음 생성 시간 설정
            nextSpawnTime = spawnInterval + Random.Range(-spawnRandomRange, spawnRandomRange);
            spawnTimer = 0f;
        }
    }

    bool CanSpawnTrash()
    {
        if (trashCanFillSensor == null)
        {
            return true;
        }

        // 가득 차면 생성 중단
        if (trashCanFillSensor.IsFull)
        {
            return false;
        }

        return CanSpawnOnLeftSide() || CanSpawnOnRightSide();
    }

    bool CanSpawnOnLeftSide()
    {
        if (trashCanFillSensor == null)
        {
            return true;
        }

        return trashCanFillSensor.LeftFillRatio < stopSpawnFillRatio;
    }

    bool CanSpawnOnRightSide()
    {
        if (trashCanFillSensor == null)
        {
            return true;
        }

        return trashCanFillSensor.RightFillRatio < stopSpawnFillRatio;
    }
    
    System.Collections.IEnumerator SpawnTrashCoroutine()
    {
        if (trashPrefab == null)
        {
            Debug.LogError("Trash prefab이 설정되지 않았습니다!");
            yield break;
        }

        // 비어 있는 쪽 또는 덜 찬 쪽을 우선 선택
        Transform spawnPosition = SelectSpawnPosition();

        if (spawnPosition == null)
        {
            Debug.LogError("Trash 생성 위치가 설정되지 않았습니다!");
            yield break;
        }

        // 생성 위치에 오프셋 추가
        Vector3 offsetPosition = spawnPosition.position + new Vector3(
            Random.Range(-positionOffsetX, positionOffsetX),
            0f,
            Random.Range(-positionOffsetZ, positionOffsetZ)
        );

        // short stagger to spread instantiation across frames when many generators run
        float stagger = Random.Range(0f, 0.05f);
        if (stagger > 0f) yield return new WaitForSeconds(stagger);

        float t0 = enableSpawnProfiling ? Time.realtimeSinceStartup : 0f;

        GameObject trash;
        if (trashPool != null)
        {
            trash = trashPool.Get(offsetPosition, spawnPosition.rotation);
        }
        else
        {
            trash = Instantiate(trashPrefab, offsetPosition, spawnPosition.rotation);
        }

        if (enableSpawnProfiling)
        {
            float dt = Time.realtimeSinceStartup - t0;
            profileAccum += dt;
            profileCount++;
            if (profileCount >= profilingWindow)
            {
                Debug.Log($"[TrashGenerator] Spawn profiling: avg {profileAccum / profileCount:F4}s over {profileCount} samples");
                profileCount = 0;
                profileAccum = 0f;
            }
        }

        // 쓰레기통 센서가 감지할 수 있도록 마커를 보장합니다.
        TrashItemMarker marker = trash.GetComponent<TrashItemMarker>();
        if (marker == null)
        {
            marker = trash.AddComponent<TrashItemMarker>();
        }

        marker.ownerSensor = trashCanFillSensor;
        if (trashCanFillSensor != null)
        {
            trashCanFillSensor.RegisterOwnedTrash(trash);
        }

        // Spawn side flag
        if (trashGenerateL != null && spawnPosition == trashGenerateL)
        {
            marker.spawnSide = TrashItemMarker.SpawnSide.Left;
        }
        else if (trashGenerateR != null && spawnPosition == trashGenerateR)
        {
            marker.spawnSide = TrashItemMarker.SpawnSide.Right;
        }

        // Rigidbody에 힘 추가해서 굴러다니게 함
        Rigidbody rb = trash.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force = new Vector3(
                Random.Range(minForceX, maxForceX),
                forceY,
                Random.Range(minForceZ, maxForceZ)
            );

            // prepare spawned object and defer physics impulse to next frame
            PrepareSpawnedTrash(trash);
            rb.isKinematic = true;
            StartCoroutine(ApplyForceNextFrame(rb, force));
        }

        yield break;
    }

    Transform SelectSpawnPosition()
    {
        bool canSpawnLeft = trashGenerateL != null && CanSpawnOnLeftSide();
        bool canSpawnRight = trashGenerateR != null && CanSpawnOnRightSide();

        if (!canSpawnLeft && !canSpawnRight)
        {
            return null;
        }

        if (canSpawnLeft && !canSpawnRight)
        {
            return trashGenerateL;
        }

        if (!canSpawnLeft && canSpawnRight)
        {
            return trashGenerateR;
        }

        if (trashCanFillSensor == null)
        {
            return Random.value > 0.5f ? trashGenerateL : trashGenerateR;
        }

        if (trashCanFillSensor.LeftFillRatio < trashCanFillSensor.RightFillRatio)
        {
            return trashGenerateL;
        }

        if (trashCanFillSensor.RightFillRatio < trashCanFillSensor.LeftFillRatio)
        {
            return trashGenerateR;
        }

        return Random.value > 0.5f ? trashGenerateL : trashGenerateR;
    }

    private void PrepareSpawnedTrash(GameObject go)
    {
        if (go == null) return;

        // disable animators and particle emissions briefly to reduce startup cost
        Animator[] anims = go.GetComponentsInChildren<Animator>(true);
        foreach (var a in anims)
        {
            a.enabled = false;
        }

        ParticleSystem[] parts = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in parts)
        {
            var em = p.emission;
            em.enabled = false;
        }

        // disable other MonoBehaviours (except a small allowlist) to reduce startup cost
        var allowList = new System.Type[] { typeof(TrashItemMarker), typeof(TrashPooled) };
        DisabledComponentHolder holder = go.GetComponent<DisabledComponentHolder>();
        if (holder == null)
        {
            holder = go.AddComponent<DisabledComponentHolder>();
        }

        MonoBehaviour[] monos = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in monos)
        {
            if (mb == null) continue;
            System.Type t = mb.GetType();
            bool keep = false;
            foreach (var kt in allowList)
            {
                if (kt.IsAssignableFrom(t)) { keep = true; break; }
            }
            if (!keep && mb.enabled)
            {
                mb.enabled = false;
                holder.disabled.Add(mb);
            }
        }

        // re-enable after short delay
        StartCoroutine(ReenableComponentsNextFrame(go, 0.5f));
    }

    private System.Collections.IEnumerator ReenableComponentsNextFrame(GameObject go, float delay)
    {
        if (go == null) yield break;
        yield return new WaitForSeconds(delay);

        Animator[] anims = go.GetComponentsInChildren<Animator>(true);
        foreach (var a in anims)
        {
            if (a != null) a.enabled = true;
        }

        ParticleSystem[] parts = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in parts)
        {
            if (p == null) continue;
            var em = p.emission;
            em.enabled = true;
        }
    }

    private System.Collections.IEnumerator ApplyForceNextFrame(Rigidbody rb, Vector3 force)
    {
        yield return null;
        if (rb == null) yield break;
        rb.isKinematic = false;
        rb.AddForce(force, ForceMode.Impulse);
    }
}
