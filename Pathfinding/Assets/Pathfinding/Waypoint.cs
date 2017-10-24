using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Waypoint : MonoBehaviour 
{
	[SerializeField, HideInInspector]
	bool _dynamicWaypoint;

	/// <summary>
	/// Marks the Waypoint as Dynamic, automatically updates paths when moved
	/// </summary>
	public bool dynamicWaypoint
	{
		get {return _dynamicWaypoint;}
		set 
		{
			if (value && !_dynamicWaypoint)
			{
				waypoints.Add(this);
			}
			if(!value && _dynamicWaypoint)
			{
				waypoints.Remove(this);
			}
			_dynamicWaypoint = value;
		}
	}


	[SerializeField]
	float _maxPath = 5f;

	/// <summary>
	/// Maximum distance that this node can path to another node
	/// </summary>
	public float maxPath
	{
		get 
		{
			if (_maxPath > Navigator.Instance.maxPathLength)
				_maxPath = Navigator.Instance.maxPathLength;				
			return _maxPath;
		}
		set 
		{
			value = Mathf.Clamp(value, 0f, Navigator.Instance.maxPathLength);
			_maxPath = value;
		}
	}

	[SerializeField]
	float _radius = 1f;

	/// <summary>
	/// If using sphere casts, determines the size ray used.
	/// Units smaller than the radius should be able to use the path no problem.
	/// </summary>
	/// <value>The radius.</value>
	public float radius
	{
		get 
		{
			if (_radius > Navigator.Instance.maxRadiusCheck)
				_radius = Navigator.Instance.maxRadiusCheck;
			return _radius;
		}
		set
		{
			value = Mathf.Clamp(value, .1f, Navigator.Instance.maxRadiusCheck);
			_radius = value;
		}
	}

	/// <summary>
	/// Increasing this value makes the node less desirable to traverse
	/// </summary>
	public float penalty = 0;

	/// <summary>
	/// distance to travel to get to this node
	/// </summary>
	[NonSerialized]
	public float distTravelled = 0;

	/// <summary>
	/// distance to target destination from this node
	/// </summary>
	[NonSerialized]
	public float distToTarget = Mathf.Infinity;

	/// <summary>
	/// Heuristic used to determine shortest path to destination
	/// </summary>
	public float hueristic
	{
		get {return distTravelled + distToTarget;}
	}

	/// <summary>
	/// Tags the waypoint as a type fo waypoint. Useful when using Node functions
	/// </summary>
	public WaypointType type;

	[NonSerialized]
	Navigator manager;

	/// <summary>
	/// The previous node used to traverse to here.
	/// </summary>
	[NonSerialized]
	public Waypoint previous;

	/// <summary>
	/// Is the node pending a full update
	/// </summary>
	[NonSerialized]
	public bool pendingUpdate = false;

	/// <summary>
	/// Is the node pending an update of it's neighbours.
	/// </summary>
	[NonSerialized]
	public bool pendingNeighbourUpdate = false;

	/// <summary>
	/// List of all the nodes this node can path to
	/// </summary>
	[SerializeField] //[HideInInspector]
	public List<NeighbourInfo> neighbours;

	/// <summary>
	/// Current position used for pathfinding
	/// </summary>
	[HideInInspector, SerializeField]
	public Vector3 position;

	Collider Collider;

	void Awake()
	{
		//LayerMask layer = LayerMask.NameToLayer("Waypoint");
		//LayerMask layer = Navigator.Instance.WaypointLayer;

//		if (gameObject.layer != layer)
//			gameObject.layer = layer;

		manager = Navigator.Instance;

		Collider = GetComponent<Collider>();

		if (!Collider)
		{
			Collider = gameObject.AddComponent<SphereCollider>();
		}
	}

	void OnEnable()
	{
		position = transform.position;

		if (manager)
		{
			manager.QueueWaypointUpdate(this);
		}
		if (dynamicWaypoint)
			waypoints.Add(this);
	}

	void OnDisable()
	{
		if (manager)
			manager.RemoveWaypoint(this);
		waypoints.Remove(this);
	}

	[ContextMenu("Queue Update")]
	public void QueueUpdate()
	{
		position = transform.position;
		manager.QueueWaypointUpdate(this);
	}

	public void RemoveWaypointFromNeighbours(Waypoint waypoint, Stack<NeighbourInfo> pool)
	{
		for (int i = 0; i < neighbours.Count; ++i)
		{
			if (neighbours[i].neighbourWaypoint == waypoint)
			{
				pool.Push(neighbours[i]);
				neighbours.RemoveAt(i);
				return;
			}
		}
	}

	static List<Waypoint> waypoints = new List<Waypoint>();

	void UpdateWaypoint()
	{
		if (!dynamicWaypoint) return;

		if (position != Collider.transform.position)
		{
			int count = Physics.OverlapSphereNonAlloc(position, manager.maxPathLength, manager.colliders, Navigator.Instance.WaypointLayer);

			for (int i = 0; i < count; ++i)
			{
				var node = manager.colliders[i].GetComponent<Waypoint>();
				if(node)
					manager.QueueNeighbourUpdate(node);
			}

			position = transform.position;
			manager.QueueWaypointUpdate(this);	
		}
	}

	public static void Update()
	{
		for (int i = 0; i < waypoints.Count; ++i)
		{
			waypoints[i].UpdateWaypoint();
		}
	}

	#if UNITY_EDITOR

	void OnDrawGizmos()
	{
		var nav = Navigator.Instance;

		//Gizmos.DrawWireCube(transform.position, new Vector3(nav.maxRadiusCheck, nav.maxRadiusCheck, 5f));

		if (Navigator.Instance.drawGizmos)
		{
			Gizmos.color = Color.Lerp(nav.connectionColor*nav.gizmoStrength, nav.penaltyColor*nav.gizmoStrength, penalty/10f);	
		
			if (nav.useSphereCast && neighbours.Count > 0)
			{
				Gizmos.DrawWireSphere(position, radius);
			}

			foreach(var info in neighbours)
			{
				var neighbour = info.neighbourWaypoint;
				if (neighbour != null)
				{
					if (!nav.useSphereCast)
						Gizmos.DrawLine(position, position + (neighbour.position - position)/2f);	
					else 
					{
						var direction = neighbour.position - position;
						var distance = direction.magnitude;

						if (direction == Vector3.zero) continue;

						var rot = Quaternion.LookRotation( direction, Vector3.up);

						if ((nav.SphereCastConnectors & Navigator.Sides.Sides) > 0)
						{
							DrawSphereGizmoConnectors(neighbour, rot, Vector3.left);
							DrawSphereGizmoConnectors(neighbour, rot, Vector3.right);
						}

						if ((nav.SphereCastConnectors & Navigator.Sides.Top) > 0)
							DrawSphereGizmoConnectors(neighbour, rot, Vector3.up);
						if ((nav.SphereCastConnectors & Navigator.Sides.Bottom) > 0)
							DrawSphereGizmoConnectors(neighbour, rot, Vector3.down);
					}
				}
			}
		}
	}

	void DrawSphereGizmoConnectors(Waypoint neighbour, Quaternion rot, Vector3 drawSide)
	{
		Gizmos.DrawLine(position + rot *drawSide * radius, MidPoint (position + rot *drawSide * radius,	neighbour.position + rot *drawSide* neighbour.radius));
	}

	Vector3 MidPoint(Vector3 a, Vector3 b)
	{
		return new Vector3( (a.x + b.x)/2f, (a.y + b.y)/2f, (a.z + b.z)/2f);
	}
	#endif

	static int newHash = 0;
	int _hash = -1;
	public override int GetHashCode () //guaranteed unique hash xD
	{
		if (_hash < 0)
			_hash =newHash ++;
		return _hash;
	}
}

