using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Navigator : MonoBehaviour 
{
	static Navigator instance;
	public static Navigator Instance
	{
		get
		{
			if (instance == null)
				instance = FindObjectOfType<Navigator>();
			return instance;
		}
	}

	/// <summary>
	/// Changes the penalty cost traversing waypoint
	/// </summary>
	public delegate float CustomPenalty(Waypoint waypoint);

	/// <summary>
	/// If returns true, current waypoint will be ignored in searches
	/// </summary>
	public delegate bool CustomIgnoreWaypoint(Waypoint waypoint);

	/// <summary>
	/// If returns true, current waypoint will become the goal destination
	/// </summary>
	public delegate bool CustomGoal(Waypoint waypoint);

	[System.Flags]
	public enum Sides
	{
		Sides = (1 << 0),
		Top = (1 << 1),
		Bottom = (1 << 2)
	}

	[ExecuteInEditMode]
	void Awake()
	{
		if (instance == null)
			instance = FindObjectOfType<Navigator>();
		if (instance != this)
			Destroy(gameObject);
	}

	[ExecuteInEditMode]
	void OnDestroy()
	{
		#if UNITY_EDITOR
			ClearAll();
		#endif
	}

	[Header("Settings")]
	[Range(1, 100), Tooltip("Maximum number of Waypoints that can be processed per frame")]
	public int MaxWaypointUpdatesPerFrame = 10;
	[Range (1, 20), Tooltip("Maximum number of Pathing Calculations that can be processed per frame")]
	public int MaxPathingUpdatesPerFrame = 5;
	[Tooltip("Maximum length that any path between nodes can be")]
	public float maxPathLength = 5f;
	[Tooltip("Maximum number of nodes that can be traversed while pathfinding")]
	public int maxNodeTraversal = 50;
	[Tooltip("Choose whether to check path integrity using Sphere Casts")]
	public bool useSphereCast = false;
	[Tooltip("Limit the maximum radius of spherecast from waypoints")]
	public float maxRadiusCheck = 1f;
	[Tooltip("Layer which to check for Waypoints")]
	public LayerMask WaypointLayer;
	[Tooltip("Layers that can block waypoint paths")]
	public LayerMask CollisionLayer;
	[Tooltip("Layers which can block path finding")]
	public LayerMask NavigationCollisionLayer;

	[Header("Gizmo Controls")]
	[Tooltip("Show pathing Gizmos")]
	public bool drawGizmos;
	[Range(0f, 1f), Tooltip("Strength of connections")]
	public float gizmoStrength = .8f;
	[Tooltip("Path connection color")]
	public Color connectionColor = Color.white;
	[Tooltip("Penalty color of paths")]
	public Color penaltyColor = Color.red;
	[EnumFlagsAttribute(typeof(Sides)), Tooltip("Which sides of sphere cast to show when drawing gizmo")]
	public Sides SphereCastConnectors;

	[Header("Debug")]
	[Tooltip("How many dynamic waypoints are pending an update")]
	public int PendingWaypoints;
	[Tooltip("How many neighbouring waypoints are pending an update")]
	public int PendingNeighbours;
	[Tooltip("How many paths are pending an update")]
	public int PendingPaths;

	Queue<Waypoint> waypointsPendingUpdate = new Queue<Waypoint>();
	Queue<Waypoint> neighboursPendingUpdate = new Queue<Waypoint>();
	Queue<NavigatorInfo> pathingPendingUpdate = new Queue<NavigatorInfo>();
	HashSet<NavigatorInfo> pathUpdateLookup = new HashSet<NavigatorInfo>();

	HashSet<Waypoint> vistedNodes = new HashSet<Waypoint>();
	HashSet<Waypoint> ignoreNodes = new HashSet<Waypoint>();
	List<Waypoint> searchNodes = new List<Waypoint>();

	void Update()
	{
		PendingWaypoints = waypointsPendingUpdate.Count;
		PendingNeighbours = neighboursPendingUpdate.Count;
		PendingPaths = pathingPendingUpdate.Count;

		Waypoint.Update();
		DynamicNavigationCollider.Update();

		int itemsProcessed = 0;

		while (neighboursPendingUpdate.Count > 0 && itemsProcessed < MaxWaypointUpdatesPerFrame)
		{
			var waypoint = neighboursPendingUpdate.Dequeue();
			if (waypoint.pendingUpdate) continue;
			ProcessNeighbourWaypoint(waypoint);
			itemsProcessed ++;
		}

		itemsProcessed = 0;

		while (waypointsPendingUpdate.Count > 0 && itemsProcessed < MaxWaypointUpdatesPerFrame)
		{
			var waypoint = waypointsPendingUpdate.Dequeue();
			ProcessWaypoint(waypoint);
			itemsProcessed ++;
		}

		itemsProcessed = 0;

		while (pathingPendingUpdate.Count > 0 && itemsProcessed < MaxPathingUpdatesPerFrame)
		{
			itemsProcessed ++;
			var info = pathingPendingUpdate.Dequeue();
			if (pathUpdateLookup.Contains(info))
			{
				GetPath(info);
				pathUpdate.Push(info);
			}
		}
		while (pathUpdate.Count > 0)
		{
			pathingPendingUpdate.Enqueue(pathUpdate.Pop());
		}
	}
	Stack<NavigatorInfo> pathUpdate = new Stack<NavigatorInfo>();


	void ProcessWaypoint(Waypoint waypoint)
	{
		if (waypoint == null) return;

		ClearNeighbours(waypoint);

		var count = Physics.OverlapSphereNonAlloc(waypoint.position, maxPathLength, colliders, WaypointLayer);

		for(int i = 0; i < count; ++ i)// ( var collider in colliders)
		{
			var neighbourWaypoint = colliders[i].GetComponent<Waypoint>();
			if (neighbourWaypoint == null || neighbourWaypoint == waypoint) continue;

			QueueNeighbourUpdate(neighbourWaypoint);

			var direction = neighbourWaypoint.position - waypoint.position;
			var distance = direction.magnitude;
			if (distance > waypoint.maxPath || distance == 0) continue;

			if (useSphereCast)
			{
				if (!Physics.SphereCast(new Ray(waypoint.position, direction), waypoint.radius, distance, CollisionLayer |= NavigationCollisionLayer))
				{
					waypoint.neighbours.Add(GetNeighbourContainer(neighbourWaypoint, distance));
				}
			}
			else if (!Physics.Raycast(waypoint.position, direction, distance, CollisionLayer |= NavigationCollisionLayer))
			{
				waypoint.neighbours.Add(GetNeighbourContainer(neighbourWaypoint, distance));
			}
		}

		waypoint.neighbours.Sort( (x,y) =>
			{
				return x.distanceToNeighbour.CompareTo(y.distanceToNeighbour);
			});
				
		waypoint.pendingUpdate = false;
		waypoint.pendingNeighbourUpdate = false;
	}

	void ProcessNeighbourWaypoint(Waypoint waypoint)
	{
		if (waypoint == null) return;
		ClearNeighbours(waypoint);

		int count = Physics.OverlapSphereNonAlloc(waypoint.position, maxPathLength, colliders, WaypointLayer);

		for (int i = 0; i < count; ++ i)
		{
			var neighbourWaypoint = colliders[i].GetComponent<Waypoint>();
			if (neighbourWaypoint == null || neighbourWaypoint == waypoint) continue;

			var direction = neighbourWaypoint.position - waypoint.position;
			var distance = direction.magnitude;

			if (distance > waypoint.maxPath || distance == 0) continue;

			if (useSphereCast)
			{
				if (!Physics.SphereCast(new Ray(waypoint.position, direction), waypoint.radius, distance, CollisionLayer |= NavigationCollisionLayer))
				{
					waypoint.neighbours.Add(GetNeighbourContainer(neighbourWaypoint, distance));
				}
			}
			else if (!Physics.Raycast(waypoint.transform.position, direction, distance, CollisionLayer |= NavigationCollisionLayer))
			{
				waypoint.neighbours.Add(GetNeighbourContainer(neighbourWaypoint, distance));
			}
		}

		waypoint.neighbours.Sort( (x,y) =>
			{
				return x.distanceToNeighbour.CompareTo(y.distanceToNeighbour);
			});
		
		waypoint.pendingNeighbourUpdate = false;
	}

	public static void StartUpdates (NavigatorInfo pathingInfo)
	{
		if (Instance.pathUpdateLookup.Contains(pathingInfo)) return;
		Instance.pathUpdateLookup.Add(pathingInfo);
		Instance.pathingPendingUpdate.Enqueue(pathingInfo);
	}

	public static void StopUpdates (NavigatorInfo pathingInfo)
	{
		if (Instance && Instance.pathUpdateLookup != null)
			Instance.pathUpdateLookup.Remove(pathingInfo);
	}

	public void QueueWaypointUpdate (Waypoint waypoint) 
	{
		if (!waypoint.pendingUpdate)
		{
			waypoint.pendingUpdate = true;
			waypoint.pendingNeighbourUpdate = false;
			waypointsPendingUpdate.Enqueue(waypoint);
		}
	}

	public void QueueNeighbourUpdate (Waypoint waypoint)
	{
		if (waypoint.pendingUpdate || waypoint.pendingNeighbourUpdate)
			return;
		
		waypoint.pendingNeighbourUpdate = true;
		neighboursPendingUpdate.Enqueue(waypoint);
	}

	public void ClearNeighbours(Waypoint waypoint)
	{
		if (!waypoint) return;
		foreach(var neighbour in waypoint.neighbours)
		{
			neighbourPool.Push(neighbour);
		}
		waypoint.neighbours.Clear();
	}

	public void RemoveWaypoint (Waypoint waypoint)
	{
		foreach(var neighbour in waypoint.neighbours)
		{
			if (neighbour == null) continue;
			neighbour.neighbourWaypoint.RemoveWaypointFromNeighbours(waypoint, neighbourPool);
			neighbourPool.Push(neighbour);

			var w = neighbour.neighbourWaypoint;
			if (!w.pendingUpdate && !w.pendingNeighbourUpdate)
			{
				neighbour.neighbourWaypoint.pendingNeighbourUpdate = true;
				neighboursPendingUpdate.Enqueue(neighbour.neighbourWaypoint);	
			}
		}
		waypoint.neighbours.Clear();
	}
		
	Waypoint GetClosestWaypoint(Vector3 goal)
	{
		//Find Start Node
		var shortestDistance = Mathf.Infinity;
		Collider shortestCollider = null;
		int count = Physics.OverlapSphereNonAlloc(goal, maxPathLength, colliders, WaypointLayer);
		for (int i = 0; i < count; ++i)
		{
			var distance = (goal - colliders[i].transform.position).magnitude;
			if (distance < shortestDistance)
			{
				shortestDistance = distance;
				shortestCollider = colliders[i];
			}
		}
		if (shortestCollider) return shortestCollider.GetComponent<Waypoint>();
		return null;
	}

	Stack<Vector3> ReverseList = new Stack<Vector3>(); // sucks using stack for this, but using List.Reverse causes allocations

	[HideInInspector]
	public Collider[] colliders = new Collider[64];

	public void GetPath(NavigatorInfo info)// CustomIgnoreWaypoint IgnoreFunction, CustomGoal GoalFunction, CustomPenalty PenaltyFunction)
	{
		Waypoint startNode = null;

		var path = info.Path;
		var start = info.currentPosition;
		var goal = info.goalPosition;
		var IgnoreFunction = info.IgnoreWaypointFunction;
		var GoalFunction = info.GoalWaypointFunction;
		var PenaltyFunction = info.WaypointPenaltyFunction;

		info.NodeTraversalCount = 0;
		path.Clear();

		startNode = GetClosestWaypoint(start);//, goal);	// if no starting waypoint, just head toward gaol

		//Debug.Log(startNode);
		if (startNode == null || (start - goal).magnitude < info.minDistanceToWaypoint)
		{
			path.Add(start);
			path.Add(goal);
			return;
		}

		Waypoint endNode = GetClosestWaypoint(goal);//, start);

		if (endNode != null)
		{
			if ( (start - goal).sqrMagnitude < (endNode.position - goal).sqrMagnitude || startNode == endNode)
			{
				path.Add(start);
				path.Add(goal);
				return;
			}
		}
//
		Waypoint currentNode = null;
		vistedNodes.Clear();	// clear lists
		ignoreNodes.Clear();
		searchNodes.Clear();

		if (IgnoreFunction == null)	// set functions to default if not included
			IgnoreFunction = (Waypoint waypoint) => {return false;};
		if (GoalFunction == null)
			GoalFunction = (Waypoint waypoint) => {return false;};
		if (PenaltyFunction == null)
			PenaltyFunction = (Waypoint waypoint) => {return waypoint.penalty;};

		searchNodes.Add(startNode);	
		startNode.distTravelled = 0f;
		startNode.previous = null;

		Waypoint closestNode = startNode;

		int loopcount = 0;
		while(searchNodes.Count > 0 && loopcount < maxNodeTraversal)
		{
			loopcount ++;
			currentNode = searchNodes[searchNodes.Count - 1];
			searchNodes.RemoveAt(searchNodes.Count - 1);

			if (closestNode.distToTarget > currentNode.distToTarget)
			{
				closestNode = currentNode;

				if (closestNode.distTravelled > info.MaxPathingDistance)
				{
					break;
				}
			}

			vistedNodes.Add(currentNode); 
			var foundEndWaypoint = false;

			foreach(var neighbourInfo in currentNode.neighbours)
			{
				var neighbour = neighbourInfo.neighbourWaypoint;

				if (ignoreNodes.Contains(neighbour))
					continue;

				float distTravelled;
				distTravelled = currentNode.distTravelled + neighbourInfo.distanceToNeighbour + Mathf.Max(0, PenaltyFunction(neighbour));

				if (vistedNodes.Contains(neighbour))
				{
					continue;
					if (neighbour.distTravelled > distTravelled)
					{
						neighbour.distTravelled = distTravelled;
						neighbour.previous = currentNode;
					}
				}
				else
				{
					if (IgnoreFunction(neighbour))
					{
						ignoreNodes.Add(neighbour);
						continue;
					}

					neighbour.distTravelled = distTravelled;
					neighbour.distToTarget = (goal - neighbourInfo.neighbourWaypoint.position).magnitude + Mathf.Max(0, PenaltyFunction(neighbour));
					neighbour.previous = currentNode;

					if (neighbour == endNode || GoalFunction(neighbour))
					{
						closestNode = neighbour;
						foundEndWaypoint = true;
						break;	
					}

					vistedNodes.Add(neighbour);
					searchNodes.Add(neighbour);
				}
			}
			if (foundEndWaypoint)
				break;

			searchNodes.Sort( (x,y) =>	// sorting nodes in reverse order since it's faster to remove items from the end of a list
				{
					return y.hueristic.CompareTo(x.hueristic);
				});
		}

		currentNode = closestNode;

		if ((goal - closestNode.position).magnitude > info.minDistanceToWaypoint)
		{
			ReverseList.Push(goal);
			//path.Add(goal);
		}

		while (currentNode.previous != null)
		{
			ReverseList.Push(currentNode.position);
			//path.Add(currentNode.position);
			currentNode = currentNode.previous;
		}
		if ((currentNode.position - start).magnitude > info.minDistanceToWaypoint)
		{
			ReverseList.Push(currentNode.position);
			//path.Add(currentNode.position);
		}
		while (ReverseList.Count > 0)
		{
			path.Add(ReverseList.Pop());
		}
		//path.Reverse();
		info.NodeTraversalCount = loopcount;

		return;
	}

	[HideInInspector]
	public int poolCount;

	/// <summary>
	/// Neighbour Object Pool
	/// </summary>

	Stack<NeighbourInfo> neighbourPool = new Stack<NeighbourInfo>();
	NeighbourInfo GetNeighbourContainer(Waypoint waypoint, float distance)
	{
		if (neighbourPool.Count > 0)
		{
			var neighbour = neighbourPool.Pop();
			neighbour.neighbourWaypoint = waypoint;
			neighbour.distanceToNeighbour = distance;
			return neighbour;
		}
		return new NeighbourInfo(waypoint, distance);
	}

	#if UNITY_EDITOR

	[SerializeField]
	Waypoint[] allWaypoints;

	[ContextMenu("Preview All Paths")]
	public void PreviewAllPaths()
	{
		allWaypoints = FindObjectsOfType<Waypoint>();

		foreach(var waypoint in allWaypoints)
		{
			PreviewPath(waypoint);
		}
	}

	public void PreviewPath(Waypoint waypoint)
	{
		waypoint.neighbours.Clear();
		waypoint.position = waypoint.transform.position;
		int count = Physics.OverlapSphereNonAlloc(waypoint.transform.position, maxPathLength, colliders, WaypointLayer);

		for (int i = 0; i < count; ++ i)
		{
			var neighbourWaypoint = colliders[i].GetComponent<Waypoint>();
			if (neighbourWaypoint == null || neighbourWaypoint == waypoint) continue;

			var direction = neighbourWaypoint.transform.position - waypoint.transform.position;
			var distance = direction.magnitude;
			if (distance > waypoint.maxPath || distance == 0f) continue;

			if (useSphereCast)
			{
				if (!Physics.SphereCast(new Ray(waypoint.transform.position, direction), waypoint.radius, distance, CollisionLayer |= NavigationCollisionLayer))
				{
					waypoint.neighbours.Add(new NeighbourInfo(neighbourWaypoint, distance));
				}
			}
			else if (!Physics.Raycast(waypoint.transform.position, direction, distance, CollisionLayer |= NavigationCollisionLayer))
			{
				waypoint.neighbours.Add(new NeighbourInfo(neighbourWaypoint, distance));
			}
		}

		waypoint.neighbours.Sort( (x,y) =>
			{
				return x.distanceToNeighbour.CompareTo(y.distanceToNeighbour);
			});
	}

	public void ClearAll()
	{
		if (allWaypoints == null) return;

		foreach(var item in allWaypoints)
		{
			if (!item) continue;
			ClearNeighbours(item);
			item.position = Vector3.zero;
		}
		allWaypoints = null;
	}

	#endif
		
}

