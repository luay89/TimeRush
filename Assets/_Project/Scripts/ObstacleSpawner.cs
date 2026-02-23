using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private float spawnRangeX = 4f;
    [SerializeField] private float spawnHeight = 10f;

    private float timer;

    public float SpawnInterval
    {
        get => spawnInterval;
        set => spawnInterval = Mathf.Max(0.01f, value);
    }

    void Update()
    {
        if (!obstaclePrefab) return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            float randomX = Random.Range(-spawnRangeX, spawnRangeX);
            Vector3 spawnPos = new Vector3(randomX, spawnHeight, 0f);

            Instantiate(obstaclePrefab, spawnPos, Quaternion.identity);

            timer = 0f;
        }
    }
}
