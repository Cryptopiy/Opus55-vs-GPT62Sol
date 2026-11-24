using System;
using UnityEngine;

public sealed class IslandModels : ScriptableObject
{
    public GameObject lighthouse, rowboat, driftwood;
    public GameObject[] palms, rocks;

    public static IslandModels Load() => Resources.Load<IslandModels>("IslandModels");

    public static Transform Create(GameObject source,string name,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale)
    {
        Transform t = Instantiate(source,parent).transform;
        t.name = name;
        t.SetPositionAndRotation(position,rotation);
        t.localScale = scale;
        return t;
    }

    public static GameObject Part(GameObject source,string name)
    {
        Transform child=source.transform.Find(name);
        if (!child) throw new InvalidOperationException("Missing Blender part " + name);
        return child.gameObject;
    }
}

