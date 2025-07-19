using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class InteractionDefinitionWizard : EditorWindow
{
    // --- UI State & Input Fields ---
    private Vector2 scrollPosition;

    // Identification & UI
    private string uiTitle = "New Interaction";
    // NEW: Separate name for the asset file
    private string uniqueName = "NewInteraction";
    [TextArea(3, 5)]
    private string uiDescription = "A new interaction for an object or NPC.";

    // Access Roles
    private List<NpcRoleSO> rolesCanExecuteNoSuspicion = new List<NpcRoleSO>();
    private List<NpcRoleSO> rolesCanExecuteWithSuspicion = new List<NpcRoleSO>();
    private List<NpcRoleSO> rolesCanView = new List<NpcRoleSO>();
    private bool generallyVisible = true;

    // Execution Requirements & Effects
    private float requiredProximity = 5f;
    private List<InteractionAvailableFrom> interactionAvailableFroms = new List<InteractionAvailableFrom> { InteractionAvailableFrom.World };
    private float interactionDuration = 1.0f;
    private int witnessSuspicionLevel = 0;

    // Animations
    private string initiatorAnimationTrigger;
    private string targetAnimationTrigger;
    private bool endAnimationOnFinish = false;
    private bool endAnimationOnInterrupt = true;

    // Sounds (with foldout states)
    private InteractionSoundResult initiatorSoundOnStart = new InteractionSoundResult();
    private bool initiatorSoundOnStartFoldout = false;
    private InteractionSoundResult targetSoundOnStart = new InteractionSoundResult();
    private bool targetSoundOnStartFoldout = false;
    private InteractionSoundResult initiatorSoundOnFinish = new InteractionSoundResult();
    private bool initiatorSoundOnFinishFoldout = false;
    private InteractionSoundResult targetSoundOnFinish = new InteractionSoundResult();
    private bool targetSoundOnFinishFoldout = false;

    // Failure Reasons
    private List<HumanReadableFailureReason> humanReadableFailureReasons = new List<HumanReadableFailureReason>();

    // Debugging
    private string debugPrompt;

    // --- Path Management ---
    private string baseInteractionPath;
    private string[] subfolderOptions = new string[0];
    private int selectedSubfolderIndex = 0;
    private string newSubfolderName = "";

    // --- Constants ---
    private const string FALLBACK_ASSET_PATH = "Assets/NPCs/Generated/Interactions";

    [MenuItem("Tools/Interactables/Create Interaction Definition Wizard")]
    public static void ShowWindow()
    {
        GetWindow<InteractionDefinitionWizard>("Create Interaction Definition");
    }

    void OnEnable()
    {
        SetDefaultBasePath();
        RefreshSubfolderList();
        PopulateDefaultFailureReasons();
    }
    
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Interaction Definition Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Create a new InteractionDefinitionSO asset. The final save location is determined by the category/subfolder.", MessageType.Info);
        
        EditorGUILayout.Space();

        // --- MODIFIED: Identification Section ---
        EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
        uiTitle = EditorGUILayout.TextField("UI Title", uiTitle);
        
        EditorGUILayout.BeginHorizontal();
        uniqueName = EditorGUILayout.TextField(new GUIContent("Unique Name (for filename)", "Used for the asset filename (ID_[UniqueName].asset). No spaces or special characters."), uniqueName);
        if (GUILayout.Button(new GUIContent("<<", "Generate from UI Title"), GUILayout.Width(30)))
        {
            uniqueName = SanitizeForFileName(uiTitle);
            GUI.FocusControl(null); // Deselect field to show the change
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("UI Description");
        uiDescription = EditorGUILayout.TextArea(uiDescription, GUILayout.Height(40));

        // --- All other sections remain the same ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Access & Visibility", EditorStyles.boldLabel);
        DrawRoleList("Roles Can Execute (No Suspicion)", rolesCanExecuteNoSuspicion);
        DrawRoleList("Roles Can Execute (With Suspicion)", rolesCanExecuteWithSuspicion);
        DrawRoleList("Roles Can View", rolesCanView);
        generallyVisible = EditorGUILayout.Toggle(new GUIContent("Generally Visible", "If true, the action is visible but greyed out for those who can't execute it. If false, it's hidden unless the user has a specific 'Can View' role."), generallyVisible);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);
        requiredProximity = EditorGUILayout.FloatField("Required Proximity", requiredProximity);
        DrawInteractionAvailableFromField();
        interactionDuration = EditorGUILayout.FloatField("Interaction Duration", interactionDuration);
        witnessSuspicionLevel = EditorGUILayout.IntField("Witness Suspicion Level", witnessSuspicionLevel);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
        initiatorAnimationTrigger = EditorGUILayout.TextField("Initiator Animation Trigger", initiatorAnimationTrigger);
        targetAnimationTrigger = EditorGUILayout.TextField("Target Animation Trigger", targetAnimationTrigger);
        endAnimationOnFinish = EditorGUILayout.Toggle("End Animation on Finish", endAnimationOnFinish);
        endAnimationOnInterrupt = EditorGUILayout.Toggle("End Animation on Interrupt", endAnimationOnInterrupt);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sounds", EditorStyles.boldLabel);
        initiatorSoundOnStartFoldout = DrawSoundResult("Initiator Sound on Start", initiatorSoundOnStart, initiatorSoundOnStartFoldout);
        targetSoundOnStartFoldout = DrawSoundResult("Target Sound on Start", targetSoundOnStart, targetSoundOnStartFoldout);
        initiatorSoundOnFinishFoldout = DrawSoundResult("Initiator Sound on Finish", initiatorSoundOnFinish, initiatorSoundOnFinishFoldout);
        targetSoundOnFinishFoldout = DrawSoundResult("Target Sound on Finish", targetSoundOnFinish, targetSoundOnFinishFoldout);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Failure Reason Messages", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Default reasons are pre-populated. You can customize the text for this specific interaction.", MessageType.None);
        foreach(var reason in humanReadableFailureReasons)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(reason.Reason.ToString(), GUILayout.Width(150));
            reason.HumanReadableReason = EditorGUILayout.TextField(reason.HumanReadableReason);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Asset Location", EditorStyles.boldLabel);
        baseInteractionPath = EditorGUILayout.TextField("Base Interactions Path", baseInteractionPath);

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

        GUI.enabled = false;
        string finalPathPreview = "N/A";
        if (subfolderOptions.Length > 0 && selectedSubfolderIndex < subfolderOptions.Length)
        {
            finalPathPreview = Path.Combine(baseInteractionPath, subfolderOptions[selectedSubfolderIndex]);
        }
        EditorGUILayout.TextField("Final Save Directory", finalPathPreview);
        GUI.enabled = true;
        
        EditorGUILayout.Space(20);

        if (GUILayout.Button("Create Interaction Definition Asset", GUILayout.Height(30)))
        {
            if (ValidateInputs())
            {
                CreateInteractionDefinitionAsset();
            }
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    // --- MODIFIED: Validation now checks uniqueName ---
    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(uiTitle)) {
            EditorUtility.DisplayDialog("Error", "UI Title cannot be empty.", "OK");
            return false;
        }
        if (string.IsNullOrWhiteSpace(uniqueName)) {
            EditorUtility.DisplayDialog("Error", "Unique Name cannot be empty.", "OK");
            return false;
        }
        if (uniqueName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            EditorUtility.DisplayDialog("Error", $"Unique Name '{uniqueName}' contains invalid characters.", "OK");
            return false;
        }
        if (subfolderOptions.Length == 0 || selectedSubfolderIndex >= subfolderOptions.Length) {
            EditorUtility.DisplayDialog("Error", "A valid category/subfolder must be selected.", "OK");
            return false;
        }
        return true;
    }

    // --- MODIFIED: Asset creation now uses uniqueName ---
    private void CreateInteractionDefinitionAsset()
    {
        string selectedSubfolder = subfolderOptions[selectedSubfolderIndex];
        string finalDirectory = Path.Combine(baseInteractionPath, selectedSubfolder);

        var newInteraction = ScriptableObject.CreateInstance<InteractionDefinitionSO>();
        
        PopulateSOFields(newInteraction);
        
        // Use the sanitized unique name for the filename.
        string finalUniqueName = SanitizeForFileName(uniqueName);
        string assetName = $"ID_{finalUniqueName}.asset";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(finalDirectory, assetName));
        
        AssetDatabase.CreateAsset(newInteraction, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newInteraction;
        Debug.Log($"Successfully created Interaction Definition '{uiTitle}' at: {assetPath}");
        this.Close();
    }

    // (The rest of the helper methods are here, unchanged, for completeness)
    
    #region Helper Methods

    private void PopulateSOFields(InteractionDefinitionSO so)
    {
        so.GetType().GetField("uiTitle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, uiTitle);
        so.GetType().GetField("uiDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, uiDescription);
        so.GetType().GetField("rolesCanExecuteNoSuspicion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, new List<NpcRoleSO>(rolesCanExecuteNoSuspicion));
        so.GetType().GetField("rolesCanExecuteWithSuspicion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, new List<NpcRoleSO>(rolesCanExecuteWithSuspicion));
        so.GetType().GetField("rolesCanView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, new List<NpcRoleSO>(rolesCanView));
        so.GetType().GetField("generallyVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, generallyVisible);
        so.GetType().GetField("requiredProximity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, requiredProximity);
        so.GetType().GetField("interactionDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, interactionDuration);
        so.GetType().GetField("witnessSuspicionLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, witnessSuspicionLevel);
        so.GetType().GetField("initiatorAnimationTrigger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, initiatorAnimationTrigger);
        so.GetType().GetField("targetAnimationTrigger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, targetAnimationTrigger);
        so.GetType().GetField("endAnimationOnFinish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, endAnimationOnFinish);
        so.GetType().GetField("endAnimationOnInterrupt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, endAnimationOnInterrupt);
        so.GetType().GetField("initiatorSoundOnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, initiatorSoundOnStart);
        so.GetType().GetField("targetSoundOnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, targetSoundOnStart);
        so.GetType().GetField("initiatorSoundOnFinish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, initiatorSoundOnFinish);
        so.GetType().GetField("targetSoundOnFinish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, targetSoundOnFinish);
        so.GetType().GetField("humanReadableFailureReasons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, humanReadableFailureReasons);
        so.GetType().GetField("debugPrompt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, debugPrompt);
        so.GetType().GetField("interactionAvailableFroms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(so, new List<InteractionAvailableFrom>(interactionAvailableFroms));
    }
    
    private void DrawInteractionAvailableFromField()
    {
        var enumValues = System.Enum.GetValues(typeof(InteractionAvailableFrom)).Cast<InteractionAvailableFrom>().ToList();
        var enumNames = System.Enum.GetNames(typeof(InteractionAvailableFrom));
        int mask = 0;
        for (int i = 0; i < enumValues.Count; i++) {
            if (interactionAvailableFroms.Contains(enumValues[i])) {
                mask |= 1 << i;
            }
        }
        EditorGUI.BeginChangeCheck();
        int newMask = EditorGUILayout.MaskField("Available From", mask, enumNames);
        if (EditorGUI.EndChangeCheck()) {
            interactionAvailableFroms.Clear();
            for (int i = 0; i < enumValues.Count; i++) {
                if ((newMask & (1 << i)) != 0) {
                    interactionAvailableFroms.Add(enumValues[i]);
                }
            }
        }
    }
    
    private void SetDefaultBasePath()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = currentScene.name;
        if (!string.IsNullOrEmpty(sceneName) && sceneName.EndsWith("Level")) {
            string uniqueName = sceneName.Substring(0, sceneName.Length - "Level".Length);
            baseInteractionPath = $"Assets/Resources/SOs/Level_{uniqueName}/Interactions";
        }
        else {
            baseInteractionPath = FALLBACK_ASSET_PATH;
        }
    }

    private void RefreshSubfolderList()
    {
        if (string.IsNullOrEmpty(baseInteractionPath) || !Directory.Exists(baseInteractionPath)) return;
        var directories = Directory.GetDirectories(baseInteractionPath).Select(Path.GetFileName).ToList();
        if (directories.Count == 0) {
            string defaultFolder = Path.Combine(baseInteractionPath, "General");
            if(!Directory.Exists(defaultFolder)) Directory.CreateDirectory(defaultFolder);
            directories.Add("General");
        }
        subfolderOptions = directories.ToArray();
        selectedSubfolderIndex = 0;
    }
    
    private void PopulateDefaultFailureReasons()
    {
        humanReadableFailureReasons.Clear();
        var tempInstance = ScriptableObject.CreateInstance<InteractionDefinitionSO>();
        var allReasonTypes = System.Enum.GetValues(typeof(InteractionFailureReason)).Cast<InteractionFailureReason>();
        foreach (var reasonType in allReasonTypes) {
            HumanReadableFailureReason defaultReason = tempInstance.GetHumanReadableFailureReason(reasonType);
            humanReadableFailureReasons.Add(new HumanReadableFailureReason(defaultReason.Reason, defaultReason.Priority, defaultReason.HumanReadableReason));
        }
        DestroyImmediate(tempInstance);
    }

    private void CreateNewSubfolder()
    {
        if (string.IsNullOrWhiteSpace(newSubfolderName)) return;
        string sanitizedName = string.Join("_", newSubfolderName.Split(Path.GetInvalidFileNameChars()));
        string newPath = Path.Combine(baseInteractionPath, sanitizedName);
        if (Directory.Exists(newPath)) {
            EditorUtility.DisplayDialog("Error", $"A category named '{sanitizedName}' already exists.", "OK");
            return;
        }
        Directory.CreateDirectory(newPath);
        newSubfolderName = "";
        RefreshSubfolderList();
        selectedSubfolderIndex = System.Array.IndexOf(subfolderOptions, sanitizedName);
        GUI.FocusControl(null);
    }
    
    private void DrawRoleList(string label, List<NpcRoleSO> list) {
        EditorGUILayout.LabelField(label);
        EditorGUI.indentLevel++;
        for (int i = 0; i < list.Count; i++) {
            EditorGUILayout.BeginHorizontal();
            list[i] = (NpcRoleSO)EditorGUILayout.ObjectField(list[i], typeof(NpcRoleSO), false);
            if (GUILayout.Button("-", GUILayout.Width(20))) list.RemoveAt(i--);
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Add Role Slot")) list.Add(null);
        EditorGUI.indentLevel--;
    }

    private bool DrawSoundResult(string label, InteractionSoundResult soundResult, bool foldoutState) {
        foldoutState = EditorGUILayout.Foldout(foldoutState, label, true);
        if (foldoutState) {
            EditorGUI.indentLevel++;
            soundResult.Enabled = EditorGUILayout.Toggle("Enabled", soundResult.Enabled);
            GUI.enabled = soundResult.Enabled;
            soundResult.Clip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", soundResult.Clip, typeof(AudioClip), false);
            soundResult.SType = (SoundType)EditorGUILayout.EnumPopup("Sound Type", soundResult.SType);
            soundResult.Suspiciousness = EditorGUILayout.IntSlider("Suspiciousness", soundResult.Suspiciousness, 0, 100);
            soundResult.Loudness = (SoundLoudness)EditorGUILayout.EnumPopup("Loudness", soundResult.Loudness);
            soundResult.CausesReactions = EditorGUILayout.Toggle("Causes Reactions", soundResult.CausesReactions);
            GUI.enabled = true;
            EditorGUI.indentLevel--;
        }
        return foldoutState;
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