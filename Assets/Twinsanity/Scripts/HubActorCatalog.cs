using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class HubActorDefinition
{
    public string chunk, resource, sourceName, kind;
    public int objectId, idleClip, walkClip, runClip, hitClip, actionClip;
    public int[] clipIds, soundIds;
    public string[] clipNames;
}

public static class HubActorCatalog
{
    [Serializable] private sealed class Document { public HubActorDefinition[] actors; }
    private static Dictionary<string, HubActorDefinition> definitions;
    public static HubActorDefinition Find(string chunk, int objectId)
    {
        if (definitions == null)
        {
            definitions = new Dictionary<string, HubActorDefinition>(StringComparer.OrdinalIgnoreCase);
            TextAsset source = Resources.Load<TextAsset>("HubActors/Catalog");
            if (source == null) throw new InvalidOperationException("Missing HubActors/Catalog.json.");
            foreach (HubActorDefinition entry in JsonUtility.FromJson<Document>(source.text).actors)
                definitions[entry.chunk + ":" + entry.objectId] = entry;
        }
        definitions.TryGetValue(chunk + ":" + objectId, out HubActorDefinition result);
        return result;
    }
    public static HubActor Attach(GameObject placement, HubActorDefinition definition)
    {
        HubActor actor = placement.GetComponent<HubActor>();
        if (actor != null) return actor;
        actor = placement.AddComponent<HubActor>();
        actor.definition = definition;
        actor.BuildVisual();
        actor.ConfigureCollision();
        return actor;
    }
}
