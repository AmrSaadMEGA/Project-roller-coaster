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
	[SerializeField] private GameObject[] humanPrefabs; // Changed to array for multiple prefabs
	[SerializeField] private int humansPerRound = 8;
	[SerializeField] private float humanSpawnInterval = 0.5f;
	[SerializeField] private float gatheringRadius = 2f;
	// Add these enums at the top of your class
	public enum SpawnDirection { Horizontal, Vertical }
	public enum SpawnAlignment { Center, LeftRight, TopBottom }

	[Header("Human Spawning Configuration")]
	[SerializeField] private SpawnDirection spawnDirection = SpawnDirection.Horizontal;
	[SerializeField] private SpawnAlignment spawnAlignment = SpawnAlignment.Center;
	[SerializeField][Range(0, 1)] private float spawnDensity = 0.8f; // 0 = spread out, 1 = tight group
	[SerializeField] private bool useRandomPositions = true;

	[Header("Coaster")]
	[SerializeField] private List<RollerCoasterCart> coasterCarts = new List<RollerCoasterCart>();
	[SerializeField] private float boardingDelay = 1.5f;
	[SerializeField] private float rideSpeed = 5f;
	[SerializeField] private float restartDelay = 3f;
	[SerializeField] private float zombieWaitTime = 2f;

	// Private variables
	private List<GameObject> spawnedHumans = new List<GameObject>();
	private bool allHumansDead = false;
	private float originalScrollSpeed;
	private bool zombieSeated = false;
	private Coroutine gameLoopCoroutine;

	bool isHandlingZombiePresence;
	// Game states

	// Add new variable
	private bool boardingCompleted;

	private bool _isLossDuringRide;
	public bool IsLossDuringRide => _isLossDuringRide; // Expose through property
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
		// Validate human prefabs array
		if (humanPrefabs == null || humanPrefabs.Length == 0)
		{
			Debug.LogError("Assign at least one human prefab in humanPrefabs array!");
			enabled = false;
			return;
		}
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
			zombieHidingManager = FindFirstObjectByType<ZombieHidingManager>();
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
		if (currentState == GameState.HumansBoardingTrain)
		{
			if (!boardingCompleted && zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
			{
				Debug.Log("Zombie exposed during active boarding! Canceling process.");
				HandleBoardingFailure();
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


		// Periodic check every 2 seconds
		if (Time.frameCount % 60 == 0 && currentState == GameState.HumansGathering)
		{
			// Clean null entries
			spawnedHumans.RemoveAll(h => h == null);

			// Count non-seated humans
			int actualCount = Object.FindObjectsByType<HumanStateController>(
				FindObjectsInactive.Include,
				FindObjectsSortMode.None
			).Count(h => !h.IsDead() &&
					   h.GetComponent<HumanSeatOccupant>()?.IsSeated == false);

			int trackedCount = spawnedHumans.Count;

			// Handle overcount (stray humans)
			if (actualCount > trackedCount)
			{
				Debug.Log($"Cleaning {actualCount - trackedCount} stray humans");
				foreach (HumanStateController human in Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (!spawnedHumans.Contains(human.gameObject) &&
						human.GetComponent<HumanSeatOccupant>()?.IsSeated == false)
					{
						Destroy(human.gameObject);
					}
				}
			}
			// Handle undercount (missing humans)
			else if (trackedCount < humansPerRound)
			{
				Debug.Log($"Respawning {humansPerRound - trackedCount} missing humans");
				StartCoroutine(SpawnMissingHumans(humansPerRound - trackedCount));
			}
		}
	}
	private IEnumerator SpawnMissingHumans(int count)
	{
		Vector3 basePosition = humanGatheringPoint.position;
		bool isHorizontal = spawnDirection == SpawnDirection.Horizontal;
		float lineLength = gatheringRadius * 2;

		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = CalculateSpawnPosition(spawnedHumans.Count + i,
				basePosition, isHorizontal, lineLength);

			// Random prefab selection
			GameObject selectedPrefab = humanPrefabs[Random.Range(0, humanPrefabs.Length)];
			GameObject humanObj = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);

			// Random facing direction
			if (Random.value < 0.5f)
			{
				Vector3 newScale = humanObj.transform.localScale;
				newScale.x *= -1;
				humanObj.transform.localScale = newScale;
			}

			spawnedHumans.Add(humanObj);
			yield return new WaitForSeconds(humanSpawnInterval);
		}
	}
	public void ClearAllSeats()
	{
		Debug.Log("Clearing all seats...");
		foreach (RollerCoasterCart cart in coasterCarts)
		{
			foreach (RollerCoasterSeat seat in cart.seats)
			{
				// First check and update the human occupant if exists
				if (seat.occupyingHuman != null)
				{
					HumanSeatOccupant occupant = seat.occupyingHuman.GetComponent<HumanSeatOccupant>();
					if (occupant != null)
					{
						occupant.LeaveSeat();
					}
				}

				// Then clear the seat references
				seat.occupyingHuman = null;
				seat.SetZombieOccupation(false);
			}
		}
	}
	private void HandleBoardingFailure()
	{
		boardingCompleted = false;
		if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
		currentState = GameState.WaitingForZombieToHide;

		// ADDED: Make sure ALL humans scream before running away
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
				if (screamState != null && !screamState.IsScreaming)
				{
					screamState.ScreamAndRunAway(zombie.transform.position, true);
				}
			}
		}

		StartCoroutine(MakeHumansRunAway());
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}
	private IEnumerator GameLoop()
	{
		while (true)
		{
			// PHASE 1: Humans gathering
			yield return new WaitForSeconds(restartDelay);
			currentState = GameState.HumansGathering;
			yield return StartCoroutine(SpawnAndGatherHumans());

			// PHASE 1.5: Wait for zombie hide
			currentState = GameState.WaitingForZombieToHide;
			Debug.Log("Waiting for player to hide the zombie...");
			// Wait until the zombie is hidden
			yield return new WaitUntil(() => zombieHidingSystem.IsHidden);

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

			// NEW: Proceed to zombie boarding ONLY after humans finish
			if (boardingCompleted)
			{
				currentState = GameState.ZombieBoarding;
				// Allow zombie to unhide now
				zombieHidingSystem.UnhideZombie();
				yield return new WaitUntil(() => !zombieHidingSystem.IsHidden);
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

		// MODIFIED: Add explicit screaming triggers for all humans
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanScreamingState screaming = human.GetComponent<HumanScreamingState>();
				if (screaming != null)
				{
					// Force scream and specify the zombie's position as the threat
					screaming.ScreamAndRunAway(zombie.transform.position, true);
				}

				// Trigger panic to affect nearby humans
				HumanStateController state = human.GetComponent<HumanStateController>();
				if (state != null)
				{
					state.TriggerPanicInRadius(7f); // Increased radius for better effect
				}
			}
		}

		yield return StartCoroutine(MakeHumansRunAway());
		yield return new WaitForSeconds(restartDelay);
		isHandlingZombiePresence = false;

		// Restart game loop
		if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}
	private IEnumerator MakeHumansRunAway()
	{
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				// Explicitly allow screaming for intentional fleeing
				HumanScreamingState screaming = human.GetComponent<HumanScreamingState>();
				screaming?.ScreamAndRunAway(zombie.transform.position);
			}
		}

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
		// Now clear with despawn flag
		ClearSpawnedHumans();
	}


	private IEnumerator SpawnAndGatherHumans()
	{
		Debug.Log("Phase 1: Spawning and gathering humans");
		ClearSpawnedHumans();

		// Initial spawn loop - do batch spawning for better performance
		Vector3 basePosition = humanGatheringPoint.position;
		bool isHorizontal = spawnDirection == SpawnDirection.Horizontal;
		float lineLength = gatheringRadius * 2;

		// Spawn all humans at once with less delay
		for (int i = 0; i < humansPerRound; i++)
		{
			// Calculate position
			Vector3 spawnPos = CalculateSpawnPosition(i, basePosition, isHorizontal, lineLength);

			// Random prefab selection
			GameObject selectedPrefab = humanPrefabs[Random.Range(0, humanPrefabs.Length)];

			// Spawn the human
			GameObject humanObj = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);

			// Random facing direction
			if (Random.value < 0.5f)
			{
				Vector3 newScale = humanObj.transform.localScale;
				newScale.x *= -1;
				humanObj.transform.localScale = newScale;
			}

			spawnedHumans.Add(humanObj);

			// Make sure the human has a movement controller
			if (humanObj.GetComponent<HumanMovementController>() == null)
			{
				humanObj.AddComponent<HumanMovementController>();
			}

			// Only yield every 3 humans for faster spawning
			if (i % 3 == 0)
				yield return new WaitForSeconds(humanSpawnInterval);
		}

		// Final cleanup - to handle any stray humans
		spawnedHumans.RemoveAll(h => h == null);

		// Add an explicit cleanup step to remove any excess humans
		int humanCount = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
		if (humanCount > humansPerRound)
		{
			Debug.Log($"Cleaning up {humanCount - humansPerRound} excess humans");
			HumanStateController[] allHumans = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (HumanStateController human in allHumans)
			{
				if (!spawnedHumans.Contains(human.gameObject))
				{
					Destroy(human.gameObject);
				}
			}
		}

		// Shorter wait time before proceeding
		yield return new WaitForSeconds(0.5f);
	}

	private IEnumerator CleanupUnboardedHumans()
	{
		// Wait a short time for boarding to start
		yield return new WaitForSeconds(1.0f);

		// Find and clean all unboarded humans - this is a more aggressive approach
		List<GameObject> humansToRemove = new List<GameObject>();

		// APPROACH #1: Get all humans that aren't going to be seated
		HumanStateController[] allHumans = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include,FindObjectsSortMode.None);

		// First count how many seats we're using
		int availableSeatCount = 0;
		for (int i = 0; i < coasterCarts.Count - 1; i++) // Skip last cart (reserved for zombie)
		{
			availableSeatCount += coasterCarts[i].seats.Length;
		}

		// Get list of humans assigned to seats (these should be kept)
		List<GameObject> seatedOrBoardingHumans = new List<GameObject>();
		foreach (RollerCoasterCart cart in coasterCarts)
		{
			foreach (RollerCoasterSeat seat in cart.seats)
			{
				if (seat.occupyingHuman != null)
				{
					seatedOrBoardingHumans.Add(seat.occupyingHuman.gameObject);
				}
			}
		}

		// Also get humans that are in the process of boarding
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				if (occupant != null && occupant.AssignedSeat != null)
				{
					seatedOrBoardingHumans.Add(human);
				}
			}
		}

		// Find humans to remove (any human not in seated/boarding list)
		foreach (HumanStateController human in allHumans)
		{
			if (human != null && !seatedOrBoardingHumans.Contains(human.gameObject))
			{
				// Double check this isn't a human with a seat assignment
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				if (occupant == null || occupant.AssignedSeat == null)
				{
					humansToRemove.Add(human.gameObject);
				}
			}
		}

		// Clean up any excess humans
		if (humansToRemove.Count > 0)
		{
			Debug.Log($"Cleaning up {humansToRemove.Count} unboarded humans");
			foreach (GameObject human in humansToRemove)
			{
				if (human != null)
				{
					// Remove from spawnedHumans if it's there
					if (spawnedHumans.Contains(human))
					{
						spawnedHumans.Remove(human);
					}
					Destroy(human);
				}
			}
		}

		// Perform a second cleanup after a delay to catch any stragglers
		yield return new WaitForSeconds(2.0f);

		// Find any humans that aren't seated after the boarding process
		HumanStateController[] remainingHumans = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
		int cleanedUp = 0;

		foreach (HumanStateController human in remainingHumans)
		{
			if (human != null)
			{
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				// If not seated and not in a cart, remove it
				if (occupant == null || !occupant.IsSeated)
				{
					// Remove from spawnedHumans if it's there
					if (spawnedHumans.Contains(human.gameObject))
					{
						spawnedHumans.Remove(human.gameObject);
					}
					Destroy(human.gameObject);
					cleanedUp++;
				}
			}
		}

		if (cleanedUp > 0)
		{
			Debug.Log($"Second cleanup pass removed {cleanedUp} stray humans");
		}
	}

	private Vector3 CalculateSpawnPosition(int index, Vector3 basePos, bool isHorizontal, float lineLength)
	{
		float spacing = lineLength / Mathf.Max(1, humansPerRound - 1);
		float densitySpacing = spacing * spawnDensity;
		float positionOffset = 0;

		// Calculate base offset
		if (useRandomPositions)
		{
			positionOffset = Random.Range(-gatheringRadius, gatheringRadius);
		}
		else
		{
			positionOffset = -gatheringRadius + (index * densitySpacing);
		}

		// Apply alignment
		switch (spawnAlignment)
		{
			case SpawnAlignment.LeftRight when isHorizontal:
			case SpawnAlignment.TopBottom when !isHorizontal:
				positionOffset = Mathf.Clamp(positionOffset, -gatheringRadius, gatheringRadius);
				break;

			case SpawnAlignment.Center:
				positionOffset -= lineLength * 0.5f;
				break;
		}

		// Create final position
		if (isHorizontal)
		{
			return basePos + new Vector3(positionOffset, 0, 0);
		}
		else
		{
			return basePos + new Vector3(0, positionOffset, 0);
		}
	}

	private IEnumerator BoardHumansOnCoaster()
	{
		boardingCompleted = false;
		Debug.Log("Phase 2: Boarding humans on coaster");
		// Do an initial cleanup of any stray humans before boarding
		HumanStateController[] initialHumans = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
		foreach (HumanStateController human in initialHumans)
		{
			if (human != null && !spawnedHumans.Contains(human.gameObject))
			{
				Destroy(human.gameObject);
			}
		}
		// FIXED: More reliable zombie visibility check
		// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
		if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
		{
			Debug.LogWarning("Zombie is visible! Canceling boarding.");

			// ADDED: Make humans scream before handling failure
			foreach (GameObject human in spawnedHumans)
			{
				if (human != null)
				{
					HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
					if (screamState != null)
					{
						screamState.ScreamAndRunAway(zombie.transform.position, true);
					}
				}
			}

			HandleBoardingFailure();
			yield break;
		}

		// Double-check human count
		spawnedHumans.RemoveAll(h => h == null);
		int needed = humansPerRound - spawnedHumans.Count;
		if (needed != 0)
		{
			Debug.Log($"Adjusting human count from {spawnedHumans.Count} to {humansPerRound}");

			// Remove excess
			while (spawnedHumans.Count > humansPerRound)
			{
				GameObject human = spawnedHumans.Last();
				if (human != null) Destroy(human);
				spawnedHumans.RemoveAt(spawnedHumans.Count - 1);
			}

			// Spawn missing
			if (needed > 0)
			{
				Vector3 basePosition = humanGatheringPoint.position;
				bool isHorizontal = spawnDirection == SpawnDirection.Horizontal;
				float lineLength = gatheringRadius * 2;

				for (int i = 0; i < needed; i++)
				{
					Vector3 spawnPos = CalculateSpawnPosition(spawnedHumans.Count + i,
						basePosition, isHorizontal, lineLength);
					GameObject selectedPrefab = humanPrefabs[Random.Range(0, humanPrefabs.Length)];
					GameObject humanObj0 = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
					spawnedHumans.Add(humanObj0);
					yield return new WaitForSeconds(humanSpawnInterval);
				}
			}
		}

		// Short delay before boarding
		yield return new WaitForSeconds(boardingDelay);
		// FIXED: More reliable zombie visibility check
		// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
		if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
		{
			Debug.LogWarning("Zombie became visible during boarding prep! Humans won't board.");
			yield return StartCoroutine(MakeHumansRunAway());
			yield break;
		}

		// Force any HumanScreamingState components to stop screaming
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
				if (screamState != null)
				{
					// Reset any screaming state that might be active
					screamState.StopAllCoroutines();
				}
			}
		}

		// List to track which seats are filled
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
		for (int i = seatsToFill; i < spawnedHumans.Count; i++)
		{
			if (spawnedHumans[i] != null)
			{
				// Mark these for destruction in next cleanup
				HumanStateController controller = spawnedHumans[i].GetComponent<HumanStateController>();
				if (controller != null)
				{
					controller.IsBeingDespawned = true;
				}
			}
		}

		// Start zombie check coroutine
		Coroutine zombieCheckCoroutine = StartCoroutine(CheckForZombieVisibilityDuringBoarding());

		// FIXED: Add a debug message for clarity
		Debug.Log("Humans are moving to seats. Waiting for boarding to complete...");

		// Wait for all humans to reach destinations or react to zombie
		while (!spawnedHumans.All(h =>
			h == null ||
			h.GetComponent<HumanSeatOccupant>()?.IsSeated == true ||
			h.GetComponent<HumanMovementController>().HasReachedDestination()))
		{
			// FIXED: More reliable zombie visibility check
			// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
			bool anyHumanReacting = spawnedHumans.Any(h =>
				h != null &&
				h.GetComponent<HumanScreamingState>()?.IsScreaming == true);

			if (anyHumanReacting || (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding))
			{
				Debug.Log("Humans detected zombie during boarding movement!");
				StopCoroutine(zombieCheckCoroutine); // Stop the other check
				HandleBoardingFailure();
				yield break;
			}

			yield return new WaitForSeconds(0.1f); // Check more frequently
		}

		// Final check after reaching destinations
		// FIXED: More reliable zombie visibility check
		// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
		if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
		{
			Debug.Log("Zombie revealed after humans reached seats!");
			StopCoroutine(zombieCheckCoroutine);
			HandleBoardingFailure();
			yield break;
		}

		// Stop the visibility check coroutine
		if (zombieCheckCoroutine != null)
			StopCoroutine(zombieCheckCoroutine);
		StartCoroutine(CleanupUnboardedHumans());
		boardingCompleted = true;
		Debug.Log("All humans seated. Starting zombie boarding phase.");
		currentState = GameState.ZombieBoarding;
	}
	private IEnumerator CheckForZombieVisibilityDuringBoarding()
	{
		while (currentState == GameState.HumansBoardingTrain && !boardingCompleted)
		{
			// FIXED: More reliable zombie visibility check
			// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
			if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
			{
				Debug.Log("Zombie became visible during boarding - triggering human panic!");

				// MODIFIED: Force ALL humans to scream, not just moving ones
				foreach (GameObject human in spawnedHumans)
				{
					if (human == null) continue;

					// Get screaming component and force scream
					HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
					if (screamState != null)
					{
						// Force immediate scream with forceScream=true
						screamState.ScreamAndRunAway(zombieHidingSystem.transform.position, true);
					}

					// Also trigger panic to spread the effect
					HumanStateController stateController = human.GetComponent<HumanStateController>();
					if (stateController != null)
					{
						stateController.TriggerPanicInRadius(5f);
					}
				}

				yield return new WaitForSeconds(0.5f);
				HandleBoardingFailure();
				yield break;
			}

			yield return new WaitForSeconds(0.1f); // Check frequently
		}
	}
	// RollerCoasterGameManager.cs
	// In RollerCoasterGameManager.cs - Modify the GameOverDueToHunger method
	public void GameOverDueToHunger()
	{
		Debug.Log("Game Over - Zombie starved!");

		// Clear all seats first
		ClearAllSeats();

		// Immediately destroy all humans
		foreach (GameObject human in spawnedHumans.ToList())
		{
			if (human != null)
			{
				// Force leave seat before destruction
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				if (occupant != null)
				{
					occupant.LeaveSeat();
				}
				Destroy(human);
			}
		}
		spawnedHumans.Clear();

		// Reset boarding states
		boardingCompleted = false;
		zombieSeated = false;

		// Stop scroller
		scrollerManager.StopScrolling();

		// Update state - set to RideComplete to ensure animation works correctly
		currentState = GameState.RideComplete;
		_isLossDuringRide = (currentState == GameState.RideInProgress);

		// Reset hunger system
		ZombieHungerSystem hungerSystem = FindFirstObjectByType<ZombieHungerSystem>();
		hungerSystem.ResetHungerSystem();

		// Move the hiding spot from offscreen right to center
		if (zombieHidingManager != null)
		{
			HideSpotMovementSystem movementSystem = zombieHidingManager.GetComponent<HideSpotMovementSystem>();
			if (movementSystem != null)
			{

				// Position at offscreen right and start animation
				movementSystem.SetHideSpotPosition(movementSystem.OffscreenRightPosition);
				movementSystem.MoveHideSpotToCenter();
			}
		}

		// Restart game flow
		StopAllCoroutines();
		StartCoroutine(GameLose());
	}
	private void StartRide()
	{
		Debug.Log("Phase 3: Starting the ride");

		// Start the roller coaster moving with smooth acceleration
		scrollerManager.StartScrolling(rideSpeed);

		// Reset ride completion flags
		allHumansDead = false;
	}

	private IEnumerator GameLose()
	{
		yield return new WaitForSeconds(3);
		yield return StartCoroutine(GameLoop());
	}
	// In RollerCoasterGameManager.cs
	private IEnumerator CompleteRide()
	{
		Debug.Log("Ride complete - all humans are dead");
		scrollerManager.StopScrolling();

		// Force the zombie hiding spot to animate from right to center
		if (zombieHidingManager != null)
		{
			// First make sure we start from the right position
			HideSpotMovementSystem movementSystem = zombieHidingManager.GetComponent<HideSpotMovementSystem>();
			if (movementSystem != null)
			{

				// Start animation from right to center
				movementSystem.MoveHideSpotToCenter();

				// Wait for animation to complete
				Debug.Log("Waiting for hide spot animation to complete...");
				yield return null;
			}
		}
	}
	private void CheckHumansStatus()
	{
		// Find all ACTIVE humans (ignore destroyed ones)
		HumanStateController[] humans = Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include,FindObjectsSortMode.None)
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
		Debug.Log($"Clearing {spawnedHumans.Count} spawned humans");

		foreach (GameObject human in spawnedHumans.ToList())
		{
			if (human != null)
			{
				// Set despawn flag
				HumanStateController state = human.GetComponent<HumanStateController>();
				if (state != null) state.IsBeingDespawned = true;

				// Force leave seat before destruction
				HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
				if (occupant != null) occupant.LeaveSeat();

				Destroy(human);
			}
		}
		spawnedHumans.Clear();

		// Cleanup any remaining humans in the scene
		foreach (HumanStateController human in Object.FindObjectsByType<HumanStateController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
		{
			if (human != null && !spawnedHumans.Contains(human.gameObject))
			{
				Destroy(human.gameObject);
			}
		}
	}
}