[System.Serializable]
public class NavigatorInfo
{
	public Navigator.CustomGoal GoalWaypointFunction;
	public Navigator.CustomIgnoreWaypoint IgnoreWaypointFunction;
	public Navigator.CustomPenalty WaypointPenaltyFunction;

	public Vector3 currentPosition = Vector3.zero;
	public Vector3 goalPosition = Vector3.zero;

	public float minDistanceToWaypoint = .1f;
	public float MaxPathingDistance = 20f;
	[SerializeField]
	public List<Vector3> Path = new List<Vector3>();
	public int NodeTraversalCount;

	public Vector3 NextPosition
	{
		get 
		{
			if (Path.Count >= 2)
			{
				if ( (Path[1] - currentPosition).magnitude > (Path[1] - Path[0]).magnitude + minDistanceToWaypoint)
				{
					return Path[0];
				}
				return Path[1];
			}
			return goalPosition;
		}
	}

	public Vector3 DirectionToNextPosition
	{
		get
		{
			var direction = (NextPosition - currentPosition).normalized;
			return direction;
		}
	}

	static int newHash = 0;
	int _hash = -1;
	public override int GetHashCode () //guaranteed unique hash xD
	{
		if (_hash < 0)
			_hash = newHash ++;
		return _hash;
	}
}


#if UNITY_EDITOR

[CustomEditor(typeof(Navigator))]
public class WaypointManagerInspector : Editor
{
	Navigator navigator;

	void OnEnable()
	{
		navigator = (Navigator)target;
	}

	public override void OnInspectorGUI ()
	{
		base.OnInspectorGUI ();

		if (Application.isPlaying) return;
			
		if (GUILayout.Button("Update Path Preview"))
		{
			navigator.PreviewAllPaths();
		}

		if (GUILayout.Button("Clear Preview"))
		{
			navigator.ClearAll();
		}
	}
}


#endif

