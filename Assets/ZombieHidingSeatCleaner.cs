using UnityEngine;

public class ZombieHidingSeatCleaner : MonoBehaviour
{
    // Reference to all seats in the game
    private RollerCoasterSeat[] allSeats;
    
    // Reference to the zombie hiding system
    private ZombieHidingSystem zombieHidingSystem;
    
    // Previous hiding state to detect changes
    private bool wasHiddenOrHiding = false;
    
    private void Awake()
    {
        // Find and store all seats in the scene
        allSeats = Object.FindObjectsByType<RollerCoasterSeat>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        
        // Get the zombie hiding system reference
        zombieHidingSystem = FindFirstObjectByType<ZombieController>()?.GetComponent<ZombieHidingSystem>();
        
        if (zombieHidingSystem == null)
        {
            Debug.LogError("ZombieHidingSeatCleaner: No ZombieHidingSystem found in scene!");
        }
    }
    
    private void OnEnable()
    {
        // Subscribe to zombie hiding events if possible
        if (zombieHidingSystem != null)
        {
            // Initialize previous state
            wasHiddenOrHiding = zombieHidingSystem.IsHidden || zombieHidingSystem.IsHiding;
            
            // Clear seats at start if zombie is already hidden
            if (wasHiddenOrHiding)
            {
                ClearAllZombieSeats();
            }
        }
    }
    
    private void Update()
    {
        if (zombieHidingSystem == null) return;
        
        // Current state
        bool isHiddenOrHiding = zombieHidingSystem.IsHidden || zombieHidingSystem.IsHiding;
        
        // Clear zombie occupation status when zombie starts hiding
        if (isHiddenOrHiding && !wasHiddenOrHiding)
        {
            Debug.Log("Zombie started hiding - clearing all seat occupation flags");
            ClearAllZombieSeats();
        }
        
        // Update previous state
        wasHiddenOrHiding = isHiddenOrHiding;
    }
    
    // Method to clear all zombie seat occupation flags
    public void ClearAllZombieSeats()
    {
        if (allSeats == null) return;
        
        foreach (RollerCoasterSeat seat in allSeats)
        {
            if (seat != null)
            {
                seat.SetZombieOccupation(false);
                Debug.Log($"Cleared zombie occupation flag for seat: {seat.name}");
            }
        }
    }
}