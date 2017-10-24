using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
public class EnumFlagsPropertyDrawer : PropertyDrawer 
{
	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
	{
		var typeAttr = attribute as EnumFlagsAttribute;
		property.intValue = EditorGUI.MaskField(position, label, property.intValue, System.Enum.GetNames(typeAttr.type));  
	}
}