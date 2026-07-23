# Mesh Groups and Material Groups

This document describes how Source 2 models associate meshes with **mesh groups** (body groups) and how **material groups** (skins) remap materials across those meshes.

## Data Layout in the Model Resource

A compiled model (`vmdl_c`) contains several parallel arrays in its DATA block:

```
m_refMeshes              — mesh file references, indexed by mesh index
m_refMeshGroupMasks      — uint64 bitmask per mesh index
m_meshGroups             — group name strings, indexed by group index
m_nDefaultMeshGroupMask  — uint64 bitmask selecting which groups are active by default
m_materialGroups         — array of { m_name, m_materials[] } objects
```

Example from a compiled model:

```
m_refMeshes = [
    resource_name:"alchemist_model.vmesh",   // mesh index 0
    resource_name:"alchemist_lod.vmesh",      // mesh index 1
]
m_refMeshGroupMasks = [ 1, 1 ]               // both meshes → bit 0 set
m_meshGroups = [ "default" ]                  // group index 0
m_nDefaultMeshGroupMask = 1                   // bit 0 → "default" is active
```

## Mesh Groups (Body Groups)

### How Meshes Are Assigned to Groups

The assignment uses **bitmasks**, not indices or name matching. Each mesh has a corresponding entry in `m_refMeshGroupMasks` (one `uint64` per mesh). Each bit position in the mask corresponds to a group index in `m_meshGroups`:

| Bit position | Maps to |
|---|---|
| Bit 0 | `m_meshGroups[0]` |
| Bit 1 | `m_meshGroups[1]` |
| Bit N | `m_meshGroups[N]` |

A mesh belongs to every group whose bit is set in its mask. A mesh can belong to multiple groups simultaneously.

**Concrete example** with your data:

```
m_meshGroups = [
    "first_or_third_person_@0_#&thirdperson_default",   // group index 0
    "first_or_third_person_@1_#&firstperson_default",    // group index 1
]
m_refMeshGroupMasks = [ 0b01, 0b01, 0b01, 0b10, 0b10 ]
```

- Meshes 0–2 have bit 0 set → belong to the thirdperson group (index 0)
- Meshes 3–4 have bit 1 set → belong to the firstperson group (index 1)
- A mask of `0b11` would mean a mesh belongs to both groups

### The `@N` and `#&` Naming Convention

Mesh group names encode body group structure in a single string:

```
{BodyGroupName}_@{ChoiceIndex}[_#&{DisplayName}]
```

| Part | Meaning |
|---|---|
| `first_or_third_person` | The body group name |
| `@0`, `@1` | Choice index within the body group (not a mesh index) |
| `#&thirdperson_default` | Display name shown in tools |

The model extract code parses this by splitting on `_@` to reconstruct the body group hierarchy for modeldoc:

```csharp
// ModelExtract.ValveModel.cs:395-409
var fullName = meshGroups[i];
var split = fullName.Split("_@");
var groupName = split[0];       // "first_or_third_person"
var choiceName = split[1];      // "0_#&thirdperson_default"
```

The `#&` marker is stripped to recover the display name (`ModelExtract.ValveModel.cs:435-439`).

### Default Mesh Group Selection

`m_nDefaultMeshGroupMask` is a bitmask over group indices that determines which groups are active at load time:

```csharp
// Model.cs:503-507
var defaultGroupMask = Data.GetUnsignedIntegerProperty("m_nDefaultMeshGroupMask");
return GetMeshGroups().Where((group, index) => ((ulong)(1 << index) & defaultGroupMask) != 0);
```

### Rendering: Activating Mesh Groups

When the renderer activates a set of mesh groups, it rebuilds the list of visible meshes by testing each mesh's bitmask against the active groups:

```csharp
// ModelSceneNode.cs:578-594
private IEnumerable<bool> GetActiveMeshMaskForGroup(string groupName)
{
    var groupIndex = Array.IndexOf(meshGroups, groupName);
    if (groupIndex >= 0)
    {
        return meshGroupMasks.Select(mask => (mask & 1L << groupIndex) != 0);
    }
    return meshGroupMasks.Select(_ => false);
}
```

```csharp
// ModelSceneNode.cs:599-624
public void SetActiveMeshGroups(IEnumerable<string> setMeshGroups)
{
    activeMeshGroups = new HashSet<string>(meshGroups.Intersect(setMeshGroups));
    RenderableMeshes.Clear();

    if (meshGroups.Length > 1)
    {
        foreach (var group in activeMeshGroups)
        {
            var meshMask = GetActiveMeshMaskForGroup(group).ToArray();
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshMask[meshRenderer.MeshIndex])
                {
                    RenderableMeshes.Add(meshRenderer);
                }
            }
        }
    }
    else
    {
        RenderableMeshes.AddRange(meshRenderers);
    }
}
```

When only one mesh group exists, the bitmask is skipped and all meshes are shown.

## Material Groups (Skins)

Material groups work completely differently from mesh groups. They are a **positional replacement table**, not a bitmask.

