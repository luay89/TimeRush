using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FeedbackConfigReferenceValidator
{
    public readonly struct ValidationIssue
    {
        public readonly string AssetPath;
        public readonly string GameObjectName;
        public readonly string ComponentName;
        public readonly string Message;

        public ValidationIssue(string assetPath, string gameObjectName, string componentName, string message)
        {
            AssetPath = assetPath;
            GameObjectName = gameObjectName;
            ComponentName = componentName;
            Message = message;
        }

        public override string ToString()
        {
            return AssetPath + " :: " + GameObjectName + " :: " + ComponentName + " :: " + Message;
        }
    }

    private static readonly ComponentRule[] BootRules =
    {
        new ComponentRule(typeof(FeedbackStateRelay), requireFeedbackConfig: false),
        new ComponentRule(typeof(FeedbackVfxPresenter), requireFeedbackConfig: true),
        new ComponentRule(typeof(FeedbackAudioPresenter), requireFeedbackConfig: true),
        new ComponentRule(typeof(PaceFeedbackEmitter), requireFeedbackConfig: true),
        new ComponentRule(typeof(PauseOverlayPresenter), requireFeedbackConfig: true)
    };

    private static readonly ComponentRule[] GameRules =
    {
        new ComponentRule(typeof(CameraFeedbackController), requireFeedbackConfig: true),
        new ComponentRule(typeof(PlayerMotionFeedbackEmitter), requireFeedbackConfig: true)
    };

    private static readonly ComponentRule[] MenuRules =
    {
        new ComponentRule(typeof(MenuHubUI), requireFeedbackConfig: true)
    };

    private static readonly ComponentRule[] HudRules =
    {
        new ComponentRule(typeof(ScreenFeedbackPresenter), requireFeedbackConfig: true)
    };

    [MenuItem("Tools/TimeRush/Validate Feedback Config References")]
    public static void ValidateAndLog()
    {
        List<ValidationIssue> issues = Validate();

        if (issues.Count == 0)
        {
            Debug.Log("FeedbackConfig reference validation passed.");
            return;
        }

        foreach (ValidationIssue issue in issues)
        {
            Debug.LogError(issue.ToString());
        }

        Debug.LogError("FeedbackConfig reference validation failed with " + issues.Count + " issue(s).");
    }

    public static void ValidateOrThrow()
    {
        List<ValidationIssue> issues = Validate();
        if (issues.Count == 0)
        {
            Debug.Log("FeedbackConfig reference validation passed.");
            return;
        }

        throw new InvalidOperationException(BuildFailureMessage(issues));
    }

    public static List<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        ValidateScene("Assets/_Project/Scenes/Boot.unity", BootRules, issues);
        ValidateScene("Assets/_Project/Scenes/Game.unity", GameRules, issues);
        ValidateScene("Assets/_Project/Scenes/MenuHub.unity", MenuRules, issues);
        ValidatePrefab("Assets/_Project/Prefabs/HUD.prefab", HudRules, issues);

        return issues;
    }

    private static void ValidateScene(string scenePath, ComponentRule[] rules, List<ValidationIssue> issues)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            ValidateObjects(scenePath, scene.GetRootGameObjects(), rules, issues);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    private static void ValidatePrefab(string prefabPath, ComponentRule[] rules, List<ValidationIssue> issues)
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot == null)
        {
            issues.Add(new ValidationIssue(prefabPath, "<asset>", "<prefab>", "Failed to load prefab."));
            return;
        }

        ValidateObjects(prefabPath, new[] { prefabRoot }, rules, issues);
    }

    private static void ValidateObjects(string assetPath, GameObject[] roots, ComponentRule[] rules, List<ValidationIssue> issues)
    {
        foreach (ComponentRule rule in rules)
        {
            var found = new List<MonoBehaviour>();
            for (int i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren(rule.ComponentType, true);
                for (int j = 0; j < components.Length; j++)
                {
                    if (components[j] is MonoBehaviour behaviour)
                    {
                        found.Add(behaviour);
                    }
                }
            }

            if (found.Count == 0)
            {
                issues.Add(new ValidationIssue(assetPath, "<missing>", rule.ComponentType.Name, "Required component was not found."));
                continue;
            }

            for (int i = 0; i < found.Count; i++)
            {
                MonoBehaviour component = found[i];
                if (!rule.RequireFeedbackConfig)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty configProperty = serialized.FindProperty("feedbackConfig");
                if (configProperty == null)
                {
                    issues.Add(new ValidationIssue(assetPath, component.gameObject.name, rule.ComponentType.Name, "Missing serialized field 'feedbackConfig'."));
                    continue;
                }

                if (configProperty.objectReferenceValue == null)
                {
                    issues.Add(new ValidationIssue(assetPath, component.gameObject.name, rule.ComponentType.Name, "feedbackConfig reference is null."));
                }
            }
        }
    }

    private static string BuildFailureMessage(List<ValidationIssue> issues)
    {
        var builder = new StringBuilder();
        builder.AppendLine("FeedbackConfig reference validation failed.");

        for (int i = 0; i < issues.Count; i++)
        {
            builder.AppendLine(issues[i].ToString());
        }

        return builder.ToString();
    }

    private readonly struct ComponentRule
    {
        public readonly Type ComponentType;
        public readonly bool RequireFeedbackConfig;

        public ComponentRule(Type componentType, bool requireFeedbackConfig)
        {
            ComponentType = componentType;
            RequireFeedbackConfig = requireFeedbackConfig;
        }
    }
}
