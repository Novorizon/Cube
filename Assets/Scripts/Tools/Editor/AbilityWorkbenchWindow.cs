#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Game;
using Game.Ability;
using Game.Ability.Configuration;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class AbilityWorkbenchWindow : EditorWindow
    {
        private enum WorkbenchTab
        {
            ExcelChecker,
            JsonEditor,
            RuntimeDebugger
        }

        private readonly List<AbilityBindingDebugInfo> bindings = new List<AbilityBindingDebugInfo>();
        private WorkbenchTab tab;
        private string projectRoot;
        private string excelDir;
        private AbilityConfigCatalog excelCatalog;
        private AbilityValidationReport excelReport;
        private string excelError;
        private string search = string.Empty;
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showInfo = true;
        private int selectedAbilityId;
        private AbilityValidationIssue selectedIssue;
        private Vector2 excelScroll;
        private Vector2 jsonScroll;
        private Vector2 runtimeScroll;

        private AbilityJsonEditorState jsonState;
        private SerializedObject jsonSerialized;
        private string jsonPath;
        private AbilityValidationReport jsonReport;
        private string jsonMessage;

        private bool autoRefreshRuntime = true;
        private double lastRuntimeRefresh;
        private AbilityRuntimeSnapshot runtimeSnapshot;
        private bool showBindings = true;
        private bool showAbilities = true;
        private bool showModifiers = true;
        private bool showProjectiles = true;
        private bool showThinkers = true;
        private bool showPresentation = true;

        [MenuItem("Tools/Ability/Workbench")]
        public static void Open()
        {
            GetWindow<AbilityWorkbenchWindow>("Ability Workbench");
        }

        private void OnEnable()
        {
            projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            excelDir = Path.Combine(projectRoot, "Data", "Excel");
            EnsureJsonState();
            RefreshExcel();
        }

        private void OnDisable()
        {
            if (jsonState != null)
            {
                DestroyImmediate(jsonState);
                jsonState = null;
                jsonSerialized = null;
            }
        }

        private void OnInspectorUpdate()
        {
            if (tab == WorkbenchTab.RuntimeDebugger && autoRefreshRuntime && EditorApplication.timeSinceStartup - lastRuntimeRefresh >= 0.25d)
            {
                RefreshRuntime();
                Repaint();
            }
        }

        private void OnGUI()
        {
            tab = (WorkbenchTab)GUILayout.Toolbar((int)tab, new[] { "Excel 检查器", "JSON 编辑", "运行时调试" });
            EditorGUILayout.Space(4f);
            switch (tab)
            {
                case WorkbenchTab.ExcelChecker:
                    DrawExcelChecker();
                    break;
                case WorkbenchTab.JsonEditor:
                    DrawJsonEditor();
                    break;
                case WorkbenchTab.RuntimeDebugger:
                    DrawRuntimeDebugger();
                    break;
            }
        }

        private void DrawExcelChecker()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新读取并校验", GUILayout.Width(130f))) RefreshExcel();
            if (GUILayout.Button("定位 Excel 目录", GUILayout.Width(110f))) EditorUtility.RevealInFinder(excelDir);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(excelError))
            {
                EditorGUILayout.HelpBox(excelError, MessageType.Error);
                return;
            }

            if (excelReport == null || excelCatalog == null)
            {
                EditorGUILayout.HelpBox("尚未读取技能配置。", MessageType.Info);
                return;
            }

            MessageType summaryType = excelReport.ErrorCount > 0 ? MessageType.Error : excelReport.WarningCount > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(
                "Error " + excelReport.ErrorCount + " / Warning " + excelReport.WarningCount + " / Info " + excelReport.InfoCount +
                "    Ability " + excelCatalog.Abilities.Count + " / Modifier " + excelCatalog.Modifiers.Count + " / Action " + excelCatalog.Actions.Count,
                summaryType);

            EditorGUILayout.BeginHorizontal();
            search = EditorGUILayout.TextField("搜索", search);
            showErrors = GUILayout.Toggle(showErrors, "Error", "Button", GUILayout.Width(65f));
            showWarnings = GUILayout.Toggle(showWarnings, "Warning", "Button", GUILayout.Width(75f));
            showInfo = GUILayout.Toggle(showInfo, "Info", "Button", GUILayout.Width(55f));
            EditorGUILayout.EndHorizontal();

            excelScroll = EditorGUILayout.BeginScrollView(excelScroll);
            EditorGUILayout.LabelField("校验问题", EditorStyles.boldLabel);
            DrawIssues(excelReport, true);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("技能列表", EditorStyles.boldLabel);
            DrawAbilityList();
            EditorGUILayout.Space(8f);
            DrawSelectedAbility();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIssues(AbilityValidationReport report, bool filter)
        {
            if (report == null || report.Issues.Count == 0)
            {
                EditorGUILayout.LabelField("无问题。", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                AbilityValidationIssue issue = report.Issues[i];
                if (filter && !ShouldShow(issue)) continue;
                string haystack = issue.Code + " " + issue.Message + " " + issue.ReferenceChain + " " + issue.Source;
                if (filter && !Matches(haystack)) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUIStyle severityStyle = issue.Severity == AbilityValidationSeverity.Error
                    ? EditorStyles.boldLabel
                    : EditorStyles.label;
                EditorGUILayout.LabelField(issue.Severity + " [" + issue.Code + "]", severityStyle, GUILayout.Width(165f));
                if (GUILayout.Button("选择", GUILayout.Width(50f))) selectedIssue = issue;
                if (issue.Source != null && GUILayout.Button("定位", GUILayout.Width(50f))) RevealSource(issue.Source);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(issue.ReferenceChain)) EditorGUILayout.LabelField("引用链: " + issue.ReferenceChain, EditorStyles.miniLabel);
                if (issue.Source != null) EditorGUILayout.LabelField("来源: " + issue.Source, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            if (selectedIssue != null)
            {
                EditorGUILayout.HelpBox("已选问题: " + selectedIssue.Code + " — " + selectedIssue.Message, MessageType.None);
            }
        }

        private void DrawAbilityList()
        {
            for (int i = 0; i < excelCatalog.Abilities.Count; i++)
            {
                AbilityConfigRecord record = excelCatalog.Abilities[i];
                if (record?.Definition == null) continue;
                string label = record.Id + "  " + record.Definition.DisplayName + "  [" + record.Definition.Name + "]";
                if (!Matches(label)) continue;
                bool selected = selectedAbilityId == record.Id;
                if (GUILayout.Toggle(selected, label, "Button") && !selected)
                {
                    selectedAbilityId = record.Id;
                }
            }
        }

        private void DrawSelectedAbility()
        {
            AbilityConfigRecord record = FindAbility(selectedAbilityId);
            if (record?.Definition == null)
            {
                EditorGUILayout.HelpBox("选择一个技能查看只读定义和引用关系。", MessageType.Info);
                return;
            }

            AbilityDefinition definition = record.Definition;
            EditorGUILayout.LabelField("只读预览", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ID / Internal", record.Id + " / " + definition.Name);
            EditorGUILayout.LabelField("显示名", definition.DisplayName ?? string.Empty);
            EditorGUILayout.LabelField("Behavior", definition.Behavior.ToString());
            EditorGUILayout.LabelField("Target", definition.TargetTeam + " / " + definition.TargetType + " / " + definition.TargetFlags);
            EditorGUILayout.LabelField("Range / AOE", definition.CastRange.GetValue(1) + " / " + definition.AoeRadius.GetValue(1));
            EditorGUILayout.LabelField("CastPoint / Cooldown", definition.CastPoint.GetValue(1) + " / " + definition.Cooldown.GetValue(1));
            EditorGUILayout.LabelField("Action Group", record.ActionGroupId.ToString());
            EditorGUILayout.LabelField("Intrinsic Modifier", record.IntrinsicModifierId.ToString());
            if (record.Source != null && GUILayout.Button("定位源文件")) RevealSource(record.Source);
            EditorGUILayout.EndVertical();

            DrawActionGroup(record.ActionGroupId, "Ability Actions");
            if (record.IntrinsicModifierId > 0)
            {
                AbilityModifierConfigRecord modifier = FindModifier(record.IntrinsicModifierId);
                DrawModifier(modifier, "Intrinsic");
            }
        }

        private void DrawActionGroup(int groupId, string title)
        {
            if (groupId <= 0) return;
            EditorGUILayout.LabelField(title + " — Group " + groupId, EditorStyles.boldLabel);
            for (int i = 0; i < excelCatalog.Actions.Count; i++)
            {
                AbilityActionConfigRecord action = excelCatalog.Actions[i];
                if (action == null || action.GroupId != groupId || action.Definition == null) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("#" + action.Order + " Action " + action.Id + ": " + action.Definition.ActionType + " -> " + action.Definition.Target);
                if (action.ModifierId > 0)
                {
                    EditorGUILayout.LabelField("Modifier: " + action.ModifierId, EditorStyles.miniLabel);
                    DrawModifier(FindModifier(action.ModifierId), "Referenced");
                }
                if (!string.IsNullOrEmpty(action.Definition.EffectName)) EditorGUILayout.LabelField("Effect: " + action.Definition.EffectName, EditorStyles.miniLabel);
                if (action.Source != null && GUILayout.Button("定位动作源", GUILayout.Width(100f))) RevealSource(action.Source);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawModifier(AbilityModifierConfigRecord modifier, string role)
        {
            if (modifier?.Definition == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(role + " Modifier " + modifier.Id + " — " + modifier.Definition.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Duration / Interval / Stack", modifier.Definition.Duration + " / " + modifier.Definition.Interval + " / " + modifier.Definition.MaxStack);
            EditorGUILayout.LabelField("States", modifier.Definition.States.ToString());
            EditorGUILayout.LabelField("Trigger", modifier.Definition.TriggerEventType + " / " + modifier.Definition.TriggerEventScope);
            EditorGUILayout.LabelField(
                "Groups",
                "created=" + modifier.OnCreatedActionGroupId + ", periodic=" + modifier.PeriodicActionGroupId +
                ", trigger=" + modifier.TriggerActionGroupId + ", destroy=" + modifier.OnDestroyActionGroupId);
            if (modifier.Source != null && GUILayout.Button("定位 Modifier 源", GUILayout.Width(130f))) RevealSource(modifier.Source);
            EditorGUILayout.EndVertical();
        }

        private void DrawJsonEditor()
        {
            EnsureJsonState();
            EditorGUILayout.HelpBox(
                "手写源目录与 Luban 生成目录分开。此表单使用 JsonUtility，保存时会格式化文件且不保留注释或未知字段；保存前必须通过 Provider 校验。",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("打开", GUILayout.Width(70f))) OpenJson();
            if (GUILayout.Button("新建", GUILayout.Width(70f))) NewJson();
            using (new EditorGUI.DisabledScope(jsonState.document == null))
            {
                if (GUILayout.Button("校验", GUILayout.Width(70f))) ValidateJson();
                if (GUILayout.Button("保存", GUILayout.Width(70f))) SaveJson(false);
                if (GUILayout.Button("另存为", GUILayout.Width(75f))) SaveJson(true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("文件", string.IsNullOrEmpty(jsonPath) ? "<未保存>" : jsonPath);
            if (!string.IsNullOrEmpty(jsonMessage)) EditorGUILayout.HelpBox(jsonMessage, jsonReport != null && !jsonReport.IsValid ? MessageType.Error : MessageType.Info);

            if (jsonReport != null)
            {
                EditorGUILayout.LabelField("Provider 校验", EditorStyles.boldLabel);
                DrawIssues(jsonReport, false);
            }

            jsonScroll = EditorGUILayout.BeginScrollView(jsonScroll);
            jsonSerialized.Update();
            SerializedProperty documentProperty = jsonSerialized.FindProperty("document");
            EditorGUILayout.PropertyField(documentProperty, true);
            jsonSerialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void DrawRuntimeDebugger()
        {
            EditorGUILayout.BeginHorizontal();
            autoRefreshRuntime = EditorGUILayout.ToggleLeft("自动刷新 (0.25s)", autoRefreshRuntime, GUILayout.Width(130f));
            if (GUILayout.Button("立即刷新", GUILayout.Width(90f))) RefreshRuntime();
            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后显示中间层绑定与 Ability Runtime 快照。", MessageType.Info);
                return;
            }

            AbilityManager manager = AbilityManager.Instance;
            if (manager == null || !manager.IsInitialized)
            {
                EditorGUILayout.HelpBox("AbilityManager 尚未初始化。", MessageType.Warning);
                return;
            }

            if (runtimeSnapshot == null) RefreshRuntime();
            runtimeScroll = EditorGUILayout.BeginScrollView(runtimeScroll);
            showBindings = EditorGUILayout.Foldout(showBindings, "中间层绑定 (" + bindings.Count + ")", true);
            if (showBindings) DrawBindings();
            if (runtimeSnapshot != null)
            {
                showAbilities = EditorGUILayout.Foldout(showAbilities, "技能实例单位 (" + runtimeSnapshot.Units.Count + ")", true);
                if (showAbilities) DrawRuntimeAbilities(runtimeSnapshot);
                showModifiers = EditorGUILayout.Foldout(showModifiers, "Modifiers (" + runtimeSnapshot.Modifiers.Count + ")", true);
                if (showModifiers) DrawRuntimeModifiers(runtimeSnapshot);
                showProjectiles = EditorGUILayout.Foldout(showProjectiles, "Projectiles (" + runtimeSnapshot.Projectiles.Count + ")", true);
                if (showProjectiles) DrawRuntimeProjectiles(runtimeSnapshot);
                showThinkers = EditorGUILayout.Foldout(showThinkers, "Thinkers (" + runtimeSnapshot.Thinkers.Count + ")", true);
                if (showThinkers) DrawRuntimeThinkers(runtimeSnapshot);
                showPresentation = EditorGUILayout.Foldout(showPresentation, "持续表现句柄 (" + runtimeSnapshot.PresentationHandles.Count + ")", true);
                if (showPresentation) DrawPresentationHandles(runtimeSnapshot);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBindings()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                AbilityBindingDebugInfo binding = bindings[i];
                EditorGUILayout.LabelField(
                    binding.Kind + "  runtime=" + binding.RuntimeEntityId + " business=" + binding.BusinessObjectId +
                    " valid=" + binding.IsValid + "  " + binding.DisplayName,
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawRuntimeAbilities(AbilityRuntimeSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                AbilityUnitRuntimeSnapshot unit = snapshot.Units[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Entity " + unit.EntityId + " team=" + unit.TeamId + " alive=" + unit.IsAlive + " pos=" + unit.Position);
                for (int j = 0; j < unit.Abilities.Count; j++)
                {
                    AbilityInstanceRuntimeSnapshot ability = unit.Abilities[j];
                    EditorGUILayout.LabelField(
                        ability.Name + " L" + ability.Level + " " + ability.Phase +
                        " cd=" + ability.CooldownRemaining.ToString("0.###") + " charges=" + ability.Charges +
                        " active=" + ability.Activated,
                        EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawRuntimeModifiers(AbilityRuntimeSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                ModifierRuntimeSnapshot modifier = snapshot.Modifiers[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(modifier.Name + " parent=" + modifier.ParentEntityId + " caster=" + modifier.CasterEntityId + " ability=" + modifier.AbilityName);
                EditorGUILayout.LabelField("stack=" + modifier.Stacks + " remain=" + modifier.RemainingTime.ToString("0.###") + "/" + modifier.Duration + " states=" + modifier.States, EditorStyles.miniLabel);
                for (int j = 0; j < modifier.Properties.Count; j++)
                {
                    ModifierPropertyRuntimeSnapshot property = modifier.Properties[j];
                    EditorGUILayout.LabelField(property.Property + " = " + property.Value, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawRuntimeProjectiles(AbilityRuntimeSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Projectiles.Count; i++)
            {
                ProjectileRuntimeSnapshot projectile = snapshot.Projectiles[i];
                EditorGUILayout.LabelField(
                    projectile.Name + " ability=" + projectile.AbilityName + " caster=" + projectile.CasterEntityId +
                    " target=" + projectile.TargetEntityId + " pos=" + projectile.Position + " tracking=" + projectile.Tracking,
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawRuntimeThinkers(AbilityRuntimeSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Thinkers.Count; i++)
            {
                ThinkerRuntimeSnapshot thinker = snapshot.Thinkers[i];
                EditorGUILayout.LabelField(
                    thinker.AbilityName + " caster=" + thinker.CasterEntityId + " pos=" + thinker.Position +
                    " duration=" + thinker.Duration + " interval=" + thinker.Interval + " radius=" + thinker.Radius,
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawPresentationHandles(AbilityRuntimeSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.PresentationHandles.Count; i++)
            {
                PresentationHandleInfo handle = snapshot.PresentationHandles[i];
                EditorGUILayout.LabelField(
                    handle.EffectName + " target=" + handle.TargetEntityId + " active=" + handle.IsActive,
                    EditorStyles.miniLabel);
            }
        }

        private void RefreshExcel()
        {
            try
            {
                excelCatalog = AbilityExcelValidationRunner.LoadCatalog(excelDir);
                excelReport = AbilityExcelValidationRunner.Validate(excelDir, projectRoot);
                excelError = null;
                if (selectedAbilityId == 0 && excelCatalog.Abilities.Count > 0) selectedAbilityId = excelCatalog.Abilities[0].Id;
            }
            catch (Exception exception)
            {
                excelCatalog = null;
                excelReport = null;
                excelError = exception.Message;
            }
        }

        private void RefreshRuntime()
        {
            lastRuntimeRefresh = EditorApplication.timeSinceStartup;
            if (!Application.isPlaying) return;
            AbilityManager manager = AbilityManager.Instance;
            if (manager == null || !manager.IsInitialized) return;
            manager.GetBindingDebugSnapshot(bindings);
            runtimeSnapshot = manager.Engine.CreateRuntimeSnapshot();
        }

        private void EnsureJsonState()
        {
            if (jsonState != null) return;
            jsonState = CreateInstance<AbilityJsonEditorState>();
            jsonState.hideFlags = HideFlags.HideAndDontSave;
            jsonState.document = new JsonAbilityDocument { schemaVersion = 1 };
            jsonSerialized = new SerializedObject(jsonState);
        }

        private void OpenJson()
        {
            string root = Path.Combine(projectRoot, "Data", "AbilityJsonSources");
            string path = EditorUtility.OpenFilePanel("Open Ability JSON", root, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                JsonAbilityDocument document = JsonUtility.FromJson<JsonAbilityDocument>(File.ReadAllText(path));
                if (document == null) throw new InvalidDataException("JSON did not contain an ability document.");
                jsonState.document = document;
                jsonSerialized = new SerializedObject(jsonState);
                jsonPath = path;
                ValidateJson();
            }
            catch (Exception exception)
            {
                jsonMessage = exception.Message;
                jsonReport = null;
            }
        }

        private void NewJson()
        {
            jsonState.document = new JsonAbilityDocument { schemaVersion = 1 };
            jsonSerialized = new SerializedObject(jsonState);
            jsonPath = null;
            jsonReport = null;
            jsonMessage = "已创建未保存的 schemaVersion 1 文档。";
        }

        private void ValidateJson()
        {
            jsonSerialized.ApplyModifiedProperties();
            string json = JsonUtility.ToJson(jsonState.document, true);
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProviders(new[]
            {
                new JsonAbilityDefinitionProvider(json, string.IsNullOrEmpty(jsonPath) ? "<unsaved>" : MakeProjectRelative(jsonPath))
            });
            jsonReport = registry.Validation;
            jsonMessage = "Error " + jsonReport.ErrorCount + " / Warning " + jsonReport.WarningCount + " / Info " + jsonReport.InfoCount;
        }

        private void SaveJson(bool saveAs)
        {
            ValidateJson();
            if (jsonReport == null || !jsonReport.IsValid)
            {
                jsonMessage = "存在 Error，未保存。";
                return;
            }

            string path = jsonPath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                string root = Path.Combine(projectRoot, "Data", "AbilityJsonSources");
                path = EditorUtility.SaveFilePanel("Save Ability JSON", root, "ability.json", "json");
            }
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, JsonUtility.ToJson(jsonState.document, true));
            jsonPath = path;
            jsonMessage = "已保存并通过 Provider 校验：" + MakeProjectRelative(path);
            AssetDatabase.Refresh();
        }

        private bool ShouldShow(AbilityValidationIssue issue)
        {
            if (issue.Severity == AbilityValidationSeverity.Error) return showErrors;
            if (issue.Severity == AbilityValidationSeverity.Warning) return showWarnings;
            return showInfo;
        }

        private bool Matches(string value)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   (!string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private AbilityConfigRecord FindAbility(int id)
        {
            if (excelCatalog == null) return null;
            for (int i = 0; i < excelCatalog.Abilities.Count; i++)
            {
                if (excelCatalog.Abilities[i]?.Id == id) return excelCatalog.Abilities[i];
            }
            return null;
        }

        private AbilityModifierConfigRecord FindModifier(int id)
        {
            if (excelCatalog == null) return null;
            for (int i = 0; i < excelCatalog.Modifiers.Count; i++)
            {
                if (excelCatalog.Modifiers[i]?.Id == id) return excelCatalog.Modifiers[i];
            }
            return null;
        }

        private void RevealSource(AbilityConfigSource source)
        {
            if (source == null || string.IsNullOrEmpty(source.Path)) return;
            string path = source.Path.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(path)) path = Path.Combine(projectRoot, path);
            if (File.Exists(path) || Directory.Exists(path)) EditorUtility.RevealInFinder(path);
        }

        private string MakeProjectRelative(string path)
        {
            string full = Path.GetFullPath(path);
            string root = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length).Replace('\\', '/')
                : full.Replace('\\', '/');
        }
    }

    internal sealed class AbilityJsonEditorState : ScriptableObject
    {
        public JsonAbilityDocument document;
    }
}

#endif
