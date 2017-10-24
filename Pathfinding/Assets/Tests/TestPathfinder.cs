using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPathfinder : MonoBehaviour 
{
	public NavigatorInfo info;

	public float speed = 1f;
	public float turningSpeed = 1f;

	Vector3 goal;

	Rigidbody rb;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	void OnEnable()
	{
		Navigator.StartUpdates(info);
	}

	// Update is called once per frame
	void Update () 
	{
		if (Input.GetMouseButton(0))
		{
			var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			RaycastHit info;
			if (Physics.Raycast(ray, out info))
			{
				goal = info.point;
			}
		}

		if (Input.GetKeyDown(KeyCode.Alpha1))
			Navigator.StartUpdates(info);
		if (Input.GetKeyDown(KeyCode.Alpha2))
			Navigator.StopUpdates(info);						
	}

	void FixedUpdate()
	{
		info.startPosition = transform.position;
		info.endPosition = goal;

		foreach (var item in info.Path)
		{
			var direction = item - info.startPosition;
			if (direction.magnitude < info.minDistanceToWaypoint)
				continue;

			float force = Mathf.Clamp01(direction.magnitude);

			rb.velocity = direction.normalized*force* speed;
			break;
		}

		//rb.velocity = transform.forward* Input.GetAxis("Vertical")* speed;

		if (!info.AtDestination)
		{
			//rb.velocity = info.DirectionToNextWaypoint * speed;
		}
	}

	void OnDisable()
	{
		Navigator.StopUpdates(info);
	}

	void OnDrawGizmos()
	{
		if (info.Path.Count <= 1) return;

		for (int i = 0; i < info.Path.Count - 1; ++ i)
		{
			Gizmos.DrawLine(info.Path[i], info.Path[i+1]);
		}
	}
}
