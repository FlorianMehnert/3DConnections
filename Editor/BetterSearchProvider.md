# Node Overlay Search Provider

A powerful Unity Editor search provider for finding and manipulating node and edge objects in the overlay scene. This tool provides advanced search capabilities for objects with `NodeType`, `EdgeType`, `LocalNodeConnections`, `ColoredObject`, and `ArtificialGameObject` components.

## Installation

1. Place the `BetterSearchProvider.cs` file in an `Editor` folder in your project
2. Ensure you have the required dependencies:
    - Unity Editor Coroutines package
    - Your custom node system components (NodeType, EdgeType, etc.)

## Basic Usage

### Opening the Search Window

1. Open Unity's Search window (`Ctrl+K` or `Cmd+K`)
2. Type `node:` followed by your search query
3. Results will appear showing matching objects from your node overlay scene

### Search Scope

By default, the search provider looks for objects that are children of:
- **ParentNodesObject** - Contains node objects with NodeType, LocalNodeConnections, ColoredObject components
- **ParentEdgesObject** - Contains edge objects with EdgeType components

## Search Syntax

### Basic Search Tokens

| Token | Description | Example |
|-------|-------------|---------|
| `name:` | Search by object name | `node: name:PlayerNode` |
| `tag:` | Search by Unity tag | `node: tag:Important` |
| `type:` | Alias for tag search | `node: type:Important` |
| `layer:` | Search by Unity layer | `node: layer:UI` |
| `active:` | Filter by active state | `node: active:true` |

### Component-Based Search

| Token | Description | Example |
|-------|-------------|---------|
| `t:` or `comp:` | Search by component type | `node: t:ColoredObject` |
| `nodetype:` | Search by NodeType.nodeTypeName | `node: nodetype:Component` |
| `edgetype:` | Search by EdgeType.connectionType | `node: edgetype:default` |
| `artificial:` | Has ArtificialGameObject component | `node: artificial:true` |
| `colored:` | Has ColoredObject component | `node: colored:false` |
| `hasref:` | NodeType has reference object | `node: hasref:true` |

### Connection-Based Search

| Token | Description | Example |
|-------|-------------|---------|
| `end:` | Search connection endpoints | `node: end:TargetNode` |
| `in:` | Search incoming connections | `node: in:SourceNode` |
| `out:` | Search outgoing connections | `node: out:DestinationNode` |
| `ref:` | References specific object | `node: ref:MyGameObject` |

### General Search

| Token | Description | Example |
|-------|-------------|---------|
| `any:` | Search anywhere (default) | `node: any:test` or `node: test` |

## Scope Modifiers

Control where the search looks for objects:

| Prefix | Description | Example |
|--------|-------------|---------|
| *none* | Search only nodes (default) | `node: name:test` |
| `edges:` | Search only edges | `node: edges: edgetype:connection` |
| `all:` | Search both nodes and edges | `node: all: name:test` |
| `h:` | Search entire scene hierarchy | `node: h: name:test` |

## Property-Based Search

Search for specific property values within components:

**Syntax:** `#ComponentType.propertyName:"value"`

**Examples:**
```
node: #NodeType.nodeTypeName:"GameObject"
node: #EdgeType.connectionType:"default"
node: #Transform.name:"PlayerNode"
```

## Action Commands

Execute actions on all matching objects by wrapping your query:

**Syntax:** `action{query}`

| Action | Description | Example |
|--------|-------------|---------|
| `highlight` | Highlight matching objects in red | `node: highlight{name:test}` |
| `select` | Select matching objects | `node: select{nodetype:Component}` |
| `focus` | Focus camera on first match | `node: focus{name:PlayerNode}` |
| `disable` | Disable matching objects | `node: disable{tag:Temporary}` |
| `enable` | Enable matching objects | `node: enable{tag:Temporary}` |
| `log` | Log matching objects to console | `node: log{artificial:true}` |

## Special Commands

| Command | Description |
|---------|-------------|
| `node: clearHighlights` | Clear all highlighted objects |

## Complex Query Examples

### Basic Searches
```bash
# Find all nodes named "Player"
node: name:Player

# Find all edge objects with default connection type
node: edges: edgetype:default

# Find all artificial game objects
node: artificial:true

# Find objects with ColoredObject component
node: t:ColoredObject
```

### Connection-Based Searches
```bash
# Find nodes connected to "TargetNode"
node: end:TargetNode

# Find nodes with outgoing connections to "Destination"
node: out:Destination

# Find nodes that reference "MyGameObject"
node: ref:MyGameObject
```

### Combined Searches
```bash
# Find active nodes with ColoredObject component
node: active:true t:ColoredObject

# Find GameObject-type nodes that are artificial
node: nodetype:GameObject artificial:true

# Search both nodes and edges for objects named "test"
node: all: name:test
```

### Action Examples
```bash
# Highlight all Component-type nodes
node: highlight{nodetype:Component}

# Select all edges with "default" connection type
node: select{edges: edgetype:default}

# Focus on first artificial node found
node: focus{artificial:true}

# Log all nodes with incoming connections
node: log{in:*}
```

### Property Search Examples
```bash
# Find NodeType components with specific nodeTypeName
node: #NodeType.nodeTypeName:"ScriptableObject"

# Find Transform components with specific names
node: #Transform.name:"PlayerController"

# Find EdgeType with specific connection type
node: #EdgeType.connectionType:"data"
```

## Search Results

Results display:
- **Object name** as the main label
- **Description** showing:
    - Connection information (→ outgoing, ← incoming)
    - Node/Edge type information
    - Reference object (if any)
    - Component list
- **Icon** based on object type (nodes vs edges)
- **Interactive highlighting** when selecting results

## Tips and Best Practices

1. **Use specific tokens** for faster, more accurate results
2. **Combine multiple tokens** to narrow down searches
3. **Use action commands** to perform bulk operations
4. **Leverage scope modifiers** to search in the right place
5. **Use property searches** for deep component inspection
6. **Clear highlights** regularly to avoid visual clutter

## Keyboard Shortcuts

- `Ctrl+K` / `Cmd+K` - Open search window
- `Enter` - Select highlighted result
- `Escape` - Close search window
- `Tab` - Accept search proposition

## Troubleshooting

**No results showing up:**
- Ensure `ParentNodesObject` and/or `ParentEdgesObject` exist in scene
- Check that objects have the required components
- Try using `h:` prefix to search entire hierarchy

**Search provider not appearing:**
- Ensure the script is in an `Editor` folder
- Check Unity console for compilation errors
- Restart Unity if necessary

**Highlighting not working:**
- Ensure objects have `ColoredObject` components
- Try the `clearHighlights` command if highlights are stuck