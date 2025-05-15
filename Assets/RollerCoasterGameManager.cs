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

	// Private variables
	private List<GameObject> spawnedHumans = new List<GameObject>();
	private bool zombieInFrontCart = false;
	private bool allHumansDead = false;
	private float originalScrollSpeed;

	// Game states
	public enum GameState
	{
		HumansGathering,
		HumansBoardingTrain,
		RideInProgress,
		RideComplete,
		RideRestarting
	}

	// Public accessor for current state
	public GameState CurrentState => currentState;

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
		StartCoroutine(GameLoop());
	}

	private void Update()
	{
		if (currentState == GameState.RideInProgress)
		{
			// Check if zombie is in the front cart
			CheckZombiePosition();

			// Check if all humans are dead
			CheckHumansStatus();

			// If both conditions are met, the ride is complete
			if (zombieInFrontCart && allHumansDead)
			{
				currentState = GameState.RideComplete;
				StartCoroutine(CompleteRide());
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

			// PHASE 2: Humans boarding coaster
			currentState = GameState.HumansBoardingTrain;
			yield return StartCoroutine(BoardHumansOnCoaster());

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

	private IEnumerator SpawnAndGatherHumans()
	{
		Debug.Log("Phase 1: Spawning and gathering humans");

		// Clear any remaining humans from previous rounds
		ClearSpawnedHumans();

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
	}

	private IEnumerator BoardHumansOnCoaster()
	{
		Debug.Log("Phase 2: Boarding humans on coaster");

		// Short delay before boarding
		yield return new WaitForSeconds(boardingDelay);

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

		for (int i = 0; i < seatsToFill; i++)
		{
			GameObject human = spawnedHumans[i];
			RollerCoasterSeat seat = availableSeats[i];

			// Get the HumanSeatOccupant component
			// In RollerCoasterGameManager.cs
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

		// Ensure all humans are seated properly and register with their seats
		for (int i = 0; i < seatsToFill; i++)
		{
			GameObject human = spawnedHumans[i];
			RollerCoasterSeat seat = availableSeats[i];

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
		Debug.Log("Ride complete - zombie in front cart and all humans dead");

		// Stop the coaster
		railScroller.scrollSpeed = 0;

		// Wait a moment to acknowledge completion
		yield return new WaitForSeconds(2f);

		// Move to restarting phase
		currentState = GameState.RideRestarting;
	}

	private void CheckZombiePosition()
	{
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
			Debug.Log("All humans are dead!");
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
			Debug.Log("All humans are dead!");
		}
	}

	private void SortCartsByPosition()
	{
		// Sort carts by X position (front carts have higher X)
		coasterCarts.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
	}

	private void ClearSpawnedHumans()
	{
		// Destroy any humans from previous rounds
		foreach (GameObject human in spawnedHumans)
		{
			if (human != null)
			{
				Destroy(human);
			}
		}

		// Clear the list
		spawnedHumans.Clear();

		// Also clear any seats that might still be marked as occupied
		foreach (RollerCoasterCart cart in coasterCarts)
		{
			if (cart != null)
			{
				foreach (RollerCoasterSeat seat in cart.seats)
				{
					if (seat != null && seat.occupyingHuman != null)
					{
						// Check if the occupying human is still in the scene
						bool humanFound = false;

						foreach (HumanStateController human in FindObjectsOfType<HumanStateController>())
						{
							if (human == seat.occupyingHuman)
							{
								humanFound = true;
								break;
							}
						}

						// If the human is no longer in the scene, clear the reference
						if (!humanFound)
						{
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
			UnityEditor.Handles.Label(
				Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, 10f)),
				$"Game State: {currentState}"
			);
		}
	}
}