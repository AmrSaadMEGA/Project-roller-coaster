using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static InfiniteBackgroundScroller;


#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public class InfiniteBackgroundScroller : MonoBehaviour
{
	// Static constructor for editor cleanup
	static InfiniteBackgroundScroller()
	{
#if UNITY_EDITOR
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
	}

#if UNITY_EDITOR
	private static void OnPlayModeStateChanged(PlayModeStateChange state)
	{
		if (state == PlayModeStateChange.ExitingEditMode)
		{
			var allScrollers = FindObjectsOfType<InfiniteBackgroundScroller>(true);
			foreach (var scroller in allScrollers)
			{
				scroller.CleanupClones();
			}
		}
	}
#endif

	[System.Serializable]
	public class ParallaxChild
	{
		public Transform original;
		public List<Transform> clones = new List<Transform>();
		public float speedMultiplier;
		public Vector3 initialWorldPosition; // Store initial world position for positioning
	}

	[System.Serializable]
	public class PanelData
	{
		public Transform transform;
		public int pixelWidth = 322;
		public int pixelHeight = 192;
		public float scaleFactor = 5f;
		public float leftBoundaryOffset = -8.05f;
		public float rightBoundaryOffset = 8.05f;
		[System.NonSerialized] public Dictionary<Transform, Vector3> originalChildPositions = new Dictionary<Transform, Vector3>();

		public float Width => rightBoundaryOffset - leftBoundaryOffset;
		public float LeftEdgeWorld => transform.position.x + leftBoundaryOffset;
		public float RightEdgeWorld => transform.position.x + rightBoundaryOffset;

		public void SetBoundariesFromPixels()
		{
			float worldWidth = (pixelWidth * scaleFactor) / 100f;
			leftBoundaryOffset = -worldWidth / 2f;
			rightBoundaryOffset = worldWidth / 2f;
		}
	}

	[System.Serializable]
	public class ParallaxLayer { public string tag; public float speedMultiplier = 1f; }

	// Fields
	[Header("Parallax Cloning")]
	[SerializeField] private Transform parallaxContainer; // New container for parallax clones
	public float maxParallaxMultiplier = 3f;
	public float clonePadding = 0.5f;
	public float autoCloneSpacing = 1.5f;
	private Dictionary<Transform, ParallaxChild> parallaxChildren = new Dictionary<Transform, ParallaxChild>();

	public List<PanelData> panels = new List<PanelData>();
	public List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
	public float scrollSpeed = 2f;

	[Header("Auto-Set Panel Boundaries")]
	public bool setAllPanelBoundaries = true;
	public int defaultPixelWidth = 322;
	public int defaultPixelHeight = 192;
	public float defaultScaleFactor = 5f;

	private Camera mainCam;
	private float screenLeftEdge;
	private float screenRightEdge;

	void Start()
	{
		parallaxChildren?.Clear();
		mainCam = Camera.main;

		// Initialize parallax container
		if (parallaxContainer == null)
		{
			parallaxContainer = new GameObject("ParallaxContainer").transform;
			parallaxContainer.parent = transform;
			parallaxContainer.localPosition = Vector3.zero;
		}

		if (panels.Count == 0)
		{
			foreach (Transform child in transform)
			{
				if (child != parallaxContainer) // Exclude parallaxContainer
				{
					var pd = new PanelData { transform = child };
					pd.pixelWidth = defaultPixelWidth;
					pd.pixelHeight = defaultPixelHeight;
					pd.scaleFactor = defaultScaleFactor;
					pd.SetBoundariesFromPixels();
					panels.Add(pd);
				}
			}
		}

		foreach (var pd in panels)
		{
			if (pd.originalChildPositions == null)
				pd.originalChildPositions = new Dictionary<Transform, Vector3>();
			else
				pd.originalChildPositions.Clear();
			foreach (Transform c in pd.transform)
				pd.originalChildPositions[c] = c.localPosition;
		}

		// Initialize parallax clones
		foreach (var panel in panels)
		{
			foreach (Transform child in panel.transform)
			{
				if (child != null && child.gameObject.activeInHierarchy)
				{
					CreateParallaxClones(panel, child);
				}
			}
		}
		ArrangePanelsLeftToRight();
	}

	void OnDestroy() { CleanupClones(); }
	void OnDisable() { CleanupClones(); }

	public void CleanupClones()
	{
		if (parallaxChildren == null) return;

		var clonesToDestroy = new List<GameObject>();
		foreach (var pc in parallaxChildren.Values)
		{
			foreach (var clone in pc.clones)
			{
				if (clone != null && clone.gameObject != null)
					clonesToDestroy.Add(clone.gameObject);
			}
			// Reactivate original
			if (pc.original != null && pc.original.gameObject != null)
				pc.original.gameObject.SetActive(true);
		}

		foreach (var go in clonesToDestroy)
		{
			if (Application.isPlaying)
				Destroy(go);
			else
				DestroyImmediate(go);
		}

		parallaxChildren?.Clear();
	}

	void CreateParallaxClones(PanelData panel, Transform original)
	{
		if (original == null || original.GetComponent<ParallaxCloneMarker>() != null) return;

		if (!parallaxChildren.ContainsKey(original))
		{
			float speedMultiplier = Mathf.Clamp(GetParallaxMultiplier(original), 0.1f, maxParallaxMultiplier);
			var pc = new ParallaxChild
			{
				original = original,
				speedMultiplier = speedMultiplier,
				initialWorldPosition = original.position
			};

			float requiredClones = Mathf.Ceil((scrollSpeed * pc.speedMultiplier) / panel.Width) * autoCloneSpacing;
			int clonesNeeded = Mathf.Clamp((int)requiredClones, 2, 4);
			int totalClones = clonesNeeded * 2; // Left and right clones

			for (int i = 0; i < totalClones; i++)
			{
				var clone = SafeInstantiate(original, $"{original.name}_clone_{i}");
				if (clone != null) pc.clones.Add(clone);
			}

			parallaxChildren.Add(original, pc);
			PositionClones(panel, pc);
			original.gameObject.SetActive(false); // Deactivate original to avoid duplication
		}
	}

	Transform SafeInstantiate(Transform original, string name)
	{
		if (original == null || original.name.Contains("(Clone)")) return null;

		var go = Instantiate(original.gameObject, parallaxContainer);
		go.name = name;

		var scrollers = go.GetComponentsInChildren<InfiniteBackgroundScroller>();
		foreach (var s in scrollers)
		{
			if (Application.isPlaying) Destroy(s);
			else DestroyImmediate(s);
		}

		var physicsComps = go.GetComponentsInChildren<Collider>();
		foreach (var c in physicsComps) DestroyImmediate(c);

		var rigidbodies = go.GetComponentsInChildren<Rigidbody>();
		foreach (var rb in rigidbodies) DestroyImmediate(rb);

		go.AddComponent<ParallaxCloneMarker>();
		return go.transform;
	}

	void PositionClones(PanelData panel, ParallaxChild pc)
	{
		float spacing = panel.Width;
		int totalClones = pc.clones.Count;
		int N = totalClones / 2; // Number of clones per side

		for (int i = 0; i < totalClones; i++)
		{
			int k = (i < N) ? -(i + 1) : (i - N + 1); // e.g., -2, -1, 1, 2
			pc.clones[i].position = pc.initialWorldPosition + (k * spacing) * Vector3.right;
		}
	}

	void UpdateParallaxClones(ParallaxChild pc)
	{
		float delta = scrollSpeed * pc.speedMultiplier * Time.deltaTime;
		float spacing = panels[0].Width; // Assume uniform panel width
		float buffer = clonePadding;

		// Move all clones in world space
		foreach (var t in pc.clones)
		{
			t.position += Vector3.left * delta;
		}

		// Wrapping logic
		var orderedClones = pc.clones.OrderBy(c => c.position.x).ToList();
		var leftmost = orderedClones.First();
		var rightmost = orderedClones.Last();

		if (leftmost.position.x < screenLeftEdge - buffer)
		{
			leftmost.position = rightmost.position + Vector3.right * spacing;
		}
		else if (rightmost.position.x > screenRightEdge + buffer)
		{
			rightmost.position = leftmost.position - Vector3.right * spacing;
		}
	}

	void Update()
	{
		Vector3 baseDelta = Vector3.left * scrollSpeed * Time.deltaTime;
		screenLeftEdge = mainCam.ViewportToWorldPoint(new Vector3(0, 0.5f, mainCam.nearClipPlane)).x;
		screenRightEdge = mainCam.ViewportToWorldPoint(new Vector3(1, 0.5f, mainCam.nearClipPlane)).x;

		// Move panels
		foreach (var panel in panels)
		{
			panel.transform.Translate(baseDelta, Space.World);
		}

		// Update parallax clones
		foreach (var pc in parallaxChildren.Values)
		{
			UpdateParallaxClones(pc);
		}

		// Handle panel recycling
		var leftPanel = panels.OrderBy(p => p.LeftEdgeWorld).FirstOrDefault();
		var rightPanel = panels.OrderBy(p => p.RightEdgeWorld).LastOrDefault();

		if (leftPanel != null && leftPanel.RightEdgeWorld < screenLeftEdge)
		{
			float newX = rightPanel.RightEdgeWorld - leftPanel.leftBoundaryOffset;
			leftPanel.transform.position = new Vector3(newX, leftPanel.transform.position.y, leftPanel.transform.position.z);
			panels.Remove(leftPanel);
			panels.Add(leftPanel);
		}

		if (rightPanel != null && rightPanel.LeftEdgeWorld > screenRightEdge)
		{
			float newX = leftPanel.LeftEdgeWorld + rightPanel.Width;
			rightPanel.transform.position = new Vector3(newX, rightPanel.transform.position.y, rightPanel.transform.position.z);
			panels.Remove(rightPanel);
			panels.Insert(0, rightPanel);
		}
	}

	private float GetParallaxMultiplier(Transform child)
	{
		foreach (var layer in parallaxLayers)
			if (child.CompareTag(layer.tag)) return layer.speedMultiplier;
		return 1f;
	}

	public void ArrangePanelsLeftToRight()
	{
		panels.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
		for (int i = 1; i < panels.Count; i++)
		{
			var prev = panels[i - 1]; var curr = panels[i];
			float x = prev.RightEdgeWorld - curr.leftBoundaryOffset;
			curr.transform.position = new Vector3(x, curr.transform.position.y, curr.transform.position.z);
		}
	}
#if UNITY_EDITOR
	void OnDrawGizmosSelected()
	{
		if (panels == null || panels.Count == 0)
			return;

		Gizmos.color = Color.yellow;
		Vector3 root = transform.position;
		Gizmos.DrawSphere(root, 0.1f);
		Handles.Label(root, "Scroller Root");

		if (Application.isPlaying && mainCam != null)
		{
			Gizmos.color = Color.red;
			Vector3 screenLeft = new Vector3(screenLeftEdge, root.y, root.z);
			Gizmos.DrawLine(screenLeft, screenLeft + Vector3.up * 5f);
			Handles.Label(screenLeft + Vector3.up * 5f, "Screen Edge");
		}

		for (int i = 0; i < panels.Count; i++)
		{
			var panel = panels[i];
			if (panel.transform == null) continue;

			Vector3 panelPos = panel.transform.position;
			Vector3 leftWorldPos = new Vector3(panelPos.x + panel.leftBoundaryOffset, panelPos.y, panelPos.z);
			Vector3 rightWorldPos = new Vector3(panelPos.x + panel.rightBoundaryOffset, panelPos.y, panelPos.z);

			Gizmos.color = i == 0 ? Color.blue : (i == panels.Count - 1 ? Color.green : Color.cyan);
			float pointSize = 0.2f;
			Gizmos.DrawSphere(leftWorldPos, pointSize);
			Gizmos.DrawSphere(rightWorldPos, pointSize);

			Gizmos.DrawLine(leftWorldPos, rightWorldPos);

			float markerHeight = 2f;
			Gizmos.DrawLine(leftWorldPos, leftWorldPos + Vector3.up * markerHeight);
			Gizmos.DrawLine(rightWorldPos, rightWorldPos + Vector3.up * markerHeight);

			float worldHeight = (panel.pixelHeight * panel.scaleFactor) / 100f;
			Vector3 topLeft = new Vector3(leftWorldPos.x, panelPos.y + worldHeight / 2, panelPos.z);
			Vector3 topRight = new Vector3(rightWorldPos.x, panelPos.y + worldHeight / 2, panelPos.z);
			Vector3 bottomRight = new Vector3(rightWorldPos.x, panelPos.y - worldHeight / 2, panelPos.z);
			Vector3 bottomLeft = new Vector3(leftWorldPos.x, panelPos.y - worldHeight / 2, panelPos.z);

			Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
			Gizmos.DrawLine(topLeft, topRight);
			Gizmos.DrawLine(topRight, bottomRight);
			Gizmos.DrawLine(bottomRight, bottomLeft);
			Gizmos.DrawLine(bottomLeft, topLeft);

			string panelPosition = i == 0 ? "Left" : (i == panels.Count - 1 ? "Right" : "Middle");
			Handles.Label(panelPos, $"Panel {i} ({panelPosition})");

			Vector3 infoPos = panelPos + Vector3.down * (worldHeight / 2 + 0.5f);
			Handles.Label(infoPos, $"{panel.pixelWidth}×{panel.pixelHeight}px × {panel.scaleFactor}");
		}
	}
#endif

	private static bool Any<T>(List<T> list, System.Func<T, bool> predicate)
	{
		foreach (var item in list) if (predicate(item)) return true;
		return false;
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(InfiniteBackgroundScroller))]
public class InfiniteBackgroundScrollerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		InfiniteBackgroundScroller scroller = (InfiniteBackgroundScroller)target;

		EditorGUILayout.Space();

		if (GUILayout.Button("Initialize Panels from Children"))
		{
			scroller.panels.Clear();
			foreach (Transform child in scroller.transform)
			{
				var pd = new PanelData { transform = child };
				var spriteRenderer = child.GetComponent<SpriteRenderer>();
				if (spriteRenderer != null && spriteRenderer.sprite != null)
				{
					// Use the sprite's actual pixel dimensions and transform scale
					pd.pixelWidth = (int)spriteRenderer.sprite.rect.width;
					pd.pixelHeight = (int)spriteRenderer.sprite.rect.height;
					pd.scaleFactor = child.localScale.x; // Assumes uniform scaling
				}
				else
				{
					// Fallback to defaults if no sprite is found
					pd.pixelWidth = scroller.defaultPixelWidth;
					pd.pixelHeight = scroller.defaultPixelHeight;
					pd.scaleFactor = scroller.defaultScaleFactor;
				}
				pd.SetBoundariesFromPixels();
				scroller.panels.Add(pd);
			}
			EditorUtility.SetDirty(target);
		}

		EditorGUILayout.BeginHorizontal();
		{
			if (GUILayout.Button("Calculate Panel Bounds"))
			{
				foreach (var panel in scroller.panels)
				{
					if (panel.transform != null)
					{
						var spriteRenderer = panel.transform.GetComponent<SpriteRenderer>();
						if (spriteRenderer != null && spriteRenderer.sprite != null)
						{
							// Update panel data from current sprite and transform
							panel.pixelWidth = (int)spriteRenderer.sprite.rect.width;
							panel.pixelHeight = (int)spriteRenderer.sprite.rect.height;
							panel.scaleFactor = panel.transform.localScale.x;
						}
						panel.SetBoundariesFromPixels();
					}
				}
				EditorUtility.SetDirty(target);
			}

			if (GUILayout.Button("Arrange Panels"))
			{
				scroller.ArrangePanelsLeftToRight();
				EditorUtility.SetDirty(target);
			}
		}
		EditorGUILayout.EndHorizontal();
	}
}
#endif