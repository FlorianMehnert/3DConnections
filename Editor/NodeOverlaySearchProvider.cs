#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
static class NodeOverlaySearchProvider
{
    const string providerId = "nodeoverlay";
    const string filterId = "node:";

    [SearchItemProvider]
    internal static SearchProvider CreateProvider()
    {
        return new SearchProvider(providerId, "Node Overlay")
        {
            filterId = filterId,
            priority = 500,
            isEnabledForContextualSearch = () => true,

            fetchItems = (context, items, provider) =>
            {
                var parent = GameObject.Find("ParentNodesObject");
                if (parent == null)
                    return null;

                // Parse query tokens like "name:Foo", "type:Trigger", etc.
                var tokens = ParseQuery(context.searchQuery);

                foreach (Transform child in parent.transform)
                {
                    GameObject go = child.gameObject;
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn == null) continue;

                    string nodeName = go.name.ToLowerInvariant();
                    string connType = go.tag.ToLowerInvariant(); // or from component/metadata
                    string outNames = string.Join(", ", conn.outConnections.Where(o => o).Select(o => o.name.ToLowerInvariant()));
                    string inNames = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name.ToLowerInvariant()));

                    // Match query tokens
                    if (!MatchesTokens(tokens, nodeName, connType, inNames + "," + outNames))
                        continue;

                    string label = go.name;
                    string description = $"→ [{outNames}]\n← [{inNames}]";

                    var thumbnail = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;

                    var item = provider.CreateItem(
                        id: go.GetInstanceID().ToString(),
                        score: 0,
                        label: label,
                        description: description,
                        thumbnail: thumbnail,
                        data: go
                    );

                    items.Add(item);
                }

                return null;
            },

            fetchLabel = (item, context) => item.label,
            fetchDescription = (item, context) => item.description,
            fetchThumbnail = (item, context) => item.thumbnail,
            toObject = (item, type) => item.data as GameObject,
            trackSelection = (item, context) =>
            {
                if (item.data is GameObject go)
                {
                    EditorGUIUtility.PingObject(go);
                    Selection.activeGameObject = go;
                }
            }
        };
    }

    // Utility: Parse query like "name:foo type:bar end:baz" into key-value pairs
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var tokens = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(query))
            return tokens;

        var parts = query.Split(' ');
        foreach (var part in parts)
        {
            int sep = part.IndexOf(':');
            if (sep > 0 && sep < part.Length - 1)
            {
                string key = part.Substring(0, sep).ToLowerInvariant();
                string value = part.Substring(sep + 1).ToLowerInvariant();
                tokens[key] = value;
            }
            else
            {
                tokens["any"] = part.ToLowerInvariant(); // fallback
            }
        }
        return tokens;
    }

    // Match node against the search tokens
    private static bool MatchesTokens(Dictionary<string, string> tokens, string nodeName, string connType, string endNames)
    {
        foreach (var kvp in tokens)
        {
            string key = kvp.Key;
            string val = kvp.Value;

            switch (key)
            {
                case "name":
                    if (!nodeName.Contains(val))
                        return false;
                    break;
                case "type":
                    if (!connType.Contains(val))
                        return false;
                    break;
                case "end":
                    if (!endNames.Contains(val))
                        return false;
                    break;
                case "any":
                    if (!(nodeName.Contains(val) || connType.Contains(val) || endNames.Contains(val)))
                        return false;
                    break;
            }
        }
        return true;
    }
}
#endif
