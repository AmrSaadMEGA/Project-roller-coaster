using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RollerCoasterGameManager : MonoBehaviour
{
	[Header("Game State")]
	[SerializeField] private GameState currentState = GameState.HumansGathering;

	[Header("References")]
	[SerializeField] private InfiniteRailScroller railScroller;
	[SerializeField] private Transform humanGatheringPoint;
	[SerializeField] private ZombieController zombie;
	[SerializeField] private ZombieHidingSystem zombieHidingSystem; // Added reference to ZombieHidingSystem

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
	[SerializeField] private float zombieWaitTime = 2f; // Added wait time for zombie to take seat
	[SerializeField] private float waitForZombieHidingTime = 5f; // Time to wait for player to hide zombie

	// Private variables
	private List<GameObject> spawnedHumans = new List<GameObject>();
	private bool zombieInFrontCart = false; // Keeping this for backward compatibility
	private bool allHumansDead = false;
	private float originalScrollSpeed;
	private bool zombieSeated = false; // New flag to track if zombie is seated
	private Coroutine gameLoopCoroutine; // Store reference to allow stopping

	// Game states
	public enum GameState
	{
		HumansGathering,
		WaitingForZombieToHide, // New state for waiting for player to hide zombie
		HumansBoardingTrain,
		ZombieBoarding, // New state for zombie boarding
		RideInProgress,
		RideComplete,
		RideRestarting
	}

	// Public accessor for current state
	public GameState CurrentState => currentState;

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

		// Set rail scroller on zombie hiding system
		var railScrollerField = zombieHidingSystem.GetType().GetField("railScroller",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (railScrollerField != null)
		{
			railScrollerField.SetValue(zombieHidingSystem, railScroller);
		}

		// Store original scroll speed
		originalScrollSpeed = railScroller.scrollSpeed;
	}

	private void Start()
	{
		// Start with the coaster not moving
		railScroller.scrollSpeed = 0f;

		// Sort carts from front to back (highest to lowest X position)
		SortCartsByPosition();

		// Begin the game flow
		gameLoopCoroutine = StartCoroutine(GameLoop());
	}

	private void Update()
	{
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
			// If zombie becomes visible during boarding, humans should run away
			if (zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
			{
				// Stop the current game loop
				if (gameLoopCoroutine != null)
				{
					StopCoroutine(gameLoopCoroutine);
				}

				// Set state back to waiting for zombie to hide
				currentState = GameState.WaitingForZombieToHide;

				// Cause humans to run away
				StartCoroutine(MakeHumansRunAway());

				// Restart game loop after a delay
				StartCoroutine(RestartGameLoopAfterDelay(restartDelay));
			}
		}

		// Periodic check for stray humans that aren't in our list
		if (Time.frameCount % 120 == 0) // Check roughly every 2 seconds at 60FPS
		{
			int trackedCount = spawnedHumans.Count;
			int actualCount = FindObjectsOfType<HumanStateController>().Length;

			// If we have more humans in the scene than in our list, there are strays
			if (actualCount > trackedCount)
			{
				Debug.LogWarning($"Found stray humans: Tracked={trackedCount}, Actual={actualCount}");

				// If in gathering phase, just fix our list
				if (currentState == GameState.HumansGathering)
				{
					// Add all humans to our tracked list to ensure proper cleanup later
					HumanStateController[] allHumans = FindObjectsOfType<HumanStateController>();
					foreach (HumanStateController human in allHumans)
					{
						if (human != null && human.gameObject != null)
						{
							// Check if this human is already in our list
							bool found = false;
							foreach (GameObject trackedHuman in spawnedHumans)
							{
								if (trackedHuman == human.gameObject)
								{
									found = true;
									break;
								}
							}

							// If not found, add it
							if (!found)
							{
								Debug.Log($"Adding stray human {human.gameObject.name} to tracked list");
								spawnedHumans.Add(human.gameObject);
							}
						}
					}
				}
				// Otherwise, clean up immediately
				else
				{
					Debug.Log("Cleaning up stray humans");
					HumanStateController[] allHumans = FindObjectsOfType<HumanStateController>();
					foreach (HumanStateController human in allHumans)
					{
						// Check if this human is in our tracked list
						bool found = false;
						foreach (GameObject trackedHuman in spawnedHumans)
						{
							if (trackedHuman == human.gameObject)
							{
								found = true;
								break;
							}
						}

						// If not in our list, it's a stray
						if (!found && human != null && human.gameObject != null)
						{
							Debug.Log($"Destroying stray human {human.gameObject.name}");
							Destroy(human.gameObject);
						}
					}
				}
			}
		}
	}

	private IEnumerator GameLoop()
	{
		while (true)
		{
			// PHASE 1: Humans gathering
			currentState = GameState.HumansGathering;
			yield return StartCoroutine(SpawnAndGatherHumans());

			// ALWAYS WAIT FOR ZOMBIE TO HIDE before proceeding to boarding
			// This is the crucial change to ensure humans don't board until zombie is hidden
			if (zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
			{
				currentState = GameState.WaitingForZombieToHide;
				Debug.Log("Waiting for player to hide the zombie...");

				// Wait for the player to hide the zombie up to a max time
				float waitTime0 = 0;
				while (!zombieHidingSystem.IsHidden && waitTime0 < waitForZombieHidingTime)
				{
					waitTime0 += Time.deltaTime;
					yield return null;
				}

				// If zombie is still not hidden, cause humans to run away
				if (!zombieHidingSystem.IsHidden)
				{
					Debug.Log("Player didn't hide the zombie in time! Humans are running away.");
					yield return StartCoroutine(MakeHumansRunAway());

					// Restart the game loop after a delay
					yield return new WaitForSeconds(restartDelay);
					continue; // Skip to next iteration of the loop
				}
			}

			// PHASE 2: Humans boarding coaster (only if zombie is hidden)
			// Double-check zombie is hidden before proceeding
			if (!zombieHidingSystem.IsHidden)
			{
				Debug.LogWarning("Zombie not hidden before boarding phase! Restarting game loop...");
				yield return new WaitForSeconds(restartDelay);
				continue; // Skip to next iteration of the loop
			}

			currentState = GameState.HumansBoardingTrain;
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

			// Wait until the ride completes (set by Update)
			yield return new WaitUntil(() => currentState == GameState.RideRestarting);

			// Loop back and start again
			Debug.Log("Game loop restarting...");
			yield return new WaitForSeconds(restartDelay);
		}
	}

	private IEnumerator MakeHumansRunAway()
	{
		Debug.Log("Humans are running away from the visible zombie!");

		// Track how many humans we're making run away
		int runningAwayCount = spawnedHumans.Count;
		Debug.Log($"Making {runningAwayCount} humans run away");

		// Make all humans run away
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				HumanScreamingState screamingState = human.GetComponent<HumanScreamingState>();
				if (screamingState != null)
				{
					screamingState.ScreamAndRunAway(zombie.transform.position);
				}
				else
				{
					// Fallback if no screaming state component
					HumanMovementController movement = human.GetComponent<HumanMovementController>();
					if (movement != null)
					{
						// Calculate random flee direction
						Vector2 randomDir = Random.insideUnitCircle.normalized;
						Vector3 fleeTarget = human.transform.position + new Vector3(randomDir.x, randomDir.y, 0) * 10f;
						movement.SetDestination(fleeTarget);
					}
				}
			}
		}

		// Wait a moment for the screaming animation
		yield return new WaitForSeconds(5.0f);

		// Remove all humans - both from our list and any others in the scene
		ClearSpawnedHumans();

		// Double-check that no humans remain
		HumanStateController[] remainingHumans = FindObjectsOfType<HumanStateController>();
		if (remainingHumans.Length > 0)
		{
			Debug.LogWarning($"Found {remainingHumans.Length} humans remaining after clearance. Destroying them.");
			foreach (HumanStateController human in remainingHumans)
			{
				if (human != null && human.gameObject != null)
				{
					Destroy(human.gameObject);
				}
			}
		}
	}

	// Method to restart game loop after a delay
	private IEnumerator RestartGameLoopAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);

		// Restart the game loop
		if (gameLoopCoroutine != null)
		{
			StopCoroutine(gameLoopCoroutine);
		}
		gameLoopCoroutine = StartCoroutine(GameLoop());
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
		List<RollerCoasterSeat> availableSeats = new List<RollerCoasterSeat>();

		// Get all available seats except those in the last cart (reserved for zombie)
		for (int i = 0; i < coasterCarts.Count - 1; i++)
		{
			RollerCoasterCart cart = coasterCarts[i];
			for (int j = 0; j < cart.seats.Length; j++)
			{
				if (!cart.seats[j].isOccupied)
				{
					availableSeats.Add(cart.seats[j]);
				}
			}
		}

		// Assign humans to seats
		int seatsToFill = Mathf.Min(availableSeats.Count, spawnedHumans.Count);
		List<GameObject> unboardedHumans = new List<GameObject>(); // Track humans who won't be boarding

		// First pass: identify which humans will board and which won't
		for (int i = 0; i < spawnedHumans.Count; i++)
		{
			if (i >= seatsToFill || i >= availableSeats.Count)
			{
				// This human won't be boarding
				if (spawnedHumans[i] != null)
				{
					unboardedHumans.Add(spawnedHumans[i]);
				}
			}
		}

		// Destroy unboarded humans right away
		foreach (GameObject human in unboardedHumans)
		{
			Debug.Log($"Removing unboarded human {human.name} as there are not enough seats");
			spawnedHumans.Remove(human);
			Destroy(human);
		}

		// Now actually board the humans that have seats
		for (int i = 0; i < seatsToFill; i++)
		{
			if (i >= spawnedHumans.Count)
			{
				Debug.LogWarning($"Not enough humans to fill seats. Seats: {seatsToFill}, Humans: {spawnedHumans.Count}");
				break;
			}

			GameObject human = spawnedHumans[i];
			RollerCoasterSeat seat = availableSeats[i];

			if (human == null)
			{
				Debug.LogError($"Human at index {i} is null");
				continue;
			}

			if (seat == null)
			{
				Debug.LogError($"Seat at index {i} is null");
				continue;
			}

			// Check if zombie is still hidden before assigning each human
			if (zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
			{
				Debug.LogWarning("Zombie became unhidden during boarding! Stopping boarding process.");
				yield return StartCoroutine(MakeHumansRunAway());
				yield break;
			}

			// Get the HumanSeatOccupant component
			HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
			if (occupant != null)
			{
				occupant.AssignSeat(seat); // This will trigger movement to the seat
			}

			// Get the movement controller and instruct the human to move to their seat
			HumanMovementController movement = human.GetComponent<HumanMovementController>();
			if (movement != null)
			{
				movement.SetDestination(seat.transform.position);
			}
			else
			{
				// If there's no movement controller, just teleport them
				human.transform.position = seat.transform.position;
			}
		}

		// Allow time for all humans to reach their seats
		yield return new WaitForSeconds(boardingDuration);

		// Double-check zombie is still hidden
		if (zombieHidingSystem != null && !zombieHidingSystem.IsHidden)
		{
			Debug.LogWarning("Zombie became unhidden just as humans were about to be seated!");
			yield return StartCoroutine(MakeHumansRunAway());
			yield break;
		}

		// Ensure all humans are seated properly and register with their seats
		for (int i = 0; i < seatsToFill && i < spawnedHumans.Count; i++)
		{
			GameObject human = spawnedHumans[i];
			RollerCoasterSeat seat = availableSeats[i];

			if (human == null || seat == null)
			{
				continue;
			}

			// Make sure they're at the seat position
			human.transform.position = seat.transform.position;

			// Make sure HumanStateController exists
			HumanStateController stateController = human.GetComponent<HumanStateController>();
			if (stateController != null)
			{
				// Register human with seat
				seat.occupyingHuman = stateController;
			}
		}
	}

	private void StartRide()
	{
		Debug.Log("Phase 3: Starting the ride");

		// Start the roller coaster moving
		railScroller.scrollSpeed = rideSpeed;

		// Reset ride completion flags
		zombieInFrontCart = false;
		allHumansDead = false;
	}

	private IEnumerator CompleteRide()
	{
		Debug.Log("Ride complete - all humans are dead");

		// Stop the coaster
		railScroller.scrollSpeed = 0;

		// Wait a moment to acknowledge completion
		yield return new WaitForSeconds(2f);

		// Move to restarting phase
		currentState = GameState.RideRestarting;
	}

	private void CheckZombiePosition()
	{
		// This method is kept for backward compatibility but no longer needed for game completion
		if (zombie == null || coasterCarts.Count == 0)
			return;

		// Get the cart that the zombie is currently in
		RollerCoasterCart zombieCart = null;

		// Use reflection to access the currentCart field in ZombieController
		var cartField = typeof(ZombieController)
			.GetField("currentCart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (cartField != null)
		{
			zombieCart = cartField.GetValue(zombie) as RollerCoasterCart;
		}

		// Check if the zombie cart is the front cart
		if (zombieCart == coasterCarts[0])
		{
			zombieInFrontCart = true;
			Debug.Log("Zombie has reached the front cart!");
		}
	}

	private void CheckHumansStatus()
	{
		// Find all humans in the scene
		HumanStateController[] humans = FindObjectsOfType<HumanStateController>();

		// If no humans left alive, set allHumansDead to true
		if (humans.Length == 0)
		{
			allHumansDead = true;
			Debug.Log("All humans are dead! Completing the ride...");
			return;
		}

		// Check if any humans are still alive (not in Dead state)
		allHumansDead = true;
		foreach (HumanStateController human in humans)
		{
			if (!human.IsDead())
			{
				allHumansDead = false;
				break;
			}
		}

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
		// Log how many humans we're cleaning up
		Debug.Log($"Clearing {spawnedHumans.Count} spawned humans and any strays in the scene");

		// First destroy any humans in our tracked list
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				Destroy(human);
			}
		}

		// Clear the list
		spawnedHumans.Clear();

		// Now find and destroy ANY remaining humans in the scene that might not be in our list
		foreach (HumanStateController human in FindObjectsOfType<HumanStateController>())
		{
			if (human != null && human.gameObject != null)
			{
				Debug.Log($"Found stray human: {human.gameObject.name}. Destroying it.");
				Destroy(human.gameObject);
			}
		}

		// Also clear any seats that might still be marked as occupied
		foreach (RollerCoasterCart cart in coasterCarts)
		{
			if (cart != null)
			{
				foreach (RollerCoasterSeat seat in cart.seats)
				{
					if (seat != null)
					{
						// Always clear the occupying human reference to avoid ghost references
						if (seat.occupyingHuman != null)
						{
							Debug.Log($"Clearing seat reference to human in cart {cart.name}");
							seat.occupyingHuman = null;
						}
					}
				}
			}
		}
	}

	// For visualization in the editor
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
			}
			else if (currentState == GameState.WaitingForZombieToHide)
			{
				stateInfo += " (Player needs to hide zombie)";
			}

			stateInfo += $" - Rail Speed: {railScroller.scrollSpeed:F1}";

			UnityEditor.Handles.Label(
				Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, 10f)),
				stateInfo
			);
		}
	}
}