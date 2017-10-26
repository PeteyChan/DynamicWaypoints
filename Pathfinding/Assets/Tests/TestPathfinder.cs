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

	public bool follow;

	[Range(0,1)]
	public float bias;

	Vector3 direction;

	public float turnSpeed = 5f;

	void FixedUpdate()
	{
		info.currentPosition = transform.position;
		info.goalPosition = goal;

		float force = Mathf.Min((goal - transform.position).magnitude, 2f)/2f * speed;

		if (follow)
		{

			direction = Vector3.Lerp(direction , info.DirectionToNextPosition, Time.fixedDeltaTime * turnSpeed);
		}
		else direction = Vector3.Lerp(direction , Vector3.zero, Time.fixedDeltaTime * turnSpeed);
		rb.velocity = direction * force * speed;
	}

	void OnDisable()
	{
		Navigator.StopUpdates(info);
	}

	public Transform debugGoal;
	public float radius = 5f;

	void OnDrawGizmos()
	{
//		Collider[] cols = Physics.OverlapSphere(transform.position, radius, Navigator.Instance.WaypointLayer);
//
//		float shortdist = Mathf.Infinity;
//		Collider shortCol = null;
//
//		foreach(var col in cols)
//		{
//			Gizmos.color = Color.white;
//			Gizmos.DrawLine(transform.position, col.transform.position);
//			var distToPathfinder = (col.transform.position - transform.position).magnitude;
//			var distToGoal = (debugGoal.position - col.transform.position).magnitude;
//
//			var dist = bias*distToPathfinder + (1-bias)*distToGoal;
//			if (dist < shortdist)
//			{
//				shortdist = dist;
//				shortCol = col;
//			}
//		}
//		Gizmos.color = Color.blue;
//		if (shortCol)
//			Gizmos.DrawLine(debugGoal.position, shortCol.transform.position);
//
//		Gizmos.color = Color.yellow;
//		Gizmos.DrawWireSphere(transform.position, radius);



		Gizmos.color = Color.white;
		if (info.Path.Count <= 1) return;

		for (int i = 0; i < info.Path.Count - 1; ++ i)
		{
			Gizmos.DrawLine(info.Path[i], info.Path[i+1]);
		}
	}
}
