using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicNavigationCollider : MonoBehaviour 
{
	static List<DynamicNavigationCollider> navigatorColliders = new List<DynamicNavigationCollider>();
	public bool showInfluence;
	public float InfluenceRange = 5f;
	public Vector3 InfluenceDimensions = new Vector3(8,5,5);

	Vector3 position;

	enum InfluenceType
	{
		Sphere, Box
	}

	[SerializeField]
	InfluenceType BoundsType = InfluenceType.Sphere;

	Collider col;

	void Awake()
	{
		col = GetComponent<Collider>();
		if (!col)
			gameObject.SetActive(false);
	}

	void OnEnable()
	{
		position = transform.position;
		navigatorColliders.Add(this);
		ForceUpdate();
	}

	void OnDisable()
	{
		navigatorColliders.Remove(this);
	}

	public List<Waypoint> InsideWaypoints = new List<Waypoint>();

	bool forceUpdate;

	[ContextMenu("ForceUpdate")]
	public void ForceUpdate()
	{
		forceUpdate = true;
	}

	void UpdateCollider()
	{
		if (position != transform.position || forceUpdate)
		{
			position = transform.position;
			int count = 0;

			var colliders = Navigator.Instance.colliders;

			switch (BoundsType)
			{
			case InfluenceType.Sphere:
				count = Physics.OverlapSphereNonAlloc(position, InfluenceRange, colliders, Navigator.Instance.WaypointLayer);
				break;
			case InfluenceType.Box:
				count = Physics.OverlapBoxNonAlloc(position, InfluenceDimensions/2f, colliders, transform.rotation, Navigator.Instance.WaypointLayer);
				break;
			default:
				break;					
			}

			for (int i = 0; i < count; ++ i)
			{
				var waypoint = colliders[i].GetComponent<Waypoint>();
				if (waypoint && !InsideWaypoints.Contains(waypoint))
				{
					InsideWaypoints.Add(waypoint);
				}
			}

			foreach(var waypoint in InsideWaypoints)
			{
				var direction = position - waypoint.position;
				RaycastHit info;
				if (Physics.Raycast(waypoint.position, direction, out info, direction.magnitude , Navigator.Instance.NavigationCollisionLayer) && info.distance > waypoint.radius)
				{
					waypoint.gameObject.SetActive(true);
					Navigator.Instance.QueueNeighbourUpdate(waypoint);
				}
				else
				{
					waypoint.gameObject.SetActive(false);
				}
			}

			InsideWaypoints.RemoveAll(  w => {return w.gameObject.activeSelf;});
		}
	}

	// Update is called once per frame
	public static void Update () 
	{
		for (int i = 0; i < navigatorColliders.Count; ++ i)
		{
			navigatorColliders[i].UpdateCollider();
		}
	}

	void OnDrawGizmos()
	{
		if (!showInfluence) return;

		Gizmos.color = Color.red;

		switch(BoundsType)
		{
		case InfluenceType.Sphere:
			Gizmos.DrawWireSphere(transform.position, InfluenceRange);
			break;
		case InfluenceType.Box:
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(Vector3.zero, InfluenceDimensions);
			break;
		default:
			break;
			

		}
	}
}
