using UnityEngine;
using UnityEditor;
using UnityEditorInternal; // Required for ReorderableList
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

// We need to use the namespace from the Behavior package to interact with its types.
using Unity.Behavior;
using UnityEngine.SceneManagement;

public class NpcCreationWizard : EditorWindow
{
    // --- Wizard UI State & Input Fields ---
    private string npcName = "NewNPC";
    private GameObject parentObject;
    private GameObject npcPrefab;
    private Vector2 scrollPosition;

    // --- Graph Creation Options ---
    private bool useExistingGraph = false;
    private ScriptableObject existingAuthoringGraph;
    private string graphFolderPath; // NEW: Specific path for graph and FSM script

    // --- FSM State Creation ---
    private List<string> fsmStates = new List<string> { "Idle", "Patrol", "Chase" };
    private ReorderableList reorderableStateList;

    // --- Role Creation & Assignment ---
    private bool createUniqueRole = true;
    private string uniqueRoleName = "Unique Role";
    [TextArea(3, 5)]
    private string uniqueRoleDescription = "A unique role for this specific NPC.";
    private string roleFolderPath; // NEW: Specific path for the unique role
    private List<NpcRoleSO> existingRoles = new List<NpcRoleSO>();
    private ReorderableList reorderableExistingRolesList;
    
    // --- Constants ---
    private const string DEFAULT_PREFAB_PATH = "Assets/Prefabs/NPCs/GenericNpc.prefab";
    private const string FALLBACK_ASSET_PATH = "Assets/NPCs/Generated";
    private const string DEFAULT_PARENT_NAME = "NPCs";

    [MenuItem("Tools/NPC/Create New NPC Wizard")]
    public static void ShowWindow()
    {
        GetWindow<NpcCreationWizard>("Create NPC");
    }

    void OnEnable()
    {
        // Set up defaults for the wizard
        SetDefaultGraphPath();
        SetDefaultRolePath();
        SetDefaultParent();
        SetDefaultPrefab();
        
        // Initialize the ReorderableLists for UI
        SetupFsmStateList();
        SetupExistingRolesList();
    }
    
