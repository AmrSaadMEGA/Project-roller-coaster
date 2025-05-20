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

	private bool _isLossDuringRide;
	public enum SpawnDirection { Horizontal, Vertical }
	public enum SpawnAlignment { Center, LeftRight, TopBottom }
	[Header("Human Spawning Configuration")]
	[SerializeField] private SpawnDirection spawnDirection = SpawnDirection.Horizontal;
	[SerializeField] private SpawnAlignment spawnAlignment = SpawnAlignment.Center;
	[SerializeField][Range(0, 1)] private float spawnDensity = 0.8f; // 0 = spread out, 1 = tight group
	[SerializeField] private bool useRandomPositions = true;
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
	public void ClearAllSeats()
	{
		foreach (RollerCoasterCart cart in coasterCarts)
		{
			foreach (RollerCoasterSeat seat in cart.seats)
			{
				seat.occupyingHuman = null;
				seat.SetZombieOccupation(false);

				// Reset human occupant if exists
				if (seat.occupyingHuman != null)
				{
					HumanSeatOccupant occupant = seat.occupyingHuman.GetComponent<HumanSeatOccupant>();
					if (occupant != null)
					{
						occupant.LeaveSeat();
					}
				}
			}
		}
	}
	private void HandleBoardingFailure()
	{
		boardingCompleted = false;
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

		float lineLength = gatheringRadius * 2;
		Vector3 basePosition = humanGatheringPoint.position;
		bool isHorizontal = spawnDirection == SpawnDirection.Horizontal;

		for (int i = 0; i < humansPerRound; i++)
		{
			Vector3 spawnPos = CalculateSpawnPosition(i, basePosition, isHorizontal, lineLength);

			// Add random positional offsets
			if (useRandomPositions)
			{
				float posJitter = gatheringRadius * (1 - spawnDensity) * 0.5f;
				if (isHorizontal)
				{
					spawnPos.x += Random.Range(-posJitter, posJitter);
					spawnPos.y += Random.Range(-0.3f, 0.3f);
				}
				else
				{
					spawnPos.y += Random.Range(-posJitter, posJitter);
					spawnPos.x += Random.Range(-0.3f, 0.3f);
				}
			}

			// Add rotation variation
			GameObject humanObj = Instantiate(humanPrefab, spawnPos, Quaternion.identity);
			humanObj.transform.Rotate(0, 0, Random.Range(-5f, 5f));

			// Spawn exactly the number of humans specified
			spawnedHumans = new List<GameObject>(humansPerRound); // Initialize with capacity

			// Spawn humans at gathering point
			for (int j = 0; j < humansPerRound; j++)
			{
				// Calculate random offset within gathering radius
				Vector2 randomOffset = Random.insideUnitCircle * gatheringRadius;
				spawnPos = humanGatheringPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0);

				// Spawn the human
				GameObject humanObj0 = Instantiate(humanPrefab, spawnPos, Quaternion.identity);
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

		// FIXED: More reliable zombie visibility check
		// Only react if zombie is DEFINITELY visible (not hidden AND not in the process of hiding)
		if (!zombieHidingSystem.IsHidden && !zombieHidingSystem.IsHiding)
		{
			Debug.LogWarning("Zombie is visible! Canceling boarding.");
			HandleBoardingFailure();
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
		StopCoroutine(zombieCheckCoroutine);

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

				// Force all moving humans to immediately react
				foreach (GameObject human in spawnedHumans)
				{
					if (human == null) continue;

					HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
					if (occupant != null && !occupant.IsSeated)
					{
						HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
						if (screamState != null)
						{
							screamState.ScreamAndRunAway(zombieHidingSystem.transform.position, true);
						}
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
		ZombieHungerSystem hungerSystem = FindObjectOfType<ZombieHungerSystem>();
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
		zombieInFrontCart = false;
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

		RestartGame();
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
		Debug.Log($"Clearing {spawnedHumans.Count} spawned humans");

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

		// Cleanup any remaining humans in the scene
		foreach (HumanStateController human in FindObjectsOfType<HumanStateController>())
		{
			if (human != null && !spawnedHumans.Contains(human.gameObject))
			{
				Destroy(human.gameObject);
			}
		}
	}
	public void RestartGame()
	{
	}
	// For visualization in the editor
#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		if (humanGatheringPoint != null)
		{
			bool isHorizontal = spawnDirection == SpawnDirection.Horizontal;
			Vector3 lineStart = humanGatheringPoint.position;
			Vector3 lineEnd = humanGatheringPoint.position;
			Vector3 size = Vector3.zero;

			// Calculate line dimensions based on direction
			if (isHorizontal)
			{
				lineStart.x -= gatheringRadius;
				lineEnd.x += gatheringRadius;
				size = new Vector3(gatheringRadius * 2, 0.6f, 0.1f);
			}
			else
			{
				lineStart.y -= gatheringRadius;
				lineEnd.y += gatheringRadius;
				size = new Vector3(0.6f, gatheringRadius * 2, 0.1f);
			}

			// Apply alignment offsets
			switch (spawnAlignment)
			{
				case SpawnAlignment.Center:
					// Already centered
					break;

				case SpawnAlignment.LeftRight when isHorizontal:
					lineStart.x = humanGatheringPoint.position.x;
					lineEnd.x = humanGatheringPoint.position.x + gatheringRadius * 2;
					size.x = gatheringRadius * 2;
					break;

				case SpawnAlignment.TopBottom when !isHorizontal:
					lineStart.y = humanGatheringPoint.position.y;
					lineEnd.y = humanGatheringPoint.position.y + gatheringRadius * 2;
					size.y = gatheringRadius * 2;
					break;
			}

			// Draw main line
			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(lineStart, lineEnd);

			// Draw spawn area box
			Gizmos.color = new Color(1, 0, 1, 0.15f);
			Gizmos.DrawCube(humanGatheringPoint.position, size);

			// Label
			string alignmentText = spawnAlignment switch
			{
				SpawnAlignment.Center => "Centered",
				SpawnAlignment.LeftRight => isHorizontal ? "Left to Right" : "Top to Bottom",
				_ => "Custom"
			};

			UnityEditor.Handles.Label(humanGatheringPoint.position + Vector3.up * 2f,
				$"Human Spawn Line\n" +
				$"Direction: {spawnDirection}\n" +
				$"Alignment: {alignmentText}\n" +
				$"Density: {spawnDensity:0.00}\n" +
				$"Random: {useRandomPositions}");
		}

		// ... rest of existing gizmo code ...
#endif
	}
}