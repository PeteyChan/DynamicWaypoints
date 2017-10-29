using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestPathfinderCountUI : MonoBehaviour 
{
	Text text;

	// Use this for initialization
	void Start () 
	{
		text = GetComponent<Text>();
	}
	
	// Update is called once per frame
	void Update () 
	{
		text.text = string.Format("Pathfinders : {0}", TestSpawner.PathfinderCount);
	}
}
