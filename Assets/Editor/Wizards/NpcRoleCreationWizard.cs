using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class NpcRoleCreationWizard : EditorWindow
{
    // --- UI State & Input Fields ---
    private string roleName = "New Role";
    [TextArea(3, 5)]
    private string roleDescription = "A new role for an NPC.";
    private float roleWeight = 1.0f;
    private RoleType roleType = RoleType.Default;
    private bool isSticky = false;
    private string baseFolderPath;

    // --- Defaults & Mappings ---
    private static readonly Dictionary<RoleType, float> DEFAULT_ROLE_WEIGHTS = new Dictionary<RoleType, float>
    {
        { RoleType.Default, 1.0f },
        { RoleType.Job, 5.0f },
        { RoleType.UniqueIdentifier, 10.0f },
        { RoleType.ZoneAccess, 0.1f },
        { RoleType.Skill, 2.5f },
        { RoleType.ItemAbility, 2.0f },
        { RoleType.StoryFlag, -1.0f },
        { RoleType.Knowledge, -1.0f }
    };

    private static readonly Dictionary<RoleType, string> ROLE_TYPE_SUBFOLDERS = new Dictionary<RoleType, string>
    {
        { RoleType.Default, "Default" },
        { RoleType.Job, "Job" },
        { RoleType.UniqueIdentifier, "Unique" },
        { RoleType.ZoneAccess, "ZoneAccess" },
        { RoleType.Skill, "Skill" },
        { RoleType.ItemAbility, "ItemAbility" },
        { RoleType.StoryFlag, "StoryFlag" },
        { RoleType.Knowledge, "Knowledge" }
    };

    // NEW: Default sticky settings based on RoleType
    private static readonly Dictionary<RoleType, bool> DEFAULT_STICKY_SETTINGS = new Dictionary<RoleType, bool>
    {
        { RoleType.Default, false },
        { RoleType.Job, false },
        { RoleType.UniqueIdentifier, false },
        { RoleType.ZoneAccess, false },
        { RoleType.Skill, true }, // Skills are sticky by default
        { RoleType.ItemAbility, false },
        { RoleType.StoryFlag, false },
        { RoleType.Knowledge, true } // Knowledge is sticky by default
    };

    // --- Constants ---
    private const string FALLBACK_ASSET_PATH = "Assets/NPCs/Generated/Roles";

    [MenuItem("Tools/NPC/Create New Role Asset Wizard")]
    public static void ShowWindow()
    {
        GetWindow<NpcRoleCreationWizard>("Create NPC Role");
    }

    void OnEnable()
    {
        // Set default values when the window is opened
        SetDefaultBasePath();
        UpdateDefaultsForRoleType();
    }

    private void SetDefaultBasePath()
    {
        if (!string.IsNullOrWhiteSpace(baseFolderPath)) return;

        // We can reuse the level-aware path logic from the other wizard
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = currentScene.name;

        if (!string.IsNullOrEmpty(sceneName) && sceneName.EndsWith("Level"))
        {
            string uniqueName = sceneName.Substring(0, sceneName.Length - "Level".Length);
            baseFolderPath = $"Assets/Resources/SOs/Level_{uniqueName}/Roles";
        }
        else
        {
            baseFolderPath = FALLBACK_ASSET_PATH;
        }
    }

    void OnGUI()
    {
        GUILayout.Label("NPC Role Creation Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This wizard helps you create a new NpcRoleSO asset. The final save location will be determined by the 'Base Folder Path' and the selected 'Role Type'.", MessageType.Info);
        
        EditorGUILayout.Space();

        // --- Role Property Fields ---
        roleName = EditorGUILayout.TextField("Role Name", roleName);
        
        EditorGUILayout.LabelField("Role Description");
        roleDescription = EditorGUILayout.TextArea(roleDescription, GUILayout.Height(60));

        // --- Role Type and dependent fields ---
        EditorGUI.BeginChangeCheck();
        roleType = (RoleType)EditorGUILayout.EnumPopup("Role Type", roleType);
        // If the role type was changed by the user, update the default weight and sticky flag
        if (EditorGUI.EndChangeCheck())
        {
            UpdateDefaultsForRoleType();
        }

        roleWeight = EditorGUILayout.FloatField("Role Weight", roleWeight);
        isSticky = EditorGUILayout.Toggle("Is Sticky", isSticky);

        EditorGUILayout.Space();

        // --- Path Configuration ---
        EditorGUILayout.LabelField("Asset Location", EditorStyles.boldLabel);
        baseFolderPath = EditorGUILayout.TextField("Base Folder Path", baseFolderPath);

        // Display a read-only field to show the user the final calculated path
        GUI.enabled = false;
        string finalPathPreview = Path.Combine(baseFolderPath, ROLE_TYPE_SUBFOLDERS[roleType]);
        EditorGUILayout.TextField("Final Save Directory", finalPathPreview);
        GUI.enabled = true;

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Create Role Asset"))
        {
            if (ValidateInputs())
            {
                CreateRoleAsset();
            }
        }
    }

    private void UpdateDefaultsForRoleType()
    {
        // Set the weight to the default value for the selected type
        if (DEFAULT_ROLE_WEIGHTS.TryGetValue(roleType, out float defaultWeight))
        {
            roleWeight = defaultWeight;
        }

        // Set the sticky flag to the default value for the selected type
        if (DEFAULT_STICKY_SETTINGS.TryGetValue(roleType, out bool defaultSticky))
        {
            isSticky = defaultSticky;
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            EditorUtility.DisplayDialog("Error", "Role Name cannot be empty.", "OK");
            return false;
        }
        if (string.IsNullOrWhiteSpace(baseFolderPath))
        {
            EditorUtility.DisplayDialog("Error", "Base Folder Path cannot be empty.", "OK");
            return false;
        }
        return true;
    }

    private void CreateRoleAsset()
    {
        // 1. Determine the final directory based on the base path and role type
        string subfolder = ROLE_TYPE_SUBFOLDERS[roleType];
        string finalDirectory = Path.Combine(baseFolderPath, subfolder);

        // 2. Ensure the directory exists
        if (!Directory.Exists(finalDirectory))
        {
            Directory.CreateDirectory(finalDirectory);
        }

        // 3. Create the NpcRoleSO instance and populate it
        NpcRoleSO newRole = ScriptableObject.CreateInstance<NpcRoleSO>();
        newRole.RoleName = roleName;
        newRole.RoleDescription = roleDescription;
        newRole.RoleWeight = roleWeight;
        newRole.RoleType = roleType;
        newRole.Sticky = isSticky;
        
        // 4. Generate a unique path and create the asset
        string assetName = $"R_{roleName.Replace(" ", "")}.asset";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(finalDirectory, assetName));
        
        AssetDatabase.CreateAsset(newRole, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5. Finalize and give user feedback
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newRole;
        Debug.Log($"Successfully created NPC Role '{roleName}' at: {assetPath}");
        this.Close();
    }
}