using UnityEngine;

public class TrashGenerator : MonoBehaviour
{
    [Header("Trash 생성 위치")]
    public Transform trashGenerateL;
    public Transform trashGenerateR;
    
    [Header("Trash 프리팹")]
    public GameObject trashPrefab;

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
            trashCanFillSensor = FindAnyObjectByType<TrashCanFillSensor>();
        }

        // 첫 생성 시간 설정
        nextSpawnTime = Random.Range(0f, spawnInterval);
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;
        
        if (spawnTimer >= nextSpawnTime && CanSpawnTrash())
        {
            SpawnTrash();
            
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
    
    void SpawnTrash()
    {
        if (trashPrefab == null)
        {
            Debug.LogError("Trash prefab이 설정되지 않았습니다!");
            return;
        }
        
        // 비어 있는 쪽 또는 덜 찬 쪽을 우선 선택
        Transform spawnPosition = SelectSpawnPosition();
        
        if (spawnPosition == null)
        {
            Debug.LogError("Trash 생성 위치가 설정되지 않았습니다!");
            return;
        }
        
        // 생성 위치에 오프셋 추가
        Vector3 offsetPosition = spawnPosition.position + new Vector3(
            Random.Range(-positionOffsetX, positionOffsetX),
            0f,
            Random.Range(-positionOffsetZ, positionOffsetZ)
        );
        
        // Trash 생성
        GameObject trash = Instantiate(trashPrefab, offsetPosition, spawnPosition.rotation);

        // 쓰레기통 센서가 감지할 수 있도록 마커를 보장합니다.
        TrashItemMarker marker = trash.GetComponent<TrashItemMarker>();
        if (marker == null)
        {
            marker = trash.AddComponent<TrashItemMarker>();
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
            rb.AddForce(force, ForceMode.Impulse);
        }
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
}
