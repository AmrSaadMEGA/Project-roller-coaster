using UnityEngine;
using System.Collections.Generic;

public class RollerCoasterCart : MonoBehaviour
{
	[Tooltip("References to all seats in this cart")]
	public RollerCoasterSeat[] seats;

	/// <summary>
	/// Checks if there's an adjacent seat to the provided one
	/// </summary>
	/// <param name="currentSeatIndex">Index of the current seat</param>
	/// <param name="adjacentSeatIndex">Out parameter that will contain the adjacent seat index if found</param>
	/// <returns>True if an adjacent empty seat is found, false otherwise</returns>
	public bool TryGetAdjacentSeat(int currentSeatIndex, out int adjacentSeatIndex)
	{
		adjacentSeatIndex = -1;

		// If current seat index is invalid
		if (currentSeatIndex < 0 || currentSeatIndex >= seats.Length)
			return false;

		// In a two-seat setup, the adjacent seat would be 0 if current is 1, and 1 if current is 0
		adjacentSeatIndex = (currentSeatIndex == 0) ? 1 : 0;

		// Make sure the adjacent seat exists
		if (adjacentSeatIndex >= 0 && adjacentSeatIndex < seats.Length)
			return true;

		return false;
	}

	/// <summary>
	/// Gets the index of the seat within this cart
	/// </summary>
	public int GetSeatIndex(RollerCoasterSeat seat)
	{
		for (int i = 0; i < seats.Length; i++)
		{
			if (seats[i] == seat)
				return i;
		}
		return -1; // Seat not found in this cart
	}
}