using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSpawner : MonoBehaviour 
{
	[SerializeField]
	GameObject Pathfinder;

	public static int PathfinderCount = 1;

	Stack<GameObject> Pathfinders = new Stack<GameObject>();

	// Update is called once per frame
	void Update () 
	{
		if (Input.GetKey(KeyCode.Alpha1))
		{
			if (PathfinderCount >= 1000)
				return;
			Pathfinders.Push(Instantiate(Pathfinder, transform.position, transform.rotation));
			PathfinderCount ++;
		}
		if (Input.GetKey(KeyCode.Alpha2))
		{
			if (Pathfinders.Count > 0)
			{
				Destroy(Pathfinders.Pop());
				PathfinderCount --;
			}
		}
	}
}