    #region Reorderable List Setup
    private void SetupFsmStateList()
    {
        reorderableStateList = new ReorderableList(fsmStates, typeof(string), true, true, true, true);
        reorderableStateList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "FSM States for Enum");
        reorderableStateList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            fsmStates[index] = EditorGUI.TextField(rect, fsmStates[index]);
        };
    }
    
    private void SetupExistingRolesList()
    {
        reorderableExistingRolesList = new ReorderableList(existingRoles, typeof(NpcRoleSO), true, true, true, true);
        reorderableExistingRolesList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Assign Existing Roles");
        reorderableExistingRolesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            existingRoles[index] = (NpcRoleSO)EditorGUI.ObjectField(rect, existingRoles[index], typeof(NpcRoleSO), false);
        };
    }
    #endregion
    
    #region Default Value Setup
    private void SetDefaultGraphPath()
    {
        if (string.IsNullOrWhiteSpace(graphFolderPath))
        {
            graphFolderPath = Path.Combine(GetLevelAssetBasePath(), "Routines");
        }
    }

    private void SetDefaultRolePath()
    {
        if (string.IsNullOrWhiteSpace(roleFolderPath))
        {
            roleFolderPath = Path.Combine(GetLevelAssetBasePath(), "Roles", "Unique");
        }
    }

    private void SetDefaultParent()
    {
        if (parentObject == null) parentObject = GameObject.Find(DEFAULT_PARENT_NAME);
    }

    private void SetDefaultPrefab()
    {
        if (npcPrefab == null) npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_PREFAB_PATH);
    }
    #endregion
    
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("NPC Creation Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This wizard helps you create or configure an NPC with a Behavior Graph and Roles.", MessageType.Info);
        
        // --- Common NPC Fields ---
        EditorGUILayout.LabelField("Core NPC Settings", EditorStyles.boldLabel);
        npcName = EditorGUILayout.TextField("NPC Name", npcName);
        parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object (Optional)", parentObject, typeof(GameObject), true);
        npcPrefab = (GameObject)EditorGUILayout.ObjectField("NPC Prefab", npcPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();

        // --- Role Management Section ---
        EditorGUILayout.LabelField("Role Configuration", EditorStyles.boldLabel);
        createUniqueRole = EditorGUILayout.Toggle("Create Unique Role", createUniqueRole);
        if (createUniqueRole)
        {
            EditorGUI.indentLevel++;
            uniqueRoleName = EditorGUILayout.TextField("Unique Role Name", uniqueRoleName);
            EditorGUILayout.LabelField("Unique Role Description");
            uniqueRoleDescription = EditorGUILayout.TextArea(uniqueRoleDescription, GUILayout.Height(60));
            roleFolderPath = EditorGUILayout.TextField("Role Folder Path", roleFolderPath); // NEW
            EditorGUI.indentLevel--;
        }
        
        // Always show the list for existing roles
        reorderableExistingRolesList.DoLayoutList();

        EditorGUILayout.Space();

        // --- Graph Management Section ---
        EditorGUILayout.LabelField("Behavior Graph Configuration", EditorStyles.boldLabel);
        useExistingGraph = EditorGUILayout.Toggle("Use Existing Graph", useExistingGraph);
        if (useExistingGraph)
        {
            existingAuthoringGraph = (ScriptableObject)EditorGUILayout.ObjectField("Authoring Graph", existingAuthoringGraph, typeof(ScriptableObject), false);
        }
        else
        {
            graphFolderPath = EditorGUILayout.TextField("Graph Folder Path", graphFolderPath); // UPDATED
            reorderableStateList.DoLayoutList();
        }

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Create NPC"))
        {
            if (ValidateInputs())
            {
                CreateNpcAssets();
            }
        }
        
        EditorGUILayout.EndScrollView();
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(npcName))
        {
            EditorUtility.DisplayDialog("Error", "NPC Name cannot be empty.", "OK"); return false;
        }
        if (npcPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "You must select an NPC Prefab.", "OK"); return false;
        }
        if (createUniqueRole)
        {
             if (string.IsNullOrWhiteSpace(uniqueRoleName) || string.IsNullOrWhiteSpace(uniqueRoleDescription))
             {
                 EditorUtility.DisplayDialog("Error", "Unique Role Name and Description cannot be empty.", "OK"); return false;
             }
             if (string.IsNullOrWhiteSpace(roleFolderPath)) // NEW
             {
                 EditorUtility.DisplayDialog("Error", "Role Folder Path cannot be empty when creating a unique role.", "OK"); return false;
             }
        }
        if (useExistingGraph)
        {
            if (existingAuthoringGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "You must assign an existing Authoring Graph.", "OK"); return false;
            }
            if (existingAuthoringGraph.GetType().Name != "BehaviorAuthoringGraph")
            {
                EditorUtility.DisplayDialog("Error", "The assigned asset is not a BehaviorAuthoringGraph.", "OK"); return false;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(graphFolderPath)) // UPDATED
            {
                EditorUtility.DisplayDialog("Error", "Graph Folder Path cannot be empty when creating a new graph.", "OK"); return false;
            }
        }
        return true;
    }

    private void CreateNpcAssets()
    {
        // --- 1. Instantiate Prefab ---
        GameObject npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab);
        Undo.RegisterCreatedObjectUndo(npcInstance, "Create " + npcName);
        npcInstance.name = npcName;
        if (parentObject != null) npcInstance.transform.SetParent(parentObject.transform, worldPositionStays: false);

        string graphAssetPath = "";
        NpcRoleSO uniqueRole = null;
        
        // --- 2. Create New Assets (if required) ---
        if (createUniqueRole)
        {
            if (!Directory.Exists(roleFolderPath)) Directory.CreateDirectory(roleFolderPath); // UPDATED

            uniqueRole = ScriptableObject.CreateInstance<NpcRoleSO>();
            uniqueRole.RoleName = uniqueRoleName;
            uniqueRole.RoleDescription = uniqueRoleDescription;
            uniqueRole.RoleWeight = 10f; // High weight for unique roles
            uniqueRole.RoleType = RoleType.UniqueIdentifier;

            string roleAssetName = $"R_{npcName}.asset";
            string rolePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(roleFolderPath, roleAssetName)); // UPDATED
            AssetDatabase.CreateAsset(uniqueRole, rolePath);
        }

        if (useExistingGraph)
        {
            graphAssetPath = AssetDatabase.GetAssetPath(existingAuthoringGraph);
        }
        else // Create new graph
        {
            if (!Directory.Exists(graphFolderPath)) Directory.CreateDirectory(graphFolderPath); // UPDATED
            
            // Create BehaviorAuthoringGraph Asset
            ScriptableObject authoringGraphSO = ScriptableObject.CreateInstance("Unity.Behavior.BehaviorAuthoringGraph");
            
            string graphAssetName = $"{npcName}Routine.asset";
            string fullGraphAssetPath = Path.Combine(graphFolderPath, graphAssetName); // UPDATED
            string uniqueGraphPath = AssetDatabase.GenerateUniqueAssetPath(fullGraphAssetPath);
            AssetDatabase.CreateAsset(authoringGraphSO, uniqueGraphPath);
            graphAssetPath = uniqueGraphPath; // Use the unique path

            // Create FSM Script
            string fsmScriptName = $"{npcName}FSMStates";
            string scriptPath = Path.Combine(graphFolderPath, $"{fsmScriptName}.cs"); // UPDATED
            string scriptContent = GenerateFsmScriptContent(fsmScriptName, fsmStates);
            File.WriteAllText(scriptPath, scriptContent);
        }
        
        // --- 3. Save, Refresh, and Assign ---
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Assign Roles
        AssignRolesToNpc(npcInstance, uniqueRole, existingRoles);
        
        // Assign Graph
        if (!SetupAndAssignRoutineGraph(npcInstance, graphAssetPath))
        {
            Debug.LogError($"Failed to assign routine graph to '{npcName}'. Please assign it manually.");
        }
        
        // --- 4. Finalize ---
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = npcInstance;
        this.Close();
        Debug.Log($"Successfully created and configured NPC '{npcName}'.");
    }
    
    private void AssignRolesToNpc(GameObject npcInstance, NpcRoleSO uniqueRole, List<NpcRoleSO> otherRoles)
    {
        var npcIdentity = npcInstance.GetComponent<NPCIdentity>();
        if (npcIdentity == null)
        {
            Debug.LogError($"The NPC Prefab '{npcPrefab.name}' is missing the 'NPCIdentity' component. Cannot assign roles.", npcPrefab);
            return;
        }

        // Add the newly created unique role, if it exists
        if (uniqueRole != null)
        {
            npcIdentity.AddDefaultRole(uniqueRole);
        }

        // Add all roles from the list
        foreach (var role in otherRoles)
        {
            if (role != null)
            {
                npcIdentity.AddDefaultRole(role);
            }
        }
        
        // Mark the component as dirty so the changes are saved
        EditorUtility.SetDirty(npcIdentity);
    }

    private bool SetupAndAssignRoutineGraph(GameObject npcInstance, string graphAssetPath)
    {
        var behaviorGraphAgent = npcInstance.GetComponent<SaveableBehaviorGraphAgent>();
        if (behaviorGraphAgent == null) return false;

        var authoringGraphSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(graphAssetPath);
        var authoringGraphInternal = authoringGraphSO as BehaviorAuthoringGraph;
        if (authoringGraphInternal == null) return false;

        BehaviorGraph runtimeGraph = BehaviorAuthoringGraph.GetOrCreateGraph(authoringGraphInternal);
        if (runtimeGraph == null) return false;
        
        if (runtimeGraph.RootGraph == null)
        {
            try
            {
                MethodInfo buildMethod = authoringGraphInternal.GetType().GetMethod("BuildRuntimeGraph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (buildMethod == null) return false;
                
                buildMethod.Invoke(authoringGraphInternal, new object[] { true });
                EditorUtility.SetDirty(authoringGraphInternal);
                AssetDatabase.SaveAssetIfDirty(authoringGraphInternal);
            }
            catch (System.Exception) { return false; }
        }
        
        behaviorGraphAgent.Graph = runtimeGraph;
        EditorUtility.SetDirty(behaviorGraphAgent);
        EditorUtility.SetDirty(npcInstance);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(npcInstance.scene);

        return true;
    }

    private string GetLevelAssetBasePath()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = currentScene.name;

        if (!string.IsNullOrEmpty(sceneName) && sceneName.EndsWith("Level"))
        {
            string uniqueName = sceneName.Substring(0, sceneName.Length - "Level".Length);
            return $"Assets/Resources/SOs/Level_{uniqueName}";
        }
        else
        {
            return FALLBACK_ASSET_PATH;
        }
    }
    
    private string GenerateFsmScriptContent(string enumName, List<string> states)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Unity.Behavior;");
        sb.AppendLine();
        sb.AppendLine("[BlackboardEnum]");
        sb.AppendLine($"public enum {enumName}");
        sb.AppendLine("{");
        foreach (var state in states)
        {
            string formattedState = state.Trim().Replace(" ", "");
            if (!string.IsNullOrEmpty(formattedState)) sb.AppendLine($"    {formattedState},");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}

