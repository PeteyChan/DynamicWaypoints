using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPathfinder : MonoBehaviour 
{
	public NavigatorInfo info;

	public float speed = 8f;

	Vector3 goal;

	Rigidbody rb;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	void OnEnable()
	{
		Navigator.StartUpdates(info); // Navigator will continuously update info
	}

	// Update is called once per frame
	void Update () 
	{
		Navigator.StartUpdates(info);
		if (Input.GetMouseButton(0))
		{
			var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			RaycastHit info;
			if (Physics.Raycast(ray, out info))
			{
				goal = info.point;
			}
		}					
	}

	public bool traversePath = true;

	[Range(0,1)]
	public float bias;

	Vector3 direction;

	public float turnSpeed = 5f;

	void FixedUpdate()
	{
		info.currentPosition = transform.position; // set the start and goal positions
		info.goalPosition = goal;

		float force = Mathf.Min((goal - transform.position).magnitude, 2f)/2f * speed;

		if (traversePath)
		{
			direction = Vector3.Lerp(direction , info.DirectionToNextPosition, Time.fixedDeltaTime * turnSpeed); // the direction vector gives a unit one vector toward the next waypoint
		}
		else direction = Vector3.Lerp(direction , Vector3.zero, Time.fixedDeltaTime * turnSpeed);

		rb.velocity = direction * force * speed;	
	}

	void OnDisable()
	{
		Navigator.StopUpdates(info);	// Stops updating info object. Important to stop otherwise it will keep updating in the background
	}


	void OnDrawGizmos()
	{
		Gizmos.color = Color.white;
		if (info.Path.Count <= 1) return;

		for (int i = 0; i < info.Path.Count - 1; ++ i)
		{
			Gizmos.DrawLine(info.Path[i], info.Path[i+1]);
		}
	}
}