### How Materials Are Initially Assigned

Each mesh's draw calls already reference a specific material by name in the mesh data itself. When the renderer creates draw calls, it reads `m_material` (or `m_pMaterial`) from each draw call definition:

```csharp
// RenderableMesh.cs:236-242
foreach (var objectDrawCall in objectDrawCalls)
{
    var materialName = objectDrawCall.GetStringProperty("m_material")
                    ?? objectDrawCall.GetStringProperty("m_pMaterial");
    // ...
    var material = renderContext.MaterialLoader.GetMaterial(materialName, shaderArguments);
}
```

The data hierarchy is:
```
Mesh → m_sceneObjects[] → m_drawCalls[] → m_material (string path)
```

### How Material Group Switching Works

All material groups have the **same number** of entries in their `m_materials` array. The first group (index 0) defines the "default" materials. Switching to another group creates a position-by-position replacement mapping:

```
default.m_materials[0] → target.m_materials[0]
default.m_materials[1] → target.m_materials[1]
default.m_materials[2] → target.m_materials[2]
```

The replacement maps from the **currently active** group to the **new** group:

```csharp
// ModelSceneNode.cs:309-347
public void SetMaterialGroup(string name)
{
    // ...
    foreach (var (Active, Replacement) in activeMaterialGroup.Materials.Zip(materialGroup.Materials))
    {
        materialTable[Active] = Replacement;
    }

    activeMaterialGroup = materialGroup;

    foreach (var mesh in meshRenderers)
    {
        mesh.ReplaceMaterials(materialTable);
    }
}
```

`ReplaceMaterials` walks every draw call and swaps any material whose name appears in the table:

```csharp
// RenderableMesh.cs:163-179
public void ReplaceMaterials(Dictionary<string, string> materialTable)
{
    foreach (var drawCall in DrawCalls)
    {
        var materialName = drawCall.Material.Material.Name;
        if (materialTable.TryGetValue(materialName, out var replacementName))
        {
            drawCall.SetNewMaterial(renderContext.MaterialLoader.GetMaterial(replacementName, ...));
        }
    }
}
```

### Concrete Example

```
m_materialGroups = [
    {
        m_name = "default"
        m_materials = [
            "bare_arm_133.vmat",         // position 0
            "glove_bloodhound_left.vmat", // position 1
            "glove_bloodhound_right.vmat" // position 2
        ]
    },
    {
        m_name = "glove_only"
        m_materials = [
            "bare_arm_hidden.vmat",       // position 0
            "glove_bloodhound_left.vmat", // position 1
            "glove_bloodhound_right.vmat" // position 2
        ]
    }
]
```

When switching from "default" to "glove_only":
- `bare_arm_133.vmat` → `bare_arm_hidden.vmat` (position 0 swap)
- `glove_bloodhound_left.vmat` → `glove_bloodhound_left.vmat` (position 1, no change)
- `glove_bloodhound_right.vmat` → `glove_bloodhound_right.vmat` (position 2, no change)

Any draw call currently using `bare_arm_133.vmat` gets remapped to `bare_arm_hidden.vmat`. The glove materials map to themselves, so those draw calls are untouched.

### Initial Material Table at Construction

When a non-default skin is selected before meshes are loaded, the `materialTable` is passed into the `RenderableMesh` constructor as `initialMaterialTable`. This means each draw call is created with the correct replacement material from the start, rather than being loaded with the default and then swapped:

```csharp
// ModelSceneNode.cs:443,458
meshRenderers.Add(new RenderableMesh(mesh, meshIndex, Scene, model, materialTable, ...));
```

```csharp
// RenderableMesh.cs:73-74
public RenderableMesh(Mesh mesh, int meshIndex, Scene scene, Model? model = null,
    Dictionary<string, string>? initialMaterialTable = null, ...)
```

## Key Source Files

| File | Role |
|---|---|
| `ValveResourceFormat/Resource/ResourceTypes/Model.cs` | Data access: `GetMeshGroups()`, `GetMaterialGroups()`, `GetDefaultMeshGroups()`, `GetReferenceMeshNamesAndLoD()` |
| `Renderer/Renderer/SceneNodes/ModelSceneNode.cs` | Rendering: mesh group activation (`SetActiveMeshGroups`), material group switching (`SetMaterialGroup`), bitmask resolution (`GetActiveMeshMaskForGroup`) |
| `Renderer/Renderer/RenderableMesh.cs` | Draw call creation (`ConfigureDrawCalls`), material replacement (`ReplaceMaterials`) |
| `ValveResourceFormat/IO/ModelExtract.ValveModel.cs` | Extraction: parses `_@` / `#&` naming convention to reconstruct body groups for modeldoc output |

## Summary

```
Mesh → Group:     bitmask per mesh, one bit per group index
Group → Default:  m_nDefaultMeshGroupMask selects which groups are on at load
Material → Mesh:  each draw call in the mesh data names its own material
Skin switching:   positional zip of active group's materials with new group's materials
```