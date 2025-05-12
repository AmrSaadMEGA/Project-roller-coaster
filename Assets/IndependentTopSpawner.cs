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
	[Tooltip("How far off-screen to the right items should initially appear")]
	public float offscreenSpawnOffset = 1f;

	private float timer = 0f;
	private List<Transform> allSpawned = new List<Transform>();

	[Tooltip("Must match the RideWithBackground scrollSpeed")]
	public float scrollSpeed = 2f;

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

		// Get world‐space camera bounds
		Vector3 bl = mainCam.ViewportToWorldPoint(new Vector3(0, 0, mainCam.nearClipPlane));
		Vector3 tr = mainCam.ViewportToWorldPoint(new Vector3(1, 1, mainCam.nearClipPlane));
		float midY = (bl.y + tr.y) * 0.5f;

		// Define X‐range so spawns occur just off the right edge
		float spawnXMin = tr.x + offscreenSpawnOffset;
		float spawnXMax = tr.x + offscreenSpawnOffset + /* optional extra range */ 0.5f;

		for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
		{
			// random X in the off-screen band
			float x = Random.Range(spawnXMin, spawnXMax);
			// random Y in top half
			float y = Random.Range(midY, tr.y);

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

			// spawn unparented in world-space
			GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
			// add the rider script if not already on the prefab
			if (go.GetComponent<RideWithBackground>() == null)
				go.AddComponent<RideWithBackground>().scrollSpeed = scrollSpeed;
			allSpawned.Add(go.transform);
			return;
		}
	}
}
