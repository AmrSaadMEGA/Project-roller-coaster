using UnityEngine;
using System.Collections;
using static RollerCoasterGameManager;
using System.Linq;

[RequireComponent(typeof(ZombieController))]
public class ZombieHidingSystem : MonoBehaviour
{
	[Header("Hiding")]
	[SerializeField] private Transform hideSpot;
	[SerializeField] private float hideMoveDuration = 1.5f;
	[SerializeField] private string hideAnimationState = "Hiding";
	[SerializeField] private float hideSpotClickRadius = 1.5f;

	[Header("Unhiding")]
	[SerializeField] private float unhideDelay = 0.5f;
	[SerializeField] private string unhideAnimationState = "Unhiding";

	[Header("Human Respawning")]
	[SerializeField] private float respawnDelay = 3.0f; // New parameter for respawn delay

	[Header("References")]
	[SerializeField] private RollerCoasterGameManager gameManager;
	[SerializeField] private InfiniteRailScroller railScroller; // Added reference to the rail scroller

	// Private variables
	private ZombieController zombieController;
	private bool isHiding = false;
	private bool isHidden = false;
	private bool autoHideDone = false;
	private Transform originalParent;
	private Vector3 originalLocalPosition;
	private bool humansScared = false; // New flag to track if humans were scared

	// Public access
	public bool IsHidden => isHidden;
	public bool IsHiding => isHiding;

	private void Awake()
	{
		zombieController = GetComponent<ZombieController>();

		if (hideSpot == null)
		{
			Debug.LogError("ZombieHidingSystem requires a hideSpot Transform reference!");
		}

		if (gameManager == null)
		{
			gameManager = FindObjectOfType<RollerCoasterGameManager>();
			if (gameManager == null)
			{
				Debug.LogError("ZombieHidingSystem requires a RollerCoasterGameManager reference!");
			}
		}

		// Find rail scroller if not assigned
		if (railScroller == null)
		{
			railScroller = FindObjectOfType<InfiniteRailScroller>();
			if (railScroller == null)
			{
				Debug.LogError("ZombieHidingSystem requires an InfiniteRailScroller reference!");
			}
		}

		// Store original parent and position
		originalParent = transform.parent;
		originalLocalPosition = transform.localPosition;
	}

	private void Start()
	{
		// Auto-hide at the beginning of the game
		StartCoroutine(AutoHideAtStart());
	}

	private IEnumerator AutoHideAtStart()
	{
		// Hide instantly without delay
		transform.position = hideSpot.position;
		isHidden = true;
		yield break; // Remove the wait
	}

	// Add a condition to Update to prevent unwanted game restarting
	private void Update()
	{
		// Modified condition to allow hiding during boarding phase
		if (!isHiding && !IsRailMoving() &&
		   gameManager.CurrentState != RollerCoasterGameManager.GameState.RideInProgress)
		{
			if (Input.GetMouseButtonDown(0))
			{
				Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				if (Vector2.Distance(mousePosition, hideSpot.position) <= hideSpotClickRadius)
				{
					if (!isHidden)
					{
						HideZombie();
					}
				}
			}
		}
		// Prevent panic after successful boarding
		if (gameManager.CurrentState == RollerCoasterGameManager.GameState.ZombieBoarding &&
			!isHidden && !humansScared)
		{
			humansScared = true; // Block automatic scare system
		}

		// Check game state to determine when to unhide
		// Modified condition with additional seating check
		if (isHidden && !isHiding && gameManager != null &&
			   gameManager.CurrentState == GameState.ZombieBoarding &&
			   gameManager.BoardingCompleted &&
			   AllHumansProperlySeated())
		{
			UnhideZombie();
		}

		// Modified: Check if humans are boarding and zombie isn't hidden
		// Modified condition: Only trigger panic during HumanGathering or HumansBoardingTrain
		if (!isHidden && !isHiding && gameManager != null && autoHideDone && !humansScared)
		{
			if (gameManager.CurrentState == RollerCoasterGameManager.GameState.HumansGathering ||
				gameManager.CurrentState == RollerCoasterGameManager.GameState.HumansBoardingTrain)
			{
				TriggerHumanScreaming();
				humansScared = true;
			}
		}
	}
	// NEW HELPER METHOD
	private bool AllHumansProperlySeated()
	{
		if (gameManager?.SpawnedHumans == null) return false;

		foreach (GameObject human in gameManager.SpawnedHumans)
		{
			if (human == null) continue;
			HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
			if (occupant == null || !occupant.IsSeated) return false;

			// Additional verification with seat data
			if (occupant.OccupiedSeat?.occupyingHuman != occupant.HumanController)
				return false;
		}
		return true;
	}
	// New method to check if the rail is moving
	private bool IsRailMoving()
	{
		if (railScroller == null)
			return false;

		// If the scroll speed is greater than a small threshold, consider the rail moving
		return Mathf.Abs(railScroller.scrollSpeed) > 0.01f;
	}

