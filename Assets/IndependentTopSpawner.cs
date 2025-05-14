using UnityEngine;
using System.Collections.Generic;

public class IndependentTopSpawner : MonoBehaviour
{
	[Header("References")]
	public GameObject prefab;
	public Camera mainCam;

	[Header("Spawn Settings")]
	public float spawnInterval = 1f;
	public float minDistanceBetweenSpawns = 1f;
	public int maxSpawnAttempts = 10;

	[Tooltip("Optional extra range in world-units for X, measured from top-left pivot")]
	public Vector2 spawnXOffsetRange = new Vector2(1f, 1.5f);
	[Tooltip("Optional extra range in world-units for Y, measured down from top-left pivot")]
	public Vector2 spawnYOffsetRange = new Vector2(0f, -0.5f);

	[Tooltip("Must match the RideWithBackground scrollSpeed")]
	public float scrollSpeed = 2f;

	private float timer = 0f;
	private List<Transform> allSpawned = new List<Transform>();

	void Awake()
	{
		mainCam = mainCam ?? Camera.main;
		if (prefab == null)
		{
			Debug.LogError("Assign a prefab to IndependentTopSpawner.");
			enabled = false;
		}
	}

	void Update()
	{
		timer += Time.deltaTime;
		if (timer >= spawnInterval)
		{
			TrySpawn();
			timer = 0f;
		}
	}

	void TrySpawn()
	{
		// Clean up destroyed entries
		allSpawned.RemoveAll(t => t == null);

		// Compute world‐space position of the top-left corner
		Vector3 topLeft = mainCam.ViewportToWorldPoint(
			new Vector3(0, 1, mainCam.nearClipPlane)
		);

		for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
		{
			// pick a random X offset from spawnXOffsetRange
			float x = topLeft.x + Random.Range(spawnXOffsetRange.x, spawnXOffsetRange.y);
			// pick a random Y offset (negative means downwards)
			float y = topLeft.y + Random.Range(spawnYOffsetRange.x, spawnYOffsetRange.y);

			Vector3 spawnPos = new Vector3(x, y, 0f);

			// distance check
			bool tooClose = false;
			foreach (var t in allSpawned)
			{
				if (Vector3.Distance(spawnPos, t.position) < minDistanceBetweenSpawns)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose) continue;

			// instantiate and give it the rider script
			GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
			var rider = go.GetComponent<RideWithBackground>();
			if (rider == null)
				rider = go.AddComponent<RideWithBackground>();
			rider.scrollSpeed = scrollSpeed;

			allSpawned.Add(go.transform);
			return;
		}
	}
}
