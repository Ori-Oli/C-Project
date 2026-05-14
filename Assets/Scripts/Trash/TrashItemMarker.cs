using UnityEngine;

public class TrashItemMarker : MonoBehaviour
{
	public enum SpawnSide
	{
		Unknown = 0,
		Left = 1,
		Right = 2
	}

	public SpawnSide spawnSide = SpawnSide.Unknown;
	public TrashCanFillSensor ownerSensor;
}