using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnumFlagsAttribute : PropertyAttribute 
{
	public System.Type type;
	public EnumFlagsAttribute(System.Type type) 
	{
		this.type = type;
	}
}