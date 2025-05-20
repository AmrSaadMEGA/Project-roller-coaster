using UnityEngine;
using System.Collections;
using static RollerCoasterGameManager;
using System.Linq;

[RequireComponent(typeof(ZombieController))]
public class ZombieHidingSystem : MonoBehaviour
{
	[Header("Hiding")]
	[SerializeField] private Transform hideSpot; // Keeping this for compatibility
	[SerializeField] private bool useScreenPositionForHiding = true; // Toggle between methods
	[Tooltip("Position where zombie will hide in screen space (0,0 is bottom-left, 1,1 is top-right)")]
	[SerializeField] private Vector2 screenHidingPosition = new Vector2(0.1f, 0.2f); // Default to bottom-left area
	[SerializeField] private float screenHidingDepth = 10f; // Z-distance from camera when hiding
	[SerializeField] private float hideMoveDuration = 1.5f;
	[SerializeField] private string hideAnimationState = "Hiding";
	[SerializeField] private float hideSpotClickRadius = 1.5f;
	[SerializeField] private bool showScreenHidingGizmo = true; // Toggle visibility of the screen position gizmo

	[Header("Unhiding")]
	[SerializeField] private float unhideDelay = 0.5f;
	[SerializeField] private string unhideAnimationState = "Unhiding";

	[Header("Human Respawning")]
	[SerializeField] private float respawnDelay = 3.0f; // Parameter for respawn delay

	[Header("References")]
	[SerializeField] private RollerCoasterGameManager gameManager;
	[SerializeField] private InfiniteRailScroller railScroller; // Reference to the rail scroller

	// Private variables
	private ZombieController zombieController;
	private bool isHiding = false;
	private bool isHidden = false;
	private bool autoHideDone = false;
	private Transform originalParent;
	private Vector3 originalLocalPosition;
	private bool humansScared = false; // Flag to track if humans were scared
	private bool humansClearing = false; // Flag to track if humans are currently clearing/despawning
	private Camera mainCamera;
	private Vector3 hidingPosition; // Cached world position from screen coordinates

	// Public access
	public bool IsHidden => isHidden;
	public bool IsHiding => isHiding;
	public bool HumansClearing => humansClearing; // Property to check if humans are clearing
	public string UnhideAnimationState => unhideAnimationState;

	public void SetHideState(bool value)
	{
		isHidden = value;
		isHiding = value;
	}

