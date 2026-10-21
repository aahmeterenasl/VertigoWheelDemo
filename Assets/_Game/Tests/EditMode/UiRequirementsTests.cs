using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Common;
using VertigoWheel.UI.Dialogs;
using VertigoWheel.UI.Rewards;
using VertigoWheel.UI.Wheel;
using Object = UnityEngine.Object;

namespace VertigoWheel.Tests.EditMode
{
    /// <summary>Audits saved authoring assets without changing the user's open scene or saving test edits.</summary>
    public sealed class UiRequirementsTests
    {
        private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static IEnumerable<string> AuthoredUiAssets()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/UI" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).Concat(new[] { GameScenePath });
        }

        [TestCaseSource(nameof(AuthoredUiAssets))]
        public void SavedUi_UsesConsistentNamesAndAppropriateGraphics(string path)
        {
            Inspect(path, roots =>
            {
                foreach (GameObject root in roots)
                {
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    {
                        // TMP owns fallback-material submeshes and their generated names.
                        if (item.GetComponent<TMP_SubMeshUI>() != null) continue;
                        Assert.That(item.name, Does.Match(@"^ui_[a-z0-9]+(?:_[a-z0-9]+)*$"),
                            path + ": " + item.name);
                    }
                    Assert.That(root.GetComponentsInChildren<Text>(true), Is.Empty,
                        path + ": authored UI text must use TextMeshPro.");
                    foreach (Image image in root.GetComponentsInChildren<Image>(true))
                    {
                        string context = path + ": " + image.name;
                        bool isViewport = IsScrollViewport(image);
                        bool isButtonTarget = image.GetComponent<Selectable>()?.targetGraphic == image;
                        bool isModalBlocker = image.GetComponent<DialogView>() != null;
                        Assert.That(!image.raycastTarget || (image.enabled &&
                            (isViewport || isButtonTarget || isModalBlocker)), Is.True,
                            context + " has an unnecessary raycast target.");

                        // Reusable reward prefabs are intentionally clipped by their eventual scroll viewport.
                        bool needsMasking = isViewport || HasClippingParent(image.transform)
                            || image.GetComponentInParent<RewardItemView>(true) != null;
                        Assert.That(image.maskable, Is.EqualTo(needsMasking),
                            context + " should only be Maskable when used by a viewport or scrolling reward item.");
                        if (image.sprite != null && image.sprite.border != Vector4.zero)
                        {
                            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced),
                                context + " must preserve its authored sprite borders.");
                        }
                        if (image.name.Contains("_icon_") || image.name.Contains("_wheel_base_")
                            || image.name.Contains("_wheel_pointer_"))
                        {
                            Assert.That(image.type, Is.EqualTo(Image.Type.Simple), context);
                            Assert.That(image.preserveAspect, Is.True, context + " must not distort artwork.");
                        }
                    }
                    foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Assert.That(label.raycastTarget, Is.False,
                            path + ": " + label.name + " should not intercept input.");
                    }
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component != null)
                        {
                            AssertNoPersistentEvents(component, path);
                            AssertValueNames(component, path);
                        }
                    }
                }
            });
        }

        [Test]
        public void GameCanvas_ExpandsAndAnimationsTargetSeparateChildTransforms()
        {
            Inspect(GameScenePath, roots =>
            {
                Canvas[] canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).ToArray();
                Assert.That(canvases, Is.Not.Empty);
                foreach (Canvas canvas in canvases.Where(canvas => canvas.isRootCanvas))
                {
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    Assert.That(scaler, Is.Not.Null, canvas.name);
                    Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                    Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                }
                foreach (GameObject root in roots)
                {
                    foreach (DialogView dialog in root.GetComponentsInChildren<DialogView>(true))
                    {
                        AssertSeparateChild(dialog, "_content");
                    }
                    foreach (WheelView wheel in root.GetComponentsInChildren<WheelView>(true))
                    {
                        AssertSeparateChild(wheel, "_rotationRoot");
                    }
                }
            });
        }

        [TestCaseSource(nameof(AuthoredUiAssets))]
        public void OnValidate_RecoversMissingButtonReferencesAndPreservesExplicitAssignments(string path)
        {
            Inspect(path, roots =>
            {
                foreach (MonoBehaviour owner in roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (owner == null) continue;
                    FieldInfo[] fields = owner.GetType().GetFields(PrivateInstance)
                        .Where(field => field.IsDefined(typeof(SerializeField), true)
                            && (field.FieldType == typeof(ActionButtonView) || field.FieldType == typeof(Button))).ToArray();
                    if (fields.Length == 0) continue;
                    MethodInfo validate = owner.GetType().GetMethod("OnValidate", PrivateInstance);
                    Assert.That(validate, Is.Not.Null, owner.GetType().Name + " needs Editor button wiring.");
                    Object[] authored = fields.Select(field => (Object)field.GetValue(owner)).ToArray();
                    foreach (Object reference in authored)
                    {
                        Assert.That(reference, Is.Not.Null, path + ": " + owner.name + " has an unassigned button.");
                    }
                    foreach (FieldInfo field in fields) field.SetValue(owner, null);
                    validate.Invoke(owner, null);
                    for (int index = 0; index < fields.Length; index++)
                    {
                        Assert.That(fields[index].GetValue(owner), Is.EqualTo(authored[index]),
                            path + ": " + owner.GetType().Name + "." + fields[index].Name);
                    }

                    GameObject explicitObject = new GameObject("ui_button_explicit_test", typeof(RectTransform),
                        typeof(Image), typeof(Button), typeof(ActionButtonView));
                    explicitObject.transform.SetParent(owner.transform, false);
                    try
                    {
                        foreach (FieldInfo field in fields)
                        {
                            field.SetValue(owner, explicitObject.GetComponent(field.FieldType));
                        }
                        validate.Invoke(owner, null);
                        foreach (FieldInfo field in fields)
                        {
                            Assert.That(field.GetValue(owner), Is.EqualTo(explicitObject.GetComponent(field.FieldType)),
                                path + ": OnValidate must retain explicit " + field.Name + " assignments.");
                        }
                    }
                    finally
                    {
                        for (int index = 0; index < fields.Length; index++) fields[index].SetValue(owner, authored[index]);
                        Object.DestroyImmediate(explicitObject);
                    }
                }
            });
        }

        private static bool IsScrollViewport(Image image)
        {
            return image.GetComponentsInParent<ScrollRect>(true)
                .Any(scroll => scroll.viewport == image.rectTransform);
        }

        private static bool HasClippingParent(Transform item)
        {
            for (Transform parent = item.parent; parent != null; parent = parent.parent)
            {
                if (parent.GetComponent<Mask>() != null || parent.GetComponent<RectMask2D>() != null) return true;
            }
            return false;
        }

        private static void AssertNoPersistentEvents(MonoBehaviour component, string path)
        {
            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.isArray && property.propertyPath.EndsWith(".m_PersistentCalls.m_Calls", StringComparison.Ordinal))
                    {
                        Assert.That(property.arraySize, Is.Zero,
                            path + ": " + component.name + "." + property.propertyPath + " must be wired in code.");
                    }
                }
            }
        }

        private static void AssertValueNames(MonoBehaviour owner, string path)
        {
            string[] fields = owner is RewardItemView ? new[] { "_icon", "_name", "_quantity" }
                : owner is WheelSlotView ? new[] { "_icon", "_quantity" }
                : owner is RewardRevealView ? new[] { "_icon", "_name", "_quantity", "_glow", "_shine" }
                : owner is BombResultView ? new[] { "_icon" }
                : owner is CashOutSummaryView ? new[] { "_title" }
                : owner is ZoneTrackView ? new[] { "_background", "_currentBackground" }
                : owner is ZoneItemView ? new[] { "_number" }
                : owner is GameView ? new[] { "_zoneLabel", "_nextSafeLabel", "_nextSuperLabel", "_statusLabel" }
                : owner is WheelView ? new[] { "_baseImage", "_pointer" }
                : Array.Empty<string>();
            foreach (string name in fields)
            {
                FieldInfo field = owner.GetType().GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
                var graphic = (Graphic)field.GetValue(owner);
                // Compact reward rows intentionally omit the optional reward-name label.
                if (graphic == null && owner is RewardItemView && name == "_name") continue;
                Assert.That(graphic, Is.Not.Null, path + ": " + owner.name + "." + name);
                Assert.That(graphic.name, Does.EndWith("_value"),
                    path + ": " + graphic.name + " changes its displayed content at runtime.");
            }
        }

        private static void AssertSeparateChild(Component owner, string fieldName)
        {
            var target = (Transform)owner.GetType().GetField(fieldName, PrivateInstance).GetValue(owner);
            Assert.That(target, Is.Not.Null, owner.name + "." + fieldName);
            Assert.That(target, Is.Not.EqualTo(owner.transform), owner.name + " must retain its layout transform.");
            Assert.That(target.IsChildOf(owner.transform), Is.True, owner.name + "." + fieldName);
        }

        private static void Inspect(string path, Action<GameObject[]> inspect)
        {
            if (path.EndsWith(".unity", StringComparison.Ordinal))
            {
                Scene scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    GameObject[] canvasRoots = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                        .Where(canvas => canvas.isRootCanvas).Select(canvas => canvas.gameObject).ToArray();
                    Assert.That(canvasRoots, Is.Not.Empty, path);
                    inspect(canvasRoots);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            else
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    inspect(new[] { root });
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
