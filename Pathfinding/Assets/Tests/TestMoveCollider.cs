using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMoveCollider : MonoBehaviour 
{
	Vector3 StartPosition;
	public Transform EndPosition;
	Rigidbody rb;

	void Start()
	{
		StartPosition = transform.position;
		rb = GetComponent<Rigidbody>();
	}

	// Update is called once per frame
	void FixedUpdate () 
	{
		rb.MovePosition(Vector3.Lerp(StartPosition, EndPosition.position, Mathf.Sin(Time.time)/2f+.5f));
	}
}
