using UnityEngine;

/// <summary>
/// Handles animation events for the zombie character.
/// This script should be attached to the same GameObject as the Animator component.
/// </summary>
public class ZombieAnimationHandler : MonoBehaviour
{
	// Reference to the ZombieController that manages targeting
	private ZombieController zombieController;

	void Awake()
	{
		// Get the reference to the ZombieController on this GameObject
		zombieController = GetComponentInParent<ZombieController>();

		if (zombieController == null)
		{
			Debug.LogError("ZombieAnimationHandler requires a ZombieController component on the same GameObject.");
		}
	}

	/// <summary>
	/// Called via Animation Event at the end of the eating animation.
	/// </summary>
	public void OnEatingAnimationEnd()
	{
		// Forward the animation event to the ZombieController
		if (zombieController != null)
		{
			zombieController.HandleEatingAnimationEnd();
		}
	}
}