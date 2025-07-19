using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine.SceneManagement;

public class InteractableDefinitionWizard : EditorWindow
{
    // --- UI State & Input Fields ---
    private string itemName = "New Item";
    private string uniqueName = "NewItem";
    [TextArea(3, 5)]
    private string itemDescription = "A new item.";
    
    // --- Path Management ---
    private string baseAssetPath;
    private string[] subfolderOptions = new string[0];
    private int selectedSubfolderIndex = 0;
    private string newSubfolderName = "";

    // --- Constants ---
    private const string FALLBACK_ASSET_PATH = "Assets/NPCs/Generated/Interactions";

    [MenuItem("Tools/Interactables/Create Interactable Definition Wizard")]
    public static void ShowWindow()
    {
        GetWindow<InteractableDefinitionWizard>("Create Interactable Definition");
    }

    void OnEnable()
    {
        SetDefaultBasePath();
        RefreshSubfolderList();
    }

    private void SetDefaultBasePath()
    {
        // Reuse the scene-aware logic, but point to a different final directory
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = currentScene.name;

        if (!string.IsNullOrEmpty(sceneName) && sceneName.EndsWith("Level"))
        {
            string uniqueName = sceneName.Substring(0, sceneName.Length - "Level".Length);
            // The only change is this final subfolder name
            baseAssetPath = $"Assets/Resources/SOs/Level_{uniqueName}/Interactions";
        }
        else
        {
            baseAssetPath = FALLBACK_ASSET_PATH;
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Interactable Definition Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This wizard helps you create a new InteractableDefinitionSO asset. These define the basic properties of an item.", MessageType.Info);
        
        EditorGUILayout.Space();

        // --- Definition Fields ---
        EditorGUILayout.LabelField("Definition Properties", EditorStyles.boldLabel);
        itemName = EditorGUILayout.TextField("Item Name (UI)", itemName);

        EditorGUILayout.BeginHorizontal();
        uniqueName = EditorGUILayout.TextField(new GUIContent("Unique Name (for filename)", "Used for the asset filename (IDef_[UniqueName].asset). No spaces or special characters."), uniqueName);
        if (GUILayout.Button(new GUIContent("<<", "Generate from Item Name"), GUILayout.Width(30)))
        {
            uniqueName = SanitizeForFileName(itemName);
            GUI.FocusControl(null); // Deselect field to show the change
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Item Description (UI)");
        itemDescription = EditorGUILayout.TextArea(itemDescription, GUILayout.Height(60));

        // --- Path Configuration ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Asset Location", EditorStyles.boldLabel);
        baseAssetPath = EditorGUILayout.TextField("Base Definitions Path", baseAssetPath);

        if (subfolderOptions.Length > 0)
        {
            selectedSubfolderIndex = EditorGUILayout.Popup("Category / Subfolder", selectedSubfolderIndex, subfolderOptions);
        }
        else
        {
            EditorGUILayout.HelpBox("No subfolders found. Create one below.", MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();
        newSubfolderName = EditorGUILayout.TextField("New Category Name", newSubfolderName);
        if (GUILayout.Button("Create", GUILayout.Width(60)))
        {
            CreateNewSubfolder();
        }
        EditorGUILayout.EndHorizontal();

        // --- Final Path Preview ---
        GUI.enabled = false;
        string finalPathPreview = "N/A";
        if (subfolderOptions.Length > 0 && selectedSubfolderIndex < subfolderOptions.Length)
        {
            finalPathPreview = Path.Combine(baseAssetPath, subfolderOptions[selectedSubfolderIndex]);
        }
        EditorGUILayout.TextField("Final Save Directory", finalPathPreview);
        GUI.enabled = true;
        
        EditorGUILayout.Space(20);

        if (GUILayout.Button("Create Interactable Definition Asset", GUILayout.Height(30)))
        {
            if (ValidateInputs())
            {
                CreateAsset();
            }
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            EditorUtility.DisplayDialog("Error", "Item Name cannot be empty.", "OK");
            return false;
        }
        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            EditorUtility.DisplayDialog("Error", "Unique Name cannot be empty.", "OK");
            return false;
        }
        if (uniqueName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            EditorUtility.DisplayDialog("Error", $"Unique Name '{uniqueName}' contains invalid characters.", "OK");
            return false;
        }
        if (subfolderOptions.Length == 0 || selectedSubfolderIndex >= subfolderOptions.Length)
        {
            EditorUtility.DisplayDialog("Error", "A valid category/subfolder must be selected.", "OK");
            return false;
        }
        return true;
    }

    private void CreateAsset()
    {
        // 1. Determine final directory
        string selectedSubfolder = subfolderOptions[selectedSubfolderIndex];
        string finalDirectory = Path.Combine(baseAssetPath, selectedSubfolder);
        
        // 2. Ensure the directory exists
        if (!Directory.Exists(finalDirectory))
        {
            Directory.CreateDirectory(finalDirectory);
        }

        // 3. Create the SO instance and populate it
        var newItemDef = ScriptableObject.CreateInstance<InteractableDefinitionSO>();
        newItemDef.Name = itemName;
        newItemDef.Description = itemDescription;
        
        // 4. Generate a unique path and create the asset
        string finalUniqueName = SanitizeForFileName(uniqueName);
        // Use "IDef_" prefix for Interactable Definition
        string assetName = $"IDef_{finalUniqueName}.asset";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(finalDirectory, assetName));
        
        AssetDatabase.CreateAsset(newItemDef, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5. Finalize
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newItemDef;
        Debug.Log($"Successfully created Interactable Definition '{itemName}' at: {assetPath}");
        this.Close();
    }
    
    #region Helper Methods

    /// <summary>
    /// Scans the base path for existing subdirectories and updates the dropdown.
    /// </summary>
    private void RefreshSubfolderList()
    {
        if (string.IsNullOrEmpty(baseAssetPath)) return;

        if (!Directory.Exists(baseAssetPath))
        {
            Directory.CreateDirectory(baseAssetPath);
        }

        var directories = Directory.GetDirectories(baseAssetPath)
                                   .Select(Path.GetFileName)
                                   .ToList();

        if (directories.Count == 0)
        {
            string defaultFolder = Path.Combine(baseAssetPath, "General");
            Directory.CreateDirectory(defaultFolder);
            directories.Add("General");
        }

        subfolderOptions = directories.ToArray();
        selectedSubfolderIndex = 0;
    }

    private void CreateNewSubfolder()
    {
        if (string.IsNullOrWhiteSpace(newSubfolderName))
        {
            EditorUtility.DisplayDialog("Error", "New category name cannot be empty.", "OK");
            return;
        }

        string sanitizedName = string.Join("_", newSubfolderName.Split(Path.GetInvalidFileNameChars()));
        string newPath = Path.Combine(baseAssetPath, sanitizedName);

        if (Directory.Exists(newPath))
        {
            EditorUtility.DisplayDialog("Error", $"A category named '{sanitizedName}' already exists.", "OK");
            return;
        }

        Directory.CreateDirectory(newPath);
        Debug.Log($"Created new directory: {newPath}");
        newSubfolderName = ""; 
        
        RefreshSubfolderList();
        int newIndex = System.Array.IndexOf(subfolderOptions, sanitizedName);
        if (newIndex != -1)
        {
            selectedSubfolderIndex = newIndex;
        }
        
        GUI.FocusControl(null);
    }
    
    /// <summary>
    /// Creates a file-safe, PascalCase name from a string.
    /// It removes invalid characters, then converts "some name" to "SomeName"
    /// without altering existing capitalization like in "IronSword".
    /// </summary>
    private string SanitizeForFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Unnamed";
        }

        // 1. Remove characters that are not letters, numbers, or whitespace.
        // This cleans the string of symbols like apostrophes, hyphens, etc.
        // \w includes letters, numbers, and underscore. \s is whitespace.
        string sanitized = Regex.Replace(input, @"[^\w\s]", "");

        // 2. Find any whitespace sequence followed by a character and capitalize that character.
        // The MatchEvaluator `m => m.Groups[1].Value.ToUpper()` takes the matched character
        // (captured in group 1 by the parentheses) and converts it to uppercase.
        // This turns "iron sword" into "ironSword" but leaves "IronSword" untouched.
        sanitized = Regex.Replace(sanitized, @"\s+(.)", m => m.Groups[1].Value.ToUpper());

        // 3. Ensure the final string is not empty and starts with a capital letter (PascalCase).
        // This turns "ironSword" into "IronSword".
        if (string.IsNullOrEmpty(sanitized))
        {
            return "Unnamed";
        }
    
        // This step is now safe because the rest of the string's capitalization is already correct.
        return char.ToUpper(sanitized[0]) + sanitized.Substring(1);
    }

    #endregion
}