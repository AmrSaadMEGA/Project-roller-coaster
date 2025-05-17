using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RollerCoasterGameManager : MonoBehaviour
{
	[Header("Game State")]
	[SerializeField] private GameState currentState = GameState.HumansGathering;

	[Header("References")]
	[SerializeField] private ScrollerManager scrollerManager; // Added ScrollerManager reference
	[SerializeField] private InfiniteRailScroller railScroller;
	[SerializeField] private Transform humanGatheringPoint;
	[SerializeField] private ZombieController zombie;
	[SerializeField] private ZombieHidingSystem zombieHidingSystem;
	[SerializeField] private ZombieHidingManager zombieHidingManager; // Add this line

	[Header("Humans")]
	[SerializeField] private GameObject humanPrefab;
	[SerializeField] private int humansPerRound = 8;
	[SerializeField] private float humanSpawnInterval = 0.5f;
	[SerializeField] private float gatheringRadius = 2f;

	[Header("Coaster")]
	[SerializeField] private List<RollerCoasterCart> coasterCarts = new List<RollerCoasterCart>();
	[SerializeField] private float boardingDelay = 1.5f;
	[SerializeField] private float boardingDuration = 2f;
	[SerializeField] private float rideSpeed = 5f;
	[SerializeField] private float restartDelay = 3f;
	[SerializeField] private float zombieWaitTime = 2f;
	[SerializeField] private float waitForZombieHidingTime = 5f;

	// Private variables
	private List<GameObject> spawnedHumans = new List<GameObject>();
	private bool zombieInFrontCart = false;
	private bool allHumansDead = false;
	private float originalScrollSpeed;
	private bool zombieSeated = false;
	private Coroutine gameLoopCoroutine;

	bool isHandlingZombiePresence;
	// Game states

	// Add new variable
	private bool boardingCompleted;
	public enum GameState
	{
		HumansGathering,
		WaitingForZombieToHide,
		HumansBoardingTrain,
		ZombieBoarding,
		RideInProgress,
		RideComplete,
		RideRestarting
	}

	// Public accessor for current state
	public GameState CurrentState => currentState;
	public bool BoardingCompleted => boardingCompleted;
	public List<GameObject> SpawnedHumans => spawnedHumans;

	// Expose railScroller for other components
	public InfiniteRailScroller RailScroller => railScroller;

	private void Awake()
	{
		// Validate required references
		if (railScroller == null)
		{
			Debug.LogError("RollerCoasterGameManager needs a reference to InfiniteRailScroller!");
			enabled = false;
			return;
		}

		if (zombie == null)
		{
			Debug.LogError("RollerCoasterGameManager needs a reference to the ZombieController!");
			enabled = false;
			return;
		}

		// Check for ScrollerManager
		if (scrollerManager == null)
		{
			Debug.LogError("RollerCoasterGameManager needs a reference to the ScrollerManager!");
			enabled = false;
			return;
		}

		// Find ZombieHidingSystem if not assigned
		if (zombieHidingSystem == null)
		{
			zombieHidingSystem = zombie.GetComponent<ZombieHidingSystem>();
			if (zombieHidingSystem == null)
			{
				Debug.LogError("RollerCoasterGameManager needs a reference to the ZombieHidingSystem!");
				enabled = false;
				return;
			}
		}
		if (zombieHidingManager == null)
		{
			zombieHidingManager = FindObjectOfType<ZombieHidingManager>();
			if (zombieHidingManager == null)
			{
				Debug.LogWarning("RollerCoasterGameManager couldn't find ZombieHidingManager - hide spot movement won't be synchronized");
			}
		}

		// Set rail scroller on zombie hiding system
		var railScrollerField = zombieHidingSystem.GetType().GetField("railScroller",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (railScrollerField != null)
		{
			railScrollerField.SetValue(zombieHidingSystem, railScroller);
		}

		// Store original scroll speed
		originalScrollSpeed = rideSpeed;
	}

	private void Start()
	{
		// Make sure scrollers start stopped
		scrollerManager.StopScrolling();

		// Sort carts from front to back (highest to lowest X position)
		SortCartsByPosition();

		// Begin the game flow
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}

	private void Update()
	{
		// Add new check in HumansGathering state
		if (currentState == GameState.HumansGathering &&
			!zombieHidingSystem.IsHidden &&
			!isHandlingZombiePresence)
		{
			StartCoroutine(HandleZombiePresenceDuringGathering());
		}
		// Check if we're waiting for zombie to hide and need to monitor status
		if (currentState == GameState.WaitingForZombieToHide)
		{
			if (zombieHidingSystem != null && zombieHidingSystem.IsHidden)
			{
				// Zombie is hidden, we can proceed to boarding
				StopCoroutine(gameLoopCoroutine);
				gameLoopCoroutine = StartCoroutine(GameLoop());
			}
		}
		else if (currentState == GameState.ZombieBoarding)
		{
			// Check if zombie has successfully unhidden and taken its seat
			if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
			{
				zombieSeated = true;
				Debug.Log("Zombie has seated. Ready to start the ride!");
			}
		}
		else if (currentState == GameState.RideInProgress)
		{
			// Check if all humans are dead - that's now enough to complete the ride
			CheckHumansStatus();

			// If all humans are dead, the ride is complete - no need to check zombie position
			if (allHumansDead)
			{
				currentState = GameState.RideComplete;
				StartCoroutine(CompleteRide());
			}
		}
		else if (currentState == GameState.HumansBoardingTrain)
		{
			if (!boardingCompleted && zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
			{
				Debug.Log("Zombie exposed during active boarding! Canceling process.");
				HandleBoardingFailure();
			}
		}


		// Periodic check for stray humans that aren't in our list
		if (Time.frameCount % 120 == 0)
		{
			// Clean null entries first
			spawnedHumans.RemoveAll(h => h == null);

			// Only count NON-SEATED humans as "actual" for stray detection
			int trackedCount = spawnedHumans.Count;
			int actualCount = FindObjectsOfType<HumanStateController>()
				.Count(h => h != null &&
						   !h.IsDead() &&
						   h.GetComponent<HumanSeatOccupant>()?.IsSeated == false);

			if (actualCount > trackedCount)
			{
				Debug.LogWarning($"Found stray humans: Tracked={trackedCount}, Actual={actualCount}");

				// Only clean up NON-SEATED HUMANS
				foreach (HumanStateController human in FindObjectsOfType<HumanStateController>())
				{
					HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
					if ((occupant == null || !occupant.IsSeated) &&
						!spawnedHumans.Contains(human.gameObject))
					{
						Debug.Log($"Destroying stray human {human.gameObject.name}");
						Destroy(human.gameObject);
					}
				}
			}
		}
	}
	private void HandleBoardingFailure()
	{
		boardingCompleted = false; // ✅ Add this
		if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
		currentState = GameState.WaitingForZombieToHide;
		StartCoroutine(MakeHumansRunAway());
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}
	private IEnumerator GameLoop()
	{
		while (true)
		{
			// PHASE 1: Humans gathering
			currentState = GameState.HumansGathering;
			yield return StartCoroutine(SpawnAndGatherHumans());

			// Always reset hiding when entering waiting state
			if (zombieHidingSystem != null && zombieHidingSystem.IsHidden)
			{
				zombieHidingSystem.UnhideZombie();
			}

			// PHASE 1.5: Wait for zombie hide
			currentState = GameState.WaitingForZombieToHide;
			Debug.Log("Waiting for player to hide the zombie...");

			float hideWaitTime = 0;
			while (!zombieHidingSystem.IsHidden && hideWaitTime < waitForZombieHidingTime)
			{
				hideWaitTime += Time.deltaTime;
				yield return null;
			}

			if (!zombieHidingSystem.IsHidden)
			{
				Debug.Log("Zombie not hidden in time!");
				yield return StartCoroutine(MakeHumansRunAway());
				yield return new WaitForSeconds(restartDelay);
				continue;
			}

			// PHASE 2: Humans boarding
			currentState = GameState.HumansBoardingTrain;
			if (zombieHidingManager != null)
			{
				zombieHidingManager.ForceHideSpotToCenter();
			}
			yield return StartCoroutine(BoardHumansOnCoaster());


			// Check if any humans are seated
			bool humansSeated = false;
			foreach (RollerCoasterCart cart in coasterCarts)
			{
				foreach (RollerCoasterSeat seat in cart.seats)
				{
					if (seat.occupyingHuman != null && !seat.occupyingHuman.IsDead())
					{
						humansSeated = true;
						break;
					}
				}
				if (humansSeated) break;
			}

			// Only proceed if humans are seated
			if (!humansSeated)
			{
				Debug.Log("No humans seated in coaster, restarting game loop...");
				yield return new WaitForSeconds(restartDelay);
				continue; // Skip to next iteration of the loop
			}

			// NEW PHASE: Zombie boarding
			currentState = GameState.ZombieBoarding;
			zombieSeated = false; // Reset zombie seated flag
								  // ADD VALIDATION
			if (!boardingCompleted)
			{
				Debug.LogError("Zombie boarding started prematurely!");
				yield break;
			}
			// Wait for zombie to get seated or timeout
			float waitTime = 0;
			while (!zombieSeated && waitTime < zombieWaitTime)
			{
				waitTime += Time.deltaTime;
				yield return null;
			}

			// If zombie didn't take seat within timeout, we'll proceed anyway
			if (!zombieSeated)
			{
				Debug.LogWarning("Zombie didn't take seat within timeout period!");
			}

			// PHASE 3: Ride in progress
			currentState = GameState.RideInProgress;
			StartRide();

			// Wait for ride completion (all humans dead or other condition)
			yield return new WaitUntil(() => allHumansDead);

			// Only restart if ride is truly complete
			Debug.Log("Ride complete. Restarting game loop...");
			yield return new WaitForSeconds(restartDelay);
		}
	}
	private IEnumerator HandleZombiePresenceDuringGathering()
	{
		isHandlingZombiePresence = true;
		Debug.Log("Zombie detected during gathering!");
		yield return StartCoroutine(MakeHumansRunAway());
		yield return new WaitForSeconds(restartDelay);
		isHandlingZombiePresence = false;

		// Restart game loop
		if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}
	private IEnumerator MakeHumansRunAway()
	{
		Debug.Log("Humans are running away from the visible zombie!");

		List<GameObject> humansToRemove = new List<GameObject>();
		int runningAwayCount = 0;

		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				HumanStateController state = human.GetComponent<HumanStateController>();
				// Skip already seated humans
				if (occupant == null || !occupant.IsSeated)
				{
					humansToRemove.Add(human);
					state?.TriggerPanicInRadius(5f);

					HumanScreamingState screaming = human.GetComponent<HumanScreamingState>();
					screaming?.ScreamAndRunAway(zombie.transform.position);
				}

				runningAwayCount++;

				HumanScreamingState screamingState = human.GetComponent<HumanScreamingState>();
				HumanStateController hsc = human.GetComponent<HumanStateController>();

				if (hsc != null) hsc.TriggerPanicInRadius(5f);

				if (screamingState != null)
				{
					screamingState.ScreamAndRunAway(zombie.transform.position);
				}
				else
				{
					HumanMovementController movement = human.GetComponent<HumanMovementController>();
					if (movement != null)
					{
						Vector2 randomDir = Random.insideUnitCircle.normalized;
						Vector3 fleeTarget = human.transform.position + new Vector3(randomDir.x, randomDir.y, 0) * 10f;
						movement.SetDestination(fleeTarget);
					}
				}
			}
		}

		Debug.Log($"Making {runningAwayCount} humans run away");

		// Wait before cleanup
		yield return new WaitForSeconds(2f);

		// Remove only the humans that actually ran away
		foreach (GameObject human in humansToRemove)
		{
			if (human != null)
			{
				spawnedHumans.Remove(human);
				Destroy(human);
			}
		}

		// Additional cleanup for any remaining nulls
		spawnedHumans.RemoveAll(h => h == null);
	}


	private IEnumerator SpawnAndGatherHumans()
	{
		Debug.Log("Phase 1: Spawning and gathering humans");

		// Clear any remaining humans from previous rounds
		ClearSpawnedHumans();

		// Destroy any stray humans that might still be in the scene but not in our list
		foreach (HumanStateController human in FindObjectsOfType<HumanStateController>())
		{
			if (human != null && human.gameObject != null)
			{
				Destroy(human.gameObject);
			}
		}

		// Wait a frame to ensure cleanup is complete
		yield return null;

		// Spawn exactly the number of humans specified
		spawnedHumans = new List<GameObject>(humansPerRound); // Initialize with capacity

		// Spawn humans at gathering point
		for (int i = 0; i < humansPerRound; i++)
		{
			// Calculate random offset within gathering radius
			Vector2 randomOffset = Random.insideUnitCircle * gatheringRadius;
			Vector3 spawnPos = humanGatheringPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0);

			// Spawn the human
			GameObject humanObj = Instantiate(humanPrefab, spawnPos, Quaternion.identity);
			spawnedHumans.Add(humanObj);

			// Make sure the human has a movement controller
			if (humanObj.GetComponent<HumanMovementController>() == null)
			{
				humanObj.AddComponent<HumanMovementController>();
			}

			// Wait between spawns
			yield return new WaitForSeconds(humanSpawnInterval);
		}

		// Allow some time for humans to gather
		yield return new WaitForSeconds(1.5f);

		// Double check our human count matches what we expect
		if (spawnedHumans.Count > humansPerRound)
		{
			Debug.LogWarning($"Too many humans spawned! Expected {humansPerRound}, got {spawnedHumans.Count}. Cleaning up excess.");
			while (spawnedHumans.Count > humansPerRound)
			{
				int lastIndex = spawnedHumans.Count - 1;
				if (spawnedHumans[lastIndex] != null)
				{
					Destroy(spawnedHumans[lastIndex]);
				}
				spawnedHumans.RemoveAt(lastIndex);
			}
		}
	}

	private IEnumerator BoardHumansOnCoaster()
	{
		boardingCompleted = false;
		Debug.Log("Phase 2: Boarding humans on coaster");

		// CRUCIAL CHECK: Verify zombie is hidden before attempting to board humans
		if (zombieHidingSystem == null || !zombieHidingSystem.IsHidden)
		{
			Debug.LogWarning("Zombie is not hidden! Humans won't board.");
			yield return StartCoroutine(MakeHumansRunAway());
			yield break;
		}

		// Double-check we have the correct number of humans
		int humanCount = spawnedHumans.Count;
		int actualHumanCount = FindObjectsOfType<HumanStateController>().Length;
		if (humanCount != actualHumanCount)
		{
			Debug.LogWarning($"Human count mismatch! Tracked: {humanCount}, Actual: {actualHumanCount}. Cleaning up before boarding.");
			ClearSpawnedHumans();
			yield return StartCoroutine(SpawnAndGatherHumans());
		}

		// Short delay before boarding
		yield return new WaitForSeconds(boardingDelay);

		// Continuous check for zombie hiding status during boarding prep
		if (!zombieHidingSystem.IsHidden)
		{
			Debug.LogWarning("Zombie became visible during boarding prep! Humans won't board.");
			yield return StartCoroutine(MakeHumansRunAway());
			yield break;
		}

		// List to track which seats are filled
		// Get all available seats across all carts except the last one
		List<RollerCoasterSeat> availableSeats = new List<RollerCoasterSeat>();
		for (int i = 0; i < coasterCarts.Count - 1; i++)
		{
			RollerCoasterCart cart = coasterCarts[i];
			foreach (RollerCoasterSeat seat in cart.seats)
			{
				availableSeats.Add(seat);
			}
		}

		int seatsToFill = Mathf.Min(availableSeats.Count, spawnedHumans.Count);
		for (int i = 0; i < seatsToFill; i++)
		{
			GameObject human = spawnedHumans[i];
			RollerCoasterSeat seat = availableSeats[i];

			HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
			if (occupant != null)
			{
				occupant.AssignSeat(seat); // This starts movement
			}
		}
		// Wait for all humans to reach their seats
		yield return new WaitUntil(() =>
			spawnedHumans.All(h =>
				h.GetComponent<HumanMovementController>().HasReachedDestination()
			)
		);
		boardingCompleted = true;
		Debug.Log("All humans seated. Starting zombie boarding phase.");
		currentState = GameState.ZombieBoarding;
	}
	private void StartRide()
	{
		Debug.Log("Phase 3: Starting the ride");

		// Start the roller coaster moving with smooth acceleration
		scrollerManager.StartScrolling(rideSpeed);

		// Reset ride completion flags
		zombieInFrontCart = false;
		allHumansDead = false;
	}

	private IEnumerator CompleteRide()
	{
		Debug.Log("Ride complete - all humans are dead");

		// Stop the coaster
		scrollerManager.StopScrolling();

		// Force hide spot to center with right-to-center movement
		if (zombieHidingManager != null)
		{
			zombieHidingManager.ForceHideSpotToCenter();
		}

		// Wait a moment
		yield return new WaitForSeconds(2f);

		// Move to restarting phase
		currentState = GameState.RideRestarting;
	}
	private void CheckHumansStatus()
	{
		// Find all ACTIVE humans (ignore destroyed ones)
		HumanStateController[] humans = FindObjectsOfType<HumanStateController>()
			.Where(h => h.gameObject.activeInHierarchy)
			.ToArray();

		allHumansDead = (humans.Length == 0) || humans.All(h => h.IsDead());

		if (allHumansDead)
		{
			Debug.Log("All humans are dead! Completing the ride...");
		}
	}

	private void SortCartsByPosition()
	{
		// Sort carts by X position (front carts have higher X)
		coasterCarts.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
	}

	private void ClearSpawnedHumans()
	{
		Debug.Log($"Clearing {spawnedHumans.Count} spawned humans and any strays in the scene");

		// Only destroy NON-SEATED humans
		List<GameObject> humansToRemove = new List<GameObject>();
		foreach (GameObject human in spawnedHumans)
		{
			HumanSeatOccupant occupant = human?.GetComponent<HumanSeatOccupant>();
			if (occupant == null || !occupant.IsSeated)
			{
				humansToRemove.Add(human);
			}
		}

		// Remove and destroy non-seated humans
		foreach (GameObject human in humansToRemove)
		{
			if (human != null)
			{
				spawnedHumans.Remove(human);
				Destroy(human);
			}
		}

		// Clear null entries
		spawnedHumans.RemoveAll(h => h == null);

		// Don't clear seats here - they are managed by the boarding process
	}
	public void RestartGame()
	{
		// Ensure hide spot moves off screen before next round
		if (zombieHidingManager != null)
		{
			zombieHidingManager.ForceHideSpotOffscreen();
		}

		// Set state to restarting
		currentState = GameState.RideRestarting;

		// Restart the game loop
		if (gameLoopCoroutine != null)
		{
			StopCoroutine(gameLoopCoroutine);
		}
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}
	// For visualization in the editor