[System.Serializable]
public class NeighbourInfo
{
	public NeighbourInfo(){}

	public NeighbourInfo(Waypoint waypoint, float distance)
	{
		this.neighbourWaypoint = waypoint;
		this.distanceToNeighbour = distance;
	}

	public Waypoint neighbourWaypoint;
	public float distanceToNeighbour;
}
	
#if UNITY_EDITOR
[CustomEditor(typeof(Waypoint)), CanEditMultipleObjects]
public class WaypointInspector : Editor
{
	Waypoint waypoint;	
	void OnEnable()
	{
		waypoint = (Waypoint)target;
	}

	public override void OnInspectorGUI ()
	{
		bool test = waypoint.dynamicWaypoint;

		EditorGUI.BeginChangeCheck();

		if (!Application.isPlaying)
		{
			if (targets.Length == 1 && GUILayout.Button("Update Waypoint Preview"))
			{
				foreach(var item in waypoint.neighbours)
				{
					item.neighbourWaypoint.RemoveWaypointFromNeighbours(waypoint, new Stack<NeighbourInfo>());
				}

				Navigator.Instance.PreviewPath(waypoint);
				foreach(var neighbour in waypoint.neighbours)
				{
					Navigator.Instance.PreviewPath(neighbour.neighbourWaypoint);
				}
			}
			if (targets.Length > 1 && GUILayout.Button("Update Waypoints Preview"))
			{
				Navigator.Instance.PreviewAllPaths();
			}
		}
			
		waypoint.dynamicWaypoint = EditorGUILayout.Toggle(new GUIContent("Dynamic Waypoint", "Marks waypoint as dynamic, automatically updates when it's position is moved"), waypoint.dynamicWaypoint);
		waypoint.type = (WaypointType)EditorGUILayout.EnumPopup(new GUIContent("Type", "Type of Waypoint, used for querying nodes during callback functions"), waypoint.type);
		waypoint.maxPath = EditorGUILayout.Slider(new GUIContent("Max Path", "Maximum distance this node can path to others"), waypoint.maxPath, 0f, Navigator.Instance.maxPathLength);
		waypoint.penalty = EditorGUILayout.Slider(new GUIContent ("Penalty", "Penalty to path finding when traversing this node"), waypoint.penalty, 0f, 10f);

		if (Navigator.Instance.useSphereCast)
			waypoint.radius = EditorGUILayout.Slider(new GUIContent("RadiusSize", "Radius of spherecast to check valid paths"), waypoint.radius, .1f, Navigator.Instance.maxRadiusCheck);

		if (EditorGUI.EndChangeCheck())
		{
			foreach(var item in targets)
			{
				var otherTargets = (Waypoint)item;
				otherTargets.maxPath = waypoint.maxPath;
				otherTargets.dynamicWaypoint = waypoint.dynamicWaypoint;
				otherTargets.radius = waypoint.radius;
				otherTargets.penalty = waypoint.penalty;
			}
		}

		if (debug = EditorGUILayout.Foldout(debug, "Debug"))
		{
			base.OnInspectorGUI ();	
		}
	}

	bool debug;
}

#endif