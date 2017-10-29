using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicNavigationCollider : MonoBehaviour 
{
	static List<DynamicNavigationCollider> navigatorColliders = new List<DynamicNavigationCollider>();
	[Tooltip("Displays how far it will influence the pathing of colliders")]
	public bool showInfluence;
	[Tooltip("How far should this collider be able to influence waypoints when it moves")]
	public float InfluenceRange = 5f;

	Vector3 position;
	Quaternion rotation;
	Collider col;

	void Awake()
	{
		col = GetComponent<Collider>();
		if (!col)
		{
			Debug.LogErrorFormat("{0} requires an attached Collider for Dynamic Navigation", name);
			gameObject.SetActive(false);
		}			
	}

	void OnEnable()
	{
		position = transform.position;
		navigatorColliders.Add(this);
		ForceUpdate();
	}

	void OnDisable()
	{
		foreach(var item in disabledWaypoints)
		{
			if (item)
				item.gameObject.SetActive(true);
		}
		if (!Navigator.Instance) return;
		navigatorColliders.Remove(this);
	}

	bool forceUpdate;

	[ContextMenu("ForceUpdate")]
	public void ForceUpdate()
	{
		forceUpdate = true;
	}

	List<Waypoint> disabledWaypoints = new List<Waypoint>();
	void UpdateCollider()
	{
		if (forceUpdate || position != transform.position || rotation != transform.rotation)
		{
			position = transform.position;
			rotation = transform.rotation;

			for (int i = 0; i < disabledWaypoints.Count; ++i)
			{
				var waypoint = disabledWaypoints[i];
				if ((waypoint.position - col.ClosestPoint(waypoint.position)).magnitude > waypoint.radius)
				{
					waypoint.gameObject.SetActive(true);
				}
			}
			disabledWaypoints.RemoveAll( w => {return w.gameObject.activeSelf;});

			int count = 0;

			var colliders = Navigator.Instance.colliders;
			count = Physics.OverlapSphereNonAlloc(position, InfluenceRange, colliders, Navigator.Instance.WaypointLayer);

			for (int i = 0; i < count; ++ i)
			{
				var waypoint = colliders[i].GetComponent<Waypoint>();
				if (waypoint)
				{
					if ((waypoint.position - col.ClosestPoint(waypoint.position)).magnitude < waypoint.radius)
					{
						disabledWaypoints.Add(waypoint);
						waypoint.gameObject.SetActive(false);
					}
					else Navigator.Instance.QueueNeighbourUpdate(waypoint);
				}
			}
			forceUpdate = false;
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
		Gizmos.DrawWireSphere(transform.position, InfluenceRange);

	}
}