	private void Awake()
	{
		zombieController = GetComponent<ZombieController>();
		mainCamera = Camera.main;

		if (!useScreenPositionForHiding && hideSpot == null)
		{
			Debug.LogError("ZombieHidingSystem requires a hideSpot Transform reference when not using screen position!");
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

		// Calculate and cache the hiding position if using screen coordinates
		if (useScreenPositionForHiding && mainCamera != null)
		{
			UpdateHidingPosition();
		}
	}

	// New method to update hiding position from screen coordinates
	private void UpdateHidingPosition()
	{
		if (mainCamera == null) return;

		// Convert screen position (0-1 range) to world position
		Vector3 viewportPosition = new Vector3(
			screenHidingPosition.x,
			screenHidingPosition.y,
			screenHidingDepth
		);

		hidingPosition = mainCamera.ViewportToWorldPoint(viewportPosition);
	}

	private void Start()
	{
		// Auto-hide at the beginning of the game
		StartCoroutine(AutoHideAtStart());
	}

	private IEnumerator AutoHideAtStart()
	{
		// Get appropriate hiding position
		Vector3 hidePos = useScreenPositionForHiding ? hidingPosition : hideSpot.position;

		// Hide instantly without delay
		transform.position = hidePos;
		isHidden = true;
		yield break;
	}

	// Update is called once per frame
	private void Update()
	{
		// Update the hiding position every frame if camera or screen changes
		if (useScreenPositionForHiding && mainCamera != null)
		{
			UpdateHidingPosition();
		}

		// Modified condition to allow hiding during boarding phase
		if (!isHiding && !IsRailMoving() &&
		   gameManager.CurrentState != RollerCoasterGameManager.GameState.RideInProgress)
		{
			if (Input.GetMouseButtonDown(0))
			{
				Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

				// Check if clicking on hide spot or near screen hiding position based on setting
				bool clickedHidePosition = useScreenPositionForHiding ?
					Vector2.Distance(mousePosition, hidingPosition) <= hideSpotClickRadius :
					Vector2.Distance(mousePosition, hideSpot.position) <= hideSpotClickRadius;

				if (clickedHidePosition)
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
		// Only unhide when humans are properly seated and not in the process of clearing
		if (isHidden && !isHiding && gameManager != null &&
			gameManager.CurrentState == GameState.ZombieBoarding &&  // Only unhide during ZombieBoarding state
			gameManager.BoardingCompleted &&                        // Ensure boarding has been marked as completed
			gameManager.CurrentState != GameState.RideComplete &&   // Not during ride completion
			AllHumansProperlySeated() &&                           // Additional verification
			!humansClearing &&                                      // Don't unhide if humans are clearing
			!AreAnyHumansPanicking())                              // Don't unhide if any humans are panicking
		{
			UnhideZombie();
		}

		// Check if humans are boarding and zombie isn't hidden
		// Modified condition with additional boarding check
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

	// Method to check if any humans are panicking
	private bool AreAnyHumansPanicking()
	{
		if (gameManager?.SpawnedHumans == null) return false;

		foreach (GameObject human in gameManager.SpawnedHumans)
		{
			if (human == null) continue;

			// Check for screaming state
			HumanScreamingState screamState = human.GetComponent<HumanScreamingState>();
			if (screamState != null && screamState.IsScreaming)
				return true;

			// Alternative check for movement to exit
			HumanMovementController movement = human.GetComponent<HumanMovementController>();
			if (movement != null && movement.IsMoving() &&
				movement.GetDestination() != Vector3.zero &&
				human.GetComponent<HumanSeatOccupant>()?.AssignedSeat != null)
			{
				// If human is moving away from their assigned seat, they're likely panicking
				RollerCoasterSeat assignedSeat = human.GetComponent<HumanSeatOccupant>()?.AssignedSeat;
				if (assignedSeat != null)
				{
					Vector3 movementDirection = (movement.GetDestination() - human.transform.position).normalized;
					Vector3 seatDirection = (assignedSeat.transform.position - human.transform.position).normalized;

					// If moving away from seat (dot product negative), they're probably panicking
					if (Vector3.Dot(movementDirection, seatDirection) < -0.5f)
						return true;
				}
			}
		}

		return false;
	}

	// Helper method
	private bool AllHumansProperlySeated()
	{
		if (gameManager?.SpawnedHumans == null) return false;

		foreach (GameObject human in gameManager.SpawnedHumans)
		{
			if (human == null) continue;
			HumanSeatOccupant occupant = human.GetComponent<HumanSeatOccupant>();
			if (occupant == null || !occupant.IsSeated) return false;

			// Additional verification with seat data
			if (occupant.OccupiedSeat?.occupyingHuman == null)
				return false;
		}
		return true;
	}

	// Method to check if the rail is moving
	private bool IsRailMoving()
	{
		if (railScroller == null)
			return false;

		// If the scroll speed is greater than a small threshold, consider the rail moving
		return Mathf.Abs(railScroller.scrollSpeed) > 0.01f;
	}

	public void HideZombie()
	{
		// Added check to prevent hiding when rail is moving or hiding spot is moving
		if (isHiding || isHidden || IsRailMoving() || IsHideSpotMoving())
		{
			if (IsRailMoving())
			{
				Debug.Log("Cannot hide while rail is moving!");
			}
			else if (IsHideSpotMoving())
			{
				Debug.Log("Cannot hide while hiding spot is moving!");
			}
			return;
		}

		GetComponent<ZombieController>().PrepareToHide();
		StartCoroutine(HideZombieCoroutine());
	}

	private bool IsHideSpotMoving()
	{
		// If using screen position, we don't need to check for movement
		if (useScreenPositionForHiding)
			return false;

		// Try to find the HideSpotMovementSystem
		HideSpotMovementSystem movementSystem = hideSpot.GetComponentInParent<HideSpotMovementSystem>();
		if (movementSystem == null)
		{
			// Try to find it in the scene
			movementSystem = FindObjectOfType<HideSpotMovementSystem>();
		}

		if (movementSystem != null)
		{
			// Access the isMoving field via reflection
			System.Reflection.FieldInfo isMovingField = typeof(HideSpotMovementSystem)
				.GetField("isMoving", System.Reflection.BindingFlags.NonPublic |
						  System.Reflection.BindingFlags.Instance);

			if (isMovingField != null)
			{
				return (bool)isMovingField.GetValue(movementSystem);
			}
		}

		return false;
	}

	public void UnhideZombie()
	{
		// Added checks to prevent unhiding when humans are clearing
		if (isHiding ||
			!isHidden ||
			gameManager.CurrentState != GameState.ZombieBoarding ||  // Only in ZombieBoarding state
			!gameManager.BoardingCompleted ||                        // Only if boarding completed
			gameManager.CurrentState == GameState.RideComplete ||
			gameManager.CurrentState == GameState.RideInProgress ||
			gameManager.SpawnedHumans.Count == 0 ||                  // No humans available
			humansClearing ||                                        // Don't unhide if humans are clearing
			AreAnyHumansPanicking())                                // Don't unhide if any humans are panicking
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

		// Determine target position based on settings
		Vector3 targetPosition = useScreenPositionForHiding ? hidingPosition : hideSpot.position;

		// Move to hiding spot
		Vector3 startPos = transform.position;
		float elapsedTime = 0f;
		while (elapsedTime < hideMoveDuration)
		{
			// If using hideSpot, get its current position each frame
			if (!useScreenPositionForHiding)
			{
				targetPosition = hideSpot.position;
			}
			// If using screen position, update it each frame in case camera moves
			else
			{
				UpdateHidingPosition();
				targetPosition = hidingPosition;
			}

			transform.position = Vector3.Lerp(startPos, targetPosition, elapsedTime / hideMoveDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = targetPosition;

		// If using hideSpot, parent to the hiding spot
		if (!useScreenPositionForHiding && hideSpot != null)
		{
			transform.SetParent(hideSpot);
		}

		isHiding = false;
		isHidden = true;
		humansScared = false; // Reset the flag when zombie hides
		humansClearing = false; // Reset the humans clearing flag when zombie hides

		Debug.Log("Zombie is now hidden!");
	}

	private IEnumerator UnhideZombieCoroutine()
	{
		isHiding = true;
		isHidden = true; // Keep hidden during animation

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

		// Re-enable attacks after movement
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

		isHidden = false; // Only set visibility here
		isHiding = false;
		humansScared = true; // Block future scare triggers
		Debug.Log("Zombie is now fully visible!");
	}

	private void TriggerHumanScreaming()
	{
		// Only trigger panic if zombie is not hidden 
		// AND only during gathering/boarding phases
		if (isHidden ||
			(gameManager.CurrentState != GameState.HumansGathering &&
			 gameManager.CurrentState != GameState.HumansBoardingTrain))
		{
			return;
		}

		// Set humans clearing flag so zombie won't unhide during this process
		humansClearing = true;

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

		// Get the HumanScreamingState component
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

	// Handle respawning humans after all are scared away
	private IEnumerator RespawnHumansAfterDelay()
	{
		yield return new WaitForSeconds(respawnDelay);

		// Reset humans clearing flag after respawn delay
		humansClearing = false;

		// Always reset to gathering state if zombie is still visible
		if (!isHidden)
		{
			// Clear all non-seated humans
			foreach (GameObject human in gameManager.SpawnedHumans.ToList())
			{
				HumanSeatOccupant occupant = human?.GetComponent<HumanSeatOccupant>();
				if (occupant == null || !occupant.IsSeated)
				{
					gameManager.SpawnedHumans.Remove(human);
					Destroy(human);
				}
			}

			// Reset to gathering state
			gameManager.StopCoroutine(gameManager.GetType().GetField("gameLoopCoroutine",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.GetValue(gameManager) as Coroutine);

			// Set state to gathering
			System.Reflection.FieldInfo stateField = gameManager.GetType().GetField("currentState",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			stateField.SetValue(gameManager, GameState.HumansGathering);

			// Restart the game loop
			gameManager.StartCoroutine("GameLoop");
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
		// Draw hide spot based on settings
		if (useScreenPositionForHiding && showScreenHidingGizmo)
		{
			// Only show in editor if we have a main camera
			if (Camera.main != null)
			{
				// Convert screen position to world coordinates for visualization
				Vector3 viewportPos = new Vector3(screenHidingPosition.x, screenHidingPosition.y, screenHidingDepth);
				Vector3 worldPos = Camera.main.ViewportToWorldPoint(viewportPos);

				Gizmos.color = new Color(1f, 0f, 1f, 0.7f); // Bright magenta
				Gizmos.DrawWireSphere(worldPos, hideSpotClickRadius);

				// Draw coordinate axis at screen position
				float axisLength = 0.5f;
				Gizmos.color = Color.red;
				Gizmos.DrawLine(worldPos, worldPos + Vector3.right * axisLength);
				Gizmos.color = Color.green;
				Gizmos.DrawLine(worldPos, worldPos + Vector3.up * axisLength);

				// Label
				UnityEditor.Handles.color = Color.white;
				UnityEditor.Handles.Label(worldPos + Vector3.up * 1f, "Screen Hiding Position");
			}
		}
		else if (hideSpot != null && !useScreenPositionForHiding)
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

			// Add humans clearing state
			if (humansClearing)
			{
				stateText += " (Humans Clearing)";
			}

			// Add hiding method info
			stateText += useScreenPositionForHiding ? " [Screen Pos]" : " [Transform]";

			UnityEditor.Handles.color = Color.yellow;
			UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateText);
		}
	}
}