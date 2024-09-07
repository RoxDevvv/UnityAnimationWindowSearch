using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using System;
using System.IO;

public class AnimationClipSearch : EditorWindow
{
    private string searchQuery = "";
    private ListView clipsListView;
    private VisualElement root;
    private string previousSearchQuery = string.Empty;
    [MenuItem("Tools/Animation Clip Search")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimationClipSearch>("Animation Clip Search");
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void CreateGUI()
    {
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        string directoryPath = Path.GetDirectoryName(scriptPath);
        string visualTreePath = Path.Combine(directoryPath, "AnimationClipSearchWindow.uxml");
        string styleSheetPath = Path.Combine(directoryPath, "AnimationClipSearchWindow.uss");

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(visualTreePath);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);

        if (visualTree == null || styleSheet == null)
        {
            Debug.LogError("Failed to load UXML or USS file. Check the paths.");
            return;
        }
        root = visualTree.CloneTree();
        root.styleSheets.Add(styleSheet);
        rootVisualElement.Add(root);

        clipsListView = root.Q<ListView>("clipsListView");
        var searchContainer = root.Q<IMGUIContainer>("searchContainer");

        clipsListView.makeItem = () =>
        {
            var wrapper = new VisualElement { name = "button-wrapper" };
            wrapper.Add(new Button());
            return wrapper;
        };
        clipsListView.bindItem = (element, index) =>
        {
            var clip = GetFilteredAnimationClips()[index];
            var button = (Button)element[0];
            button.text = clip.name;
            button.clicked += () => SetActiveAnimationClip(clip);
        };
        clipsListView.fixedItemHeight = 40;
        clipsListView.selectionType = SelectionType.None;
        clipsListView.itemsSource = GetFilteredAnimationClips();


        searchContainer.onGUIHandler = OnSearchGUI;
        // Subscribe to selection changes
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnSearchGUI()
    {
        string newSearchQuery = EditorGUILayout.TextField(searchQuery);
        if (newSearchQuery != previousSearchQuery)
        {
            searchQuery = newSearchQuery;
            previousSearchQuery = searchQuery;
            clipsListView.itemsSource = GetFilteredAnimationClips();
            clipsListView.Rebuild();
        }
    }
    private void OnSelectionChanged()
    {
        clipsListView.itemsSource = GetFilteredAnimationClips();
        clipsListView.Rebuild();
    }
    private AnimationClip[] GetFilteredAnimationClips()
    {
        var selectedGameObject = Selection.activeGameObject;
        if (selectedGameObject != null)
        {
            var animator = selectedGameObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    clips = clips.Where(clip => clip.name.ToLower().Contains(searchQuery.ToLower())).ToArray();
                }
                return clips;
            }
        }
        return Array.Empty<AnimationClip>();
    }
    private void SetActiveAnimationClip(AnimationClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("AnimationClip is null");
            return;
        }

        // Open the Animation window if not already open
        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");

        var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
        if (animationWindowType == null) return;

        var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType).FirstOrDefault() as EditorWindow;
        if (animationWindow == null) return;

        var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (animEditorField == null) return;

        var animEditor = animEditorField.GetValue(animationWindow);
        var stateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        if (stateField == null) return;

        var animationWindowState = stateField.GetValue(animEditor);
        if (animationWindowState == null) return;

        var activeAnimationClipProperty = animationWindowState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        if (activeAnimationClipProperty == null) return;

        activeAnimationClipProperty.SetValue(animationWindowState, clip);
        animationWindow.Repaint();
    }
}