// using UnityEngine;
// using UnityEditor;
// using UnityEditorInternal; // Required for ReorderableList
// using System.IO;
// using System.Reflection;
// using System.Collections.Generic;
// using System.Text;
//
// // We need to use the namespace from the Behavior package to interact with its types.
// using Unity.Behavior;
// using UnityEngine.SceneManagement;
//
// public class NpcCreationWizard : EditorWindow
// {
//     // --- Wizard UI State & Input Fields ---
//     private string npcName = "NewNPC";
//     private GameObject parentObject;
//     private GameObject npcPrefab;
//     private Vector2 scrollPosition;
//
//     // --- Graph Creation Options ---
//     private bool useExistingGraph = false;
//     private ScriptableObject existingAuthoringGraph;
//     private string assetFolderPath;
//
//     // --- FSM State Creation ---
//     private List<string> fsmStates = new List<string> { "Idle", "Patrol", "Chase" };
//     private ReorderableList reorderableStateList;
//
//     // --- NEW: Role Creation & Assignment ---
//     private bool createUniqueRole = true;
//     private string uniqueRoleName = "Unique Role";
//     [TextArea(3, 5)]
//     private string uniqueRoleDescription = "A unique role for this specific NPC.";
//     private List<NpcRoleSO> existingRoles = new List<NpcRoleSO>();
//     private ReorderableList reorderableExistingRolesList;
//     
//     // --- Constants ---
//     private const string DEFAULT_PREFAB_PATH = "Assets/Prefabs/NPCs/GenericNpc.prefab";
//     private const string FALLBACK_ASSET_PATH = "Assets/NPCs/Generated";
//     private const string DEFAULT_PARENT_NAME = "NPCs";
//
//     [MenuItem("Tools/NPC/Create New NPC Wizard")]
//     public static void ShowWindow()
//     {
//         GetWindow<NpcCreationWizard>("Create NPC");
//     }
//
//     void OnEnable()
//     {
//         // Set up defaults for the wizard
//         SetDefaultAssetPath();
//         SetDefaultParent();
//         SetDefaultPrefab();
//         
//         // Initialize the ReorderableLists for UI
//         SetupFsmStateList();
//         SetupExistingRolesList();
//     }
//     
//     #region Reorderable List Setup
//     private void SetupFsmStateList()
//     {
//         reorderableStateList = new ReorderableList(fsmStates, typeof(string), true, true, true, true);
//         reorderableStateList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "FSM States for Enum");
//         reorderableStateList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
//             rect.y += 2;
//             rect.height = EditorGUIUtility.singleLineHeight;
//             fsmStates[index] = EditorGUI.TextField(rect, fsmStates[index]);
//         };
//     }
//     
//     private void SetupExistingRolesList()
//     {
//         reorderableExistingRolesList = new ReorderableList(existingRoles, typeof(NpcRoleSO), true, true, true, true);
//         reorderableExistingRolesList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Assign Existing Roles");
//         reorderableExistingRolesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
//             rect.y += 2;
//             rect.height = EditorGUIUtility.singleLineHeight;
//             existingRoles[index] = (NpcRoleSO)EditorGUI.ObjectField(rect, existingRoles[index], typeof(NpcRoleSO), false);
//         };
//     }
//     #endregion
//     
//     #region Default Value Setup
//     private void SetDefaultAssetPath()
//     {
//         if (!string.IsNullOrWhiteSpace(assetFolderPath)) return;
//         assetFolderPath = GetLevelAssetBasePath();
//     }
//
//     private void SetDefaultParent()
//     {
//         if (parentObject == null) parentObject = GameObject.Find(DEFAULT_PARENT_NAME);
//     }
//
//     private void SetDefaultPrefab()
//     {
//         if (npcPrefab == null) npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_PREFAB_PATH);
//     }
//     #endregion
//     
//     void OnGUI()
//     {
//         scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
//
//         GUILayout.Label("NPC Creation Wizard", EditorStyles.boldLabel);
//         EditorGUILayout.HelpBox("This wizard helps you create or configure an NPC with a Behavior Graph and Roles.", MessageType.Info);
//         
//         // --- Common NPC Fields ---
//         EditorGUILayout.LabelField("Core NPC Settings", EditorStyles.boldLabel);
//         npcName = EditorGUILayout.TextField("NPC Name", npcName);
//         parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object (Optional)", parentObject, typeof(GameObject), true);
//         npcPrefab = (GameObject)EditorGUILayout.ObjectField("NPC Prefab", npcPrefab, typeof(GameObject), false);
//
//         EditorGUILayout.Space();
//
//         // --- Role Management Section ---
//         EditorGUILayout.LabelField("Role Configuration", EditorStyles.boldLabel);
//         createUniqueRole = EditorGUILayout.Toggle("Create Unique Role", createUniqueRole);
//         if (createUniqueRole)
//         {
//             EditorGUI.indentLevel++;
//             uniqueRoleName = EditorGUILayout.TextField("Unique Role Name", uniqueRoleName);
//             EditorGUILayout.LabelField("Unique Role Description");
//             uniqueRoleDescription = EditorGUILayout.TextArea(uniqueRoleDescription, GUILayout.Height(60));
//             EditorGUI.indentLevel--;
//         }
//         
//         // Always show the list for existing roles
//         reorderableExistingRolesList.DoLayoutList();
//
//         EditorGUILayout.Space();
//
//         // --- Graph Management Section ---
//         EditorGUILayout.LabelField("Behavior Graph Configuration", EditorStyles.boldLabel);
//         useExistingGraph = EditorGUILayout.Toggle("Use Existing Graph", useExistingGraph);
//         if (useExistingGraph)
//         {
//             existingAuthoringGraph = (ScriptableObject)EditorGUILayout.ObjectField("Authoring Graph", existingAuthoringGraph, typeof(ScriptableObject), false);
//         }
//         else
//         {
//             assetFolderPath = EditorGUILayout.TextField("Asset Folder Path", assetFolderPath);
//             reorderableStateList.DoLayoutList();
//         }
//
//         EditorGUILayout.Space(20);
//
//         if (GUILayout.Button("Create NPC"))
//         {
//             if (ValidateInputs())
//             {
//                 CreateNpcAssets();
//             }
//         }
//         
//         EditorGUILayout.EndScrollView();
//     }
//
//     private bool ValidateInputs()
//     {
//         if (string.IsNullOrWhiteSpace(npcName))
//         {
//             EditorUtility.DisplayDialog("Error", "NPC Name cannot be empty.", "OK"); return false;
//         }
//         if (npcPrefab == null)
//         {
//             EditorUtility.DisplayDialog("Error", "You must select an NPC Prefab.", "OK"); return false;
//         }
//         if (createUniqueRole && (string.IsNullOrWhiteSpace(uniqueRoleName) || string.IsNullOrWhiteSpace(uniqueRoleDescription)))
//         {
//              EditorUtility.DisplayDialog("Error", "Unique Role Name and Description cannot be empty.", "OK"); return false;
//         }
//         if (useExistingGraph)
//         {
//             if (existingAuthoringGraph == null)
//             {
//                 EditorUtility.DisplayDialog("Error", "You must assign an existing Authoring Graph.", "OK"); return false;
//             }
//             if (existingAuthoringGraph.GetType().Name != "BehaviorAuthoringGraph")
//             {
//                 EditorUtility.DisplayDialog("Error", "The assigned asset is not a BehaviorAuthoringGraph.", "OK"); return false;
//             }
//         }
//         else
//         {
//             if (string.IsNullOrWhiteSpace(assetFolderPath))
//             {
//                 EditorUtility.DisplayDialog("Error", "Asset Folder Path cannot be empty.", "OK"); return false;
//             }
//         }
//         return true;
//     }
//
//     private void CreateNpcAssets()
//     {
//         // --- 1. Instantiate Prefab ---
//         GameObject npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab);
//         Undo.RegisterCreatedObjectUndo(npcInstance, "Create " + npcName);
//         npcInstance.name = npcName;
//         if (parentObject != null) npcInstance.transform.SetParent(parentObject.transform, worldPositionStays: false);
//
//         string graphAssetPath = "";
//         NpcRoleSO uniqueRole = null;
//         
//         // --- 2. Create New Assets (if required) ---
//         if (createUniqueRole)
//         {
//             string rolesBasePath = GetLevelAssetBasePath();
//             string uniqueRolesPath = Path.Combine(rolesBasePath, "Roles", "Unique");
//             if (!Directory.Exists(uniqueRolesPath)) Directory.CreateDirectory(uniqueRolesPath);
//
//             uniqueRole = ScriptableObject.CreateInstance<NpcRoleSO>();
//             uniqueRole.RoleName = uniqueRoleName;
//             uniqueRole.RoleDescription = uniqueRoleDescription;
//             uniqueRole.RoleWeight = 10f; // High weight for unique roles
//             uniqueRole.RoleType = RoleType.UniqueIdentifier;
//
//             string roleAssetName = $"R_{npcName}.asset";
//             string rolePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(uniqueRolesPath, roleAssetName));
//             AssetDatabase.CreateAsset(uniqueRole, rolePath);
//         }
//
//         if (useExistingGraph)
//         {
//             graphAssetPath = AssetDatabase.GetAssetPath(existingAuthoringGraph);
//         }
//         else // Create new graph
//         {
//             if (!Directory.Exists(assetFolderPath)) Directory.CreateDirectory(assetFolderPath);
//             
//             // Create BehaviorAuthoringGraph Asset
//             ScriptableObject authoringGraphSO = ScriptableObject.CreateInstance("Unity.Behavior.BehaviorAuthoringGraph");
//             
//             string graphAssetDir = Path.Combine(assetFolderPath, "Routines");
//             string graphAssetName = $"{npcName}Routine.asset";
//             graphAssetPath = Path.Combine(graphAssetDir, graphAssetName);
//             string uniqueGraphPath = AssetDatabase.GenerateUniqueAssetPath(graphAssetPath);
//             AssetDatabase.CreateAsset(authoringGraphSO, uniqueGraphPath);
//             graphAssetPath = uniqueGraphPath; // Use the unique path
//
//             // Create FSM Script
//             string fsmScriptName = $"{npcName}FSMStates";
//             string scriptPath = Path.Combine(graphAssetDir, $"{fsmScriptName}.cs");
//             string scriptContent = GenerateFsmScriptContent(fsmScriptName, fsmStates);
//             File.WriteAllText(scriptPath, scriptContent);
//         }
//         
//         // --- 3. Save, Refresh, and Assign ---
//         AssetDatabase.SaveAssets();
//         AssetDatabase.Refresh();
//
//         // Assign Roles
//         AssignRolesToNpc(npcInstance, uniqueRole, existingRoles);
//         
//         // Assign Graph
//         if (!SetupAndAssignRoutineGraph(npcInstance, graphAssetPath))
//         {
//             Debug.LogError($"Failed to assign routine graph to '{npcName}'. Please assign it manually.");
//         }
//         
//         // --- 4. Finalize ---
//         EditorUtility.FocusProjectWindow();
//         Selection.activeObject = npcInstance;
//         this.Close();
//         Debug.Log($"Successfully created and configured NPC '{npcName}'.");
//     }
//     
//     private void AssignRolesToNpc(GameObject npcInstance, NpcRoleSO uniqueRole, List<NpcRoleSO> otherRoles)
//     {
//         var npcIdentity = npcInstance.GetComponent<NPCIdentity>();
//         if (npcIdentity == null)
//         {
//             Debug.LogError($"The NPC Prefab '{npcPrefab.name}' is missing the 'NPCIdentity' component. Cannot assign roles.", npcPrefab);
//             return;
//         }
//
//         // Add the newly created unique role, if it exists
//         if (uniqueRole != null)
//         {
//             npcIdentity.AddDefaultRole(uniqueRole);
//         }
//
//         // Add all roles from the list
//         foreach (var role in otherRoles)
//         {
//             if (role != null)
//             {
//                 npcIdentity.AddDefaultRole(role);
//             }
//         }
//         
//         // Mark the component as dirty so the changes are saved
//         EditorUtility.SetDirty(npcIdentity);
//     }
//
//     private bool SetupAndAssignRoutineGraph(GameObject npcInstance, string graphAssetPath)
//     {
//         var behaviorGraphAgent = npcInstance.GetComponent<SaveableBehaviorGraphAgent>();
//         if (behaviorGraphAgent == null) return false;
//
//         var authoringGraphSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(graphAssetPath);
//         var authoringGraphInternal = authoringGraphSO as BehaviorAuthoringGraph;
//         if (authoringGraphInternal == null) return false;
//
//         BehaviorGraph runtimeGraph = BehaviorAuthoringGraph.GetOrCreateGraph(authoringGraphInternal);
//         if (runtimeGraph == null) return false;
//         
//         if (runtimeGraph.RootGraph == null)
//         {
//             try
//             {
//                 MethodInfo buildMethod = authoringGraphInternal.GetType().GetMethod("BuildRuntimeGraph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//                 if (buildMethod == null) return false;
//                 
//                 buildMethod.Invoke(authoringGraphInternal, new object[] { true });
//                 EditorUtility.SetDirty(authoringGraphInternal);
//                 AssetDatabase.SaveAssetIfDirty(authoringGraphInternal);
//             }
//             catch (System.Exception) { return false; }
//         }
//         
//         behaviorGraphAgent.Graph = runtimeGraph;
//         EditorUtility.SetDirty(behaviorGraphAgent);
//         EditorUtility.SetDirty(npcInstance);
//         UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(npcInstance.scene);
//
//         return true;
//     }
//
//     private string GetLevelAssetBasePath()
//     {
//         Scene currentScene = SceneManager.GetActiveScene();
//         string sceneName = currentScene.name;
//
//         if (!string.IsNullOrEmpty(sceneName) && sceneName.EndsWith("Level"))
//         {
//             string uniqueName = sceneName.Substring(0, sceneName.Length - "Level".Length);
//             return $"Assets/Resources/SOs/Level_{uniqueName}";
//         }
//         else
//         {
//             return FALLBACK_ASSET_PATH;
//         }
//     }
//     
//     private string GenerateFsmScriptContent(string enumName, List<string> states)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("using Unity.Behavior;");
//         sb.AppendLine();
//         sb.AppendLine("[BlackboardEnum]");
//         sb.AppendLine($"public enum {enumName}");
//         sb.AppendLine("{");
//         foreach (var state in states)
//         {
//             string formattedState = state.Trim().Replace(" ", "");
//             if (!string.IsNullOrEmpty(formattedState)) sb.AppendLine($"    {formattedState},");
//         }
//         sb.AppendLine("}");
//         return sb.ToString();
//     }
// }