	public void HideZombie()
	{
		// Added check to prevent hiding when rail is moving
		if (isHiding || isHidden || IsRailMoving())
		{
			if (IsRailMoving())
			{
				Debug.Log("Cannot hide while rail is moving!");
			}
			return;
		}

		StartCoroutine(HideZombieCoroutine());
	}

	public void UnhideZombie()
	{
		// Add extra validation
		if (isHiding || !isHidden || !AllHumansProperlySeated())
			return;

		StartCoroutine(UnhideZombieCoroutine());
	}

	private IEnumerator HideZombieCoroutine()
	{
		// Double-check rail is not moving before proceeding
		if (IsRailMoving())
		{
			Debug.Log("Cannot hide while rail is moving!");
			yield break;
		}

		isHiding = true;

		// Play hide animation
		Animator animator = GetComponentInChildren<Animator>();
		if (animator != null && !string.IsNullOrEmpty(hideAnimationState))
		{
			animator.Play(hideAnimationState);
		}

		// Move to hiding spot
		Vector3 startPos = transform.position;
		Vector3 endPos = hideSpot.position;

		float elapsedTime = 0f;
		while (elapsedTime < hideMoveDuration)
		{
			transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / hideMoveDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = endPos;

		// Parent to the hiding spot (optional, depends on your game setup)
		transform.SetParent(hideSpot);

		isHiding = false;
		isHidden = true;
		humansScared = false; // Reset the flag when zombie hides

		Debug.Log("Zombie is now hidden!");
	}

	private IEnumerator UnhideZombieCoroutine()
	{
		isHiding = true;

		// Get reference to isMovingBetweenSeats field in ZombieController
		System.Reflection.FieldInfo movingField = typeof(ZombieController)
			.GetField("isMovingBetweenSeats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		// Short delay before unhiding
		yield return new WaitForSeconds(unhideDelay);

		// Play unhide animation
		Animator animator = GetComponentInChildren<Animator>();
		if (animator != null && !string.IsNullOrEmpty(unhideAnimationState))
		{
			animator.Play(unhideAnimationState);
		}

		// Restore original parent
		transform.SetParent(originalParent);

		// Get the last cart (assuming it's the one where zombie should start)
		RollerCoasterCart[] carts = FindObjectsOfType<RollerCoasterCart>();

		// Sort carts by position (front to back)
		System.Array.Sort(carts, (a, b) => b.transform.position.x.CompareTo(a.transform.position.x));

		// The last cart is the one at the back
		RollerCoasterCart backCart = carts[carts.Length - 1];

		// Find an empty seat in the back cart
		int seatIndex = -1;
		for (int i = 0; i < backCart.seats.Length; i++)
		{
			// ENHANCED OCCUPANCY CHECK
			if (!backCart.seats[i].isOccupied &&
				backCart.seats[i].occupyingHuman == null &&
				Vector3.Distance(backCart.seats[i].transform.position,
								transform.position) > 0.5f) // Safety margin
			{
				seatIndex = i;
				break;
			}
		}

		if (seatIndex == -1)
		{
			for (int i = 0; i < backCart.seats.Length; i++)
			{
				if (backCart.seats[i].occupyingHuman == null)
				{
					seatIndex = i;
					break;
				}
			}
		}


		// --- NEW CODE: Disable attacks during movement ---
		// Set isMovingBetweenSeats to true to prevent attacks
		movingField.SetValue(zombieController, true);

		// Move to the seat position
		Vector3 startPos = transform.position;
		Vector3 endPos = backCart.seats[seatIndex].transform.position;

		float elapsedTime = 0f;
		while (elapsedTime < hideMoveDuration)
		{
			transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / hideMoveDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = endPos;

		// --- NEW CODE: Re-enable attacks after movement ---
		movingField.SetValue(zombieController, false);

		// Update the ZombieController's cart reference
		System.Reflection.FieldInfo cartField = typeof(ZombieController)
			.GetField("currentCart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (cartField != null)
		{
			cartField.SetValue(zombieController, backCart);
		}

		// Update seat index
		System.Reflection.FieldInfo seatField = typeof(ZombieController)
			.GetField("currentSeatIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (seatField != null)
		{
			seatField.SetValue(zombieController, seatIndex);
		}

		isHiding = false;
		isHidden = false;

		Debug.Log($"Zombie is now unhidden and placed in the back cart at seat {seatIndex}!");
	}

	private void TriggerHumanScreaming()
	{
		// Only trigger panic during gathering/boarding phases
		if (gameManager.CurrentState != GameState.HumansGathering &&
			gameManager.CurrentState != GameState.HumansBoardingTrain)
		{
			return;
		}

		HumanStateController[] humans = FindObjectsOfType<HumanStateController>()
			.Where(h => !h.IsDead() && h.GetComponent<HumanSeatOccupant>()?.IsSeated == false)
			.ToArray();

		foreach (HumanStateController human in humans)
		{
			StartCoroutine(MakeHumanScreamAndRun(human));
		}

		StartCoroutine(RespawnHumansAfterDelay());
	}

	private IEnumerator MakeHumanScreamAndRun(HumanStateController human)
	{
		if (human == null)
			yield break;

		// Get the HumanScreamingState component (your custom class)
		HumanScreamingState screamingState = human.GetComponent<HumanScreamingState>();

		if (screamingState != null)
		{
			// Use the existing method to make the human scream and run
			screamingState.ScreamAndRunAway(transform.position);
		}
		else
		{
			// Fallback if the human doesn't have the HumanScreamingState component
			// Play screaming animation
			Animator animator = human.GetComponent<Animator>();
			if (animator != null)
			{
				animator.Play("Screaming");
			}

			// Get movement controller
			HumanMovementController movement = human.GetComponent<HumanMovementController>();

			if (movement != null)
			{
				// Run away from the zombie (pick a random direction)
				Vector3 runDirection = new Vector3(
					Random.Range(-1f, 1f),
					Random.Range(-1f, 1f),
					0
				).normalized;

				// Run a shorter distance away (slower movement)
				Vector3 runTarget = human.transform.position + runDirection * 0.1f;
				movement.SetDestination(runTarget);
			}
		}

		// Wait longer before removing human (more noticeable)
		yield return new WaitForSeconds(5.0f);

		// Destroy the human
		if (human != null)
		{
			Destroy(human.gameObject);
		}
	}

	// New method to handle respawning humans after all are scared away
	private IEnumerator RespawnHumansAfterDelay()
	{
		yield return new WaitForSeconds(5.0f + respawnDelay);

		// Only respawn if in gathering/boarding states
		if (!isHidden &&
			(gameManager.CurrentState == GameState.HumansGathering ||
			 gameManager.CurrentState == GameState.HumansBoardingTrain))
		{
			// Clear ONLY non-seated humans
			foreach (GameObject human in gameManager.SpawnedHumans.ToList())
			{
				HumanSeatOccupant occupant = human?.GetComponent<HumanSeatOccupant>();
				if (occupant == null || !occupant.IsSeated)
				{
					gameManager.SpawnedHumans.Remove(human);
					Destroy(human);
				}
			}

			// Restart spawning without resetting the entire game state
			StartCoroutine(CallSpawnAndGatherHumans());
		}
	}

	// Helper method to call the private SpawnAndGatherHumans method in the game manager
	private IEnumerator CallSpawnAndGatherHumans()
	{
		// Short delay before spawning new humans
		yield return new WaitForSeconds(0.5f);

		// Use reflection to call SpawnAndGatherHumans
		System.Reflection.MethodInfo spawnMethod = typeof(RollerCoasterGameManager)
			.GetMethod("SpawnAndGatherHumans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (spawnMethod != null)
		{
			// Start the coroutine in the game manager
			gameManager.StartCoroutine((IEnumerator)spawnMethod.Invoke(gameManager, null));
			Debug.Log("Respawning humans after they were scared away!");
		}
	}

	private void OnDrawGizmos()
	{
		// Draw hide spot
		if (hideSpot != null)
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hideSpot.position, hideSpotClickRadius);

			// Label
			UnityEditor.Handles.color = Color.white;
			UnityEditor.Handles.Label(hideSpot.position + Vector3.up * 1f, "Zombie Hide Spot");
		}

		// Show current state when playing
		if (Application.isPlaying)
		{
			string stateText = isHidden ? "Hidden" : (isHiding ? "Hiding/Unhiding" : "Visible");

			// Add rail movement state to the display
			if (railScroller != null && Mathf.Abs(railScroller.scrollSpeed) > 0.01f)
			{
				stateText += " (Rail Moving)";
			}

			// Add human scared state
			if (humansScared)
			{
				stateText += " (Humans Scared)";
			}

			UnityEditor.Handles.color = Color.yellow;
			UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateText);
		}
	}
}