#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		if (humanGatheringPoint != null)
		{
			// Draw gathering point
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(humanGatheringPoint.position, gatheringRadius);

			// Label
			UnityEditor.Handles.color = Color.white;
			UnityEditor.Handles.Label(humanGatheringPoint.position + Vector3.up * 2f, "Human Gathering Point");
		}

		// Visualize cart order when selected
		if (coasterCarts != null && coasterCarts.Count > 0)
		{
			for (int i = 0; i < coasterCarts.Count; i++)
			{
				if (coasterCarts[i] != null)
				{
					Gizmos.color = (i == 0) ? Color.red : (i == coasterCarts.Count - 1) ? Color.blue : Color.yellow;
					Gizmos.DrawWireCube(coasterCarts[i].transform.position, new Vector3(2f, 1f, 0.1f));

					UnityEditor.Handles.color = Color.white;
					UnityEditor.Handles.Label(
						coasterCarts[i].transform.position + Vector3.up * 1.5f,
						$"Cart {i} {(i == 0 ? "(Front)" : i == coasterCarts.Count - 1 ? "(Zombie Start)" : "")}"
					);
				}
			}
		}

		// Show current game state
		if (Application.isPlaying)
		{
			UnityEditor.Handles.color = Color.white;
			string stateInfo = $"Game State: {currentState}";

			// Add zombie status to the display
			if (zombieHidingSystem != null)
			{
				stateInfo += $" - Zombie: {(zombieHidingSystem.IsHidden ? "Hidden" : "Visible")}";
			}

			// Add more specific state info
			if (currentState == GameState.ZombieBoarding)
			{
				stateInfo += zombieSeated ? " (Zombie Seated)" : " (Waiting for Zombie)";
				if (zombieHidingSystem != null && zombieHidingSystem.IsHidden)
				{
					Debug.Log("Zombie re-hidden during boarding! Resetting process.");
					zombieSeated = false;
					currentState = GameState.WaitingForZombieToHide;
					if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
					gameLoopCoroutine = StartCoroutine(GameLoop());
				}
			}
			else if (currentState == GameState.WaitingForZombieToHide)
			{
				stateInfo += " (Player needs to hide zombie)";
			}

			// Show scrolling status
			if (scrollerManager != null)
			{
				stateInfo += $" - Scrolling: {(scrollerManager.IsStopped() ? "Stopped" : "Moving")}";
				if (scrollerManager.IsTransitioning())
				{
					stateInfo += " (Transitioning)";
				}
			}

			UnityEditor.Handles.Label(
				Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, 10f)),
				stateInfo
			);
		}
	}
#endif
}