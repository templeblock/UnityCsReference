// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEditor.Scripting.ScriptCompilation;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor.Compilation;
using System;
using System.IO;
using AssemblyFlags = UnityEditor.Scripting.ScriptCompilation.AssemblyFlags;

namespace UnityEditor
{
    [CustomEditor(typeof(UnityEditorInternal.AssemblyDefinitionImporter))]
    [CanEditMultipleObjects]
    internal class AssemblyDefinitionImporterInspector : AssetImporterEditor
    {
        internal class Styles
        {
            public static readonly GUIContent name = EditorGUIUtility.TrTextContent("Name", "The assembly name is used to generate a <name>.dll file on you disk.");
            public static readonly GUIContent unityReferences = EditorGUIUtility.TrTextContent("Unity References");
            public static readonly GUIContent defineConstraints = EditorGUIUtility.TrTextContent("Define Constraints", "Specify a constraint in the assembly definition. The assembly definition only builds if this constraint returns True.");
            public static readonly GUIContent versionDefines = EditorGUIUtility.TrTextContent("Version Defines", "Specify which versions of a packages and modules to include in compilations.");
            public static readonly GUIContent references = EditorGUIUtility.TrTextContent("Assembly Definition References", "The list of assembly files that this assembly definition should reference.");
            public static readonly GUIContent precompiledReferences = EditorGUIUtility.TrTextContent("Assembly References", "The list of Precompiled assemblies that this assembly definition should reference.");
            public static readonly GUIContent generalOptions = EditorGUIUtility.TrTextContent("General");
            public static readonly GUIContent allowUnsafeCode = EditorGUIUtility.TrTextContent("Allow 'unsafe' Code", "When enabled, the C# compiler for this assembly includes types or members that have the `unsafe` keyword.");
            public static readonly GUIContent overrideReferences = EditorGUIUtility.TrTextContent("Override References", "When enabled, you can select which specific precompiled assemblies to refer to via a drop-down list that appears. When not enabled, this assembly definition refers to all auto-referenced precompiled assemblies.");
            public static readonly GUIContent autoReferenced = EditorGUIUtility.TrTextContent("Auto Referenced", "When enabled, this assembly definition is automatically referenced in predefined assemblies.");
            public static readonly GUIContent useGUIDs = EditorGUIUtility.TrTextContent("Use GUIDs", "Use GUIDs instead of assembly names for Assembly Definition References. Allows referenced assemblies to be renamed without having to update references.");
            public static readonly GUIContent platforms = EditorGUIUtility.TrTextContent("Platforms", "Select which platforms include or exclude in the build that this assembly definition file is for.");
            public static readonly GUIContent anyPlatform = EditorGUIUtility.TrTextContent("Any Platform");
            public static readonly GUIContent includePlatforms = EditorGUIUtility.TrTextContent("Include Platforms");
            public static readonly GUIContent excludePlatforms = EditorGUIUtility.TrTextContent("Exclude Platforms");
            public static readonly GUIContent selectAll = EditorGUIUtility.TrTextContent("Select all");
            public static readonly GUIContent deselectAll = EditorGUIUtility.TrTextContent("Deselect all");
            public static readonly GUIContent apply = EditorGUIUtility.TrTextContent("Apply");
            public static readonly GUIContent revert = EditorGUIUtility.TrTextContent("Revert");
            public static readonly GUIContent loadError = EditorGUIUtility.TrTextContent("Load error");
            public static readonly GUIContent expressionOutcome = EditorGUIUtility.TrTextContent("Expression outcome", "Shows the mathematical equation that your Expression represents.");
        }

        GUIStyle m_TextStyle;

        internal enum MixedBool : int
        {
            Mixed = -1,
            False = 0,
            True = 1
        }

        internal class DefineConstraint
        {
            public string name;
            public MixedBool displayValue;
        }

        internal class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
            public MixedBool displayValue;
        }

        internal class AssemblyDefinitionReference
        {
            public string path
            {
                get { return AssetDatabase.GetAssetPath(asset); }
            }

            public string name;
            public string serializedReference;
            public AssemblyDefinitionAsset asset;
            public CustomScriptAssemblyData data;
            public MixedBool displayValue;
        }

        internal class PrecompiledReference
        {
            public string path
            {
                get { return precompiled.HasValue ? precompiled.Value.Path : null; }
            }

            public PrecompiledAssembly? precompiled;

            public string fileName
            {
                get { return precompiled.HasValue ? AssetPath.GetFileName(precompiled.Value.Path) : null; }
            }

            public string name;

            public MixedBool displayValue;
        }

        internal class AssemblyDefintionState
        {
            public string path
            {
                get { return AssetDatabase.GetAssetPath(asset); }
            }

            public AssemblyDefinitionAsset asset;
            public string name;
            public List<AssemblyDefinitionReference> references;
            public List<PrecompiledReference> precompiledReferences;
            public List<DefineConstraint> defineConstraints;
            public List<VersionDefine> versionDefines;
            public MixedBool[] optionalUnityReferences;
            public MixedBool allowUnsafeCode;
            public MixedBool overrideReferences;
            public MixedBool useGUIDs;
            public MixedBool autoReferenced;
            public MixedBool compatibleWithAnyPlatform;
            public MixedBool[] platformCompatibility;
            public bool modified;
        }

        AssemblyDefintionState[] m_TargetStates;
        SemVersionRangesFactory m_SemVersionRanges;

        AssemblyDefintionState m_State;
        ReorderableList m_ReferencesList;
        ReorderableList m_PrecompiledReferencesList;
        ReorderableList m_VersionDefineList;
        ReorderableList m_DefineConstraints;

        public override bool showImportedObject
        {
            get { return false; }
        }

        public override void OnInspectorGUI()
        {
            if (m_State == null)
            {
                try
                {
                    LoadAssemblyDefinitionFiles();
                    m_SemVersionRanges = new SemVersionRangesFactory();
                }
                catch (Exception e)
                {
                    m_State = null;
                    ShowLoadErrorExceptionGUI(e);
                    return;
                }
            }

            var platforms = Compilation.CompilationPipeline.GetAssemblyDefinitionPlatforms();
            var optionalUnityReferences = CustomScriptAssembly.OptinalUnityAssemblies;

            using (new EditorGUI.DisabledScope(false))
            {
                EditorGUI.BeginChangeCheck();

                if (targets.Length > 1)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        var value = string.Join(", ", m_TargetStates.Select(t => t.name).ToArray());
                        EditorGUILayout.TextField(Styles.name, value, EditorStyles.textField);
                    }
                }
                else
                {
                    m_State.name = EditorGUILayout.TextField(Styles.name, m_State.name, EditorStyles.textField);
                }

                GUILayout.Label(Styles.generalOptions, EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                m_State.allowUnsafeCode = ToggleWithMixedValue(Styles.allowUnsafeCode, m_State.allowUnsafeCode);
                m_State.autoReferenced = ToggleWithMixedValue(Styles.autoReferenced, m_State.autoReferenced);
                m_State.overrideReferences = ToggleWithMixedValue(Styles.overrideReferences, m_State.overrideReferences);

                EditorGUILayout.EndVertical();
                GUILayout.Space(10f);

                GUILayout.Label(Styles.defineConstraints, EditorStyles.boldLabel);
                m_DefineConstraints.DoLayoutList();

                GUILayout.Label(Styles.references, EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical(GUI.skin.box);
                m_State.useGUIDs = ToggleWithMixedValue(Styles.useGUIDs, m_State.useGUIDs);
                EditorGUILayout.EndVertical();

                if (m_State.references.Any(x => x.asset == null))
                {
                    EditorGUILayout.HelpBox("The grayed out assembly references are missing and will not be referenced during compilation.", MessageType.Info);
                }

                m_ReferencesList.DoLayoutList();

                if (m_State.overrideReferences == MixedBool.True)
                {
                    GUILayout.Label(Styles.precompiledReferences, EditorStyles.boldLabel);

                    if (m_State.precompiledReferences.Any(x => !x.precompiled.HasValue && !string.IsNullOrEmpty(x.name)))
                    {
                        EditorGUILayout.HelpBox("The grayed out assembly references are missing and will not be referenced during compilation.", MessageType.Info);
                    }

                    m_PrecompiledReferencesList.DoLayoutList();
                }

                GUILayout.Label(Styles.unityReferences, EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                for (int i = 0; i < optionalUnityReferences.Length; ++i)
                {
                    m_State.optionalUnityReferences[i] = ToggleWithMixedValue(new GUIContent(optionalUnityReferences[i].DisplayName, optionalUnityReferences[i].Tooltip), m_State.optionalUnityReferences[i]);

                    if (m_State.optionalUnityReferences[i] == MixedBool.True)
                    {
                        EditorGUILayout.HelpBox(optionalUnityReferences[i].AdditinalInformationWhenEnabled, MessageType.Info);
                    }
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(10f);

                GUILayout.Label(Styles.platforms, EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                var compatibleWithAnyPlatform = m_State.compatibleWithAnyPlatform;
                m_State.compatibleWithAnyPlatform = ToggleWithMixedValue(Styles.anyPlatform, m_State.compatibleWithAnyPlatform);

                if (compatibleWithAnyPlatform == MixedBool.Mixed && m_State.compatibleWithAnyPlatform != MixedBool.Mixed)
                {
                    // Switching from mixed state to non-mixed state.
                    // Invert state include/exclude compatibility of states that have the opposite compatibility,
                    // so all states are either include or exclude.
                    UpdatePlatformCompatibility(m_State.compatibleWithAnyPlatform, m_TargetStates);

                    // Now that we have potentially update the compatibility states, we now also
                    // need to update the combined state to reflect the changes.
                    UpdateCombinedCompatibility();
                }
                else if (m_State.compatibleWithAnyPlatform != compatibleWithAnyPlatform)
                {
                    InversePlatformCompatibility(m_State);
                }

                if (m_State.compatibleWithAnyPlatform != MixedBool.Mixed)
                {
                    GUILayout.Label(m_State.compatibleWithAnyPlatform == MixedBool.True ? Styles.excludePlatforms : Styles.includePlatforms, EditorStyles.boldLabel);

                    for (int i = 0; i < platforms.Length; ++i)
                    {
                        m_State.platformCompatibility[i] = ToggleWithMixedValue(new GUIContent(platforms[i].DisplayName), m_State.platformCompatibility[i]);
                    }

                    EditorGUILayout.Space();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(Styles.selectAll))
                    {
                        SetPlatformCompatibility(m_State, MixedBool.True);
                    }

                    if (GUILayout.Button(Styles.deselectAll))
                    {
                        SetPlatformCompatibility(m_State, MixedBool.False);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(10f);

                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Styles.versionDefines, EditorStyles.boldLabel);
                m_VersionDefineList.DoLayoutList();
                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                    m_State.modified = true;
            }

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!m_State.modified))
            {
                if (GUILayout.Button(Styles.revert))
                {
                    LoadAssemblyDefinitionFiles();
                }

                if (GUILayout.Button(Styles.apply))
                {
                    SaveAndUpdateAssemblyDefinitionStates(m_State, m_TargetStates);
                }
            }

            GUILayout.EndHorizontal();
        }

        public override void OnDisable()
        {
            if (m_State != null && m_State.modified)
            {
                AssetImporter importer = target as AssetImporter;

                string dialogText = "Unapplied import settings for \'" + importer.assetPath + "\'";

                if (targets.Length > 1)
                    dialogText = "Unapplied import settings for \'" + targets.Length + "\' files";

                if (EditorUtility.DisplayDialog("Unapplied import settings", dialogText, "Apply", "Revert"))
                {
                    SaveAndUpdateAssemblyDefinitionStates(m_State, m_TargetStates);
                }
            }
        }

        static void UpdatePlatformCompatibility(MixedBool compatibleWithAnyPlatform, AssemblyDefintionState[] states)
        {
            if (compatibleWithAnyPlatform == MixedBool.Mixed)
                throw new ArgumentOutOfRangeException("compatibleWithAnyPlatform");

            foreach (var state in states)
            {
                // Same include/exclude compatibility
                if (state.compatibleWithAnyPlatform == compatibleWithAnyPlatform)
                    continue;

                // Opposite compatibility, invert.
                state.compatibleWithAnyPlatform = compatibleWithAnyPlatform;
                InversePlatformCompatibility(state);
            }
        }

        static MixedBool ToMixedBool(bool value)
        {
            return value ? MixedBool.True : MixedBool.False;
        }

        static bool ToBool(MixedBool value)
        {
            if (value == MixedBool.Mixed)
                throw new System.ArgumentException("Cannot convert MixedBool.Mixed to bool");

            return value == MixedBool.True;
        }

        static MixedBool ToggleWithMixedValue(GUIContent title, MixedBool value)
        {
            EditorGUI.showMixedValue = value == MixedBool.Mixed;

            EditorGUI.BeginChangeCheck();

            bool newBoolValue = EditorGUILayout.Toggle(title, value == MixedBool.True);
            if (EditorGUI.EndChangeCheck())
                return newBoolValue ? MixedBool.True : MixedBool.False;

            EditorGUI.showMixedValue = false;
            return value;
        }

        static void InversePlatformCompatibility(AssemblyDefintionState state)
        {
            var platforms = Compilation.CompilationPipeline.GetAssemblyDefinitionPlatforms();

            for (int i = 0; i < platforms.Length; ++i)
                state.platformCompatibility[i] = InverseCompability(state.platformCompatibility[i]);
        }

        static void SetPlatformCompatibility(AssemblyDefintionState state, MixedBool compatibility)
        {
            var platforms = Compilation.CompilationPipeline.GetAssemblyDefinitionPlatforms();

            for (int i = 0; i < platforms.Length; ++i)
                state.platformCompatibility[i] = compatibility;
        }

        static MixedBool InverseCompability(MixedBool compatibility)
        {
            if (compatibility == MixedBool.True)
                return MixedBool.False;

            if (compatibility == MixedBool.False)
                return MixedBool.True;

            return MixedBool.Mixed;
        }

        void ShowLoadErrorExceptionGUI(Exception e)
        {
            if (m_TextStyle == null)
                m_TextStyle = "ScriptText";

            GUILayout.Label(Styles.loadError, EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(EditorGUIUtility.TempContent(e.Message), m_TextStyle);
            EditorGUI.HelpBox(rect, e.Message, MessageType.Error);
        }

        void LoadAssemblyDefinitionFiles()
        {
            m_TargetStates = new AssemblyDefintionState[targets.Length];

            for (int i = 0; i < targets.Length; ++i)
            {
                var importer = targets[i] as AssetImporter;

                if (importer == null)
                    continue;

                m_TargetStates[i] = LoadAssemblyDefintionState(importer.assetPath);
            }

            // Show as many references as the shortest list of references.
            int minReferencesCount = m_TargetStates.Min(t => t.references.Count());
            int minPrecompiledReferencesCount = m_TargetStates.Min(t => t.precompiledReferences.Count());
            int minDefineConstraintsCount = m_TargetStates.Min(t => t.defineConstraints.Count());
            int minVersionDefinesCount = m_TargetStates.Min(t => t.versionDefines.Count());

            m_State = new AssemblyDefintionState();
            m_State.name = m_TargetStates[0].name;
            m_State.references = new List<AssemblyDefinitionReference>();
            m_State.precompiledReferences = new List<PrecompiledReference>();
            m_State.defineConstraints = new List<DefineConstraint>();
            m_State.versionDefines = new List<VersionDefine>();
            m_State.modified = m_TargetStates[0].modified;
            m_State.allowUnsafeCode = m_TargetStates[0].allowUnsafeCode;
            m_State.overrideReferences = m_TargetStates[0].overrideReferences;
            m_State.useGUIDs = m_TargetStates[0].useGUIDs;
            m_State.autoReferenced = m_TargetStates[0].autoReferenced;

            for (int i = 0; i < minReferencesCount; ++i)
                m_State.references.Add(m_TargetStates[0].references[i]);

            for (int i = 0; i < minPrecompiledReferencesCount; ++i)
                m_State.precompiledReferences.Add(m_TargetStates[0].precompiledReferences[i]);

            for (int i = 0; i < minDefineConstraintsCount; ++i)
                m_State.defineConstraints.Add(m_TargetStates[0].defineConstraints[i]);

            for (int i = 0; i < minVersionDefinesCount; ++i)
                m_State.versionDefines.Add(m_TargetStates[0].versionDefines[i]);

            for (int i = 1; i < m_TargetStates.Length; ++i)
            {
                var targetState = m_TargetStates[i];

                for (int r = 0; r < minReferencesCount; ++r)
                {
                    // If already set to mixed, continue.
                    if (m_State.references[r].displayValue == MixedBool.Mixed)
                        continue;

                    // If different from existing value, set to mixed.
                    if (m_State.references[r].path != targetState.references[r].path)
                        m_State.references[r].displayValue = MixedBool.Mixed;
                }

                for (int r = 0; r < minPrecompiledReferencesCount; ++r)
                {
                    // If already set to mixed, continue.
                    if (m_State.precompiledReferences[r].displayValue == MixedBool.Mixed)
                        continue;

                    // If different from existing value, set to mixed.
                    if (m_State.precompiledReferences[r].path != targetState.precompiledReferences[r].path)
                        m_State.precompiledReferences[r].displayValue = MixedBool.Mixed;
                }

                for (int r = 0; r < minVersionDefinesCount; ++r)
                {
                    if (m_State.versionDefines[r].displayValue == MixedBool.Mixed)
                        continue;

                    if (m_State.versionDefines[r].name != targetState.versionDefines[r].name
                        && m_State.versionDefines[r].expression != targetState.versionDefines[r].expression
                        && m_State.versionDefines[r].define != targetState.versionDefines[r].define)
                    {
                        m_State.versionDefines[r].displayValue = MixedBool.Mixed;
                    }
                }

                for (int d = 0; d < m_State.defineConstraints.Count; ++d)
                {
                    // If already set to mixed, continue.
                    if (m_State.defineConstraints[d].displayValue == MixedBool.Mixed)
                        continue;

                    // If different from existing value, set to mixed.
                    if (m_State.defineConstraints[d].name != targetState.defineConstraints[d].name)
                        m_State.defineConstraints[d].displayValue = MixedBool.Mixed;
                }

                if (m_State.allowUnsafeCode != MixedBool.Mixed)
                {
                    if (m_State.allowUnsafeCode != targetState.allowUnsafeCode)
                        m_State.allowUnsafeCode = MixedBool.Mixed;
                }

                if (m_State.overrideReferences != MixedBool.Mixed)
                {
                    if (m_State.overrideReferences != targetState.overrideReferences)
                        m_State.overrideReferences = MixedBool.Mixed;
                }

                if (m_State.useGUIDs != MixedBool.Mixed)
                {
                    if (m_State.useGUIDs != targetState.useGUIDs)
                        m_State.useGUIDs = MixedBool.Mixed;
                }

                m_State.modified |= targetState.modified;
            }

            UpdateCombinedCompatibility();

            // Disable reordering for multi-editing for asmdefs
            bool enableReordering = targets.Length == 1;

            m_ReferencesList = new ReorderableList(m_State.references, typeof(AssemblyDefinitionReference), enableReordering, false, true, true);
            m_ReferencesList.drawElementCallback = DrawReferenceListElement;
            m_ReferencesList.onAddCallback = AddReferenceListElement;
            m_ReferencesList.onRemoveCallback = RemoveReferenceListElement;
            m_ReferencesList.onReorderCallback = ReorderableListChanged;
            m_ReferencesList.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            m_ReferencesList.headerHeight = 3;

            m_DefineConstraints = new ReorderableList(m_State.defineConstraints, typeof(DefineConstraint), enableReordering, false, true, true);
            m_DefineConstraints.drawElementCallback = DrawDefineConstraintListElement;
            m_DefineConstraints.onAddCallback = AddDefineConstraintListElement;
            m_DefineConstraints.onRemoveCallback = RemoveDefineConstraintListElement;
            m_DefineConstraints.onReorderCallback = ReorderableListChanged;
            m_DefineConstraints.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            m_DefineConstraints.headerHeight = 3;

            m_PrecompiledReferencesList = new ReorderableList(m_State.precompiledReferences, typeof(PrecompiledReference), enableReordering, false, true, true);
            m_PrecompiledReferencesList.drawElementCallback = DrawPrecompiledReferenceListElement;
            m_PrecompiledReferencesList.onAddCallback = AddPrecompiledReferenceListElement;
            m_PrecompiledReferencesList.onRemoveCallback = RemovePrecompiledReferenceListElement;
            m_PrecompiledReferencesList.onReorderCallback = ReorderableListChanged;
            m_PrecompiledReferencesList.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            m_PrecompiledReferencesList.headerHeight = 3;

            m_VersionDefineList = new ReorderableList(m_State.versionDefines, typeof(VersionDefine), false, false, true, true);
            m_VersionDefineList.drawElementCallback = DrawVersionDefineListElement;
            m_VersionDefineList.onAddCallback = AddVersionDefineListElement;
            m_VersionDefineList.onRemoveCallback = RemoveVersionDefineListElement;
            m_VersionDefineList.elementHeight = EditorGUIUtility.singleLineHeight * 4 + EditorGUIUtility.standardVerticalSpacing;
            m_VersionDefineList.headerHeight = 3;
            m_VersionDefineList.onReorderCallback = ReorderableListChanged;
        }

        void ReorderableListChanged(ReorderableList list)
        {
            m_State.modified = true;
        }

        private void DrawDefineConstraintListElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var list = m_DefineConstraints.list;
            var defineConstraint = list[index] as DefineConstraint;

            rect.height -= EditorGUIUtility.standardVerticalSpacing;

            string noValue = L10n.Tr("(Missing)");

            var label = defineConstraint.name != null ? defineConstraint.name : noValue;

            bool mixed = defineConstraint.displayValue == MixedBool.Mixed;
            EditorGUI.showMixedValue = mixed;
            var textFieldValue = EditorGUI.TextField(rect, mixed ? L10n.Tr("(Multiple Values)") : label);
            EditorGUI.showMixedValue = false;

            if (!string.IsNullOrEmpty(textFieldValue) && textFieldValue != noValue)
            {
                defineConstraint.name = textFieldValue;
                foreach (var state in m_TargetStates)
                    state.defineConstraints[index] = defineConstraint;
            }
        }

        private void AddDefineConstraintListElement(ReorderableList list)
        {
            ReorderableList.defaultBehaviours.DoAddButton(list);

            foreach (var state in m_TargetStates)
            {
                // Only add references to lists that are smaller or equal to the combined references list size.
                if (state.defineConstraints.Count < list.count)
                {
                    int index = Math.Min(list.index, state.defineConstraints.Count());
                    state.defineConstraints.Insert(index, list.list[list.index] as DefineConstraint);
                }
            }
        }

        private void RemoveDefineConstraintListElement(ReorderableList list)
        {
            foreach (var state in m_TargetStates)
                state.defineConstraints.RemoveAt(list.index);

            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        }

        private void RemovePrecompiledReferenceListElement(ReorderableList list)
        {
            foreach (var state in m_TargetStates)
                state.precompiledReferences.RemoveAt(list.index);

            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        }

        private void AddPrecompiledReferenceListElement(ReorderableList list)
        {
            ReorderableList.defaultBehaviours.DoAddButton(list);

            foreach (var state in m_TargetStates)
            {
                // Only add references to lists that are smaller or equal to the combined references list size.
                if (state.precompiledReferences.Count <= list.count)
                {
                    int index = Math.Min(list.index, state.precompiledReferences.Count());
                    state.precompiledReferences.Insert(index, list.list[list.index] as PrecompiledReference);
                }
            }
        }

        private void AddVersionDefineListElement(ReorderableList list)
        {
            ReorderableList.defaultBehaviours.DoAddButton(list);

            foreach (var state in m_TargetStates)
            {
                if (state.versionDefines.Count < list.count)
                {
                    int index = Math.Min(list.index, state.versionDefines.Count());
                    state.versionDefines.Insert(index, list.list[list.index] as VersionDefine);
                }
            }
        }

        private void RemoveVersionDefineListElement(ReorderableList list)
        {
            foreach (var state in m_TargetStates)
                state.versionDefines.RemoveAt(list.index);

            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        }

        private void DrawVersionDefineListElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var list = m_VersionDefineList.list;
            var versionDefine = list[index] as VersionDefine;

            rect.height -= EditorGUIUtility.standardVerticalSpacing;

            string noValue = L10n.Tr("(Missing)");

            var assetPathsMetaData = EditorCompilationInterface.Instance.GetAssetPathsMetaData().SelectMany(x => x.VersionMetaDatas.Select(y => y.Name)).ToList();

            if (!string.IsNullOrEmpty(versionDefine.name) && !assetPathsMetaData.Contains(versionDefine.name))
            {
                assetPathsMetaData.Add(versionDefine.name);
            }

            assetPathsMetaData.Insert(0, "Select...");
            int indexOfSelected = 0;
            if (!string.IsNullOrEmpty(versionDefine.name))
            {
                indexOfSelected = assetPathsMetaData.IndexOf(versionDefine.name);
            }

            bool mixed = versionDefine.displayValue == MixedBool.Mixed;
            EditorGUI.showMixedValue = mixed;

            var elementRect = new Rect(rect);
            elementRect.height = EditorGUIUtility.singleLineHeight;
            int popupIndex = EditorGUI.Popup(elementRect, GUIContent.Temp("Resource", "Select the package or module that you want to set a define for."), indexOfSelected, assetPathsMetaData.ToArray());
            versionDefine.name = assetPathsMetaData[popupIndex];

            elementRect.y += EditorGUIUtility.singleLineHeight;
            versionDefine.define = EditorGUI.TextField(elementRect,  GUIContent.Temp("Define", "Specify the name you want this define to have. This define is only set if the expression below returns true."), versionDefine.define);

            elementRect.y += EditorGUIUtility.singleLineHeight;
            versionDefine.expression = EditorGUI.TextField(elementRect, GUIContent.Temp("Expression", "Specify the semantic version of your chosen module or package. You must use mathematical interval notation."), versionDefine.expression);

            string expressionOutcome = null;
            if (!string.IsNullOrEmpty(versionDefine.expression))
            {
                try
                {
                    var expression = m_SemVersionRanges.GetExpression(versionDefine.expression);
                    expressionOutcome = expression.AppliedRule;
                }
                catch (Exception)
                {
                    expressionOutcome = "Invalid";
                }
            }

            elementRect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(elementRect, Styles.expressionOutcome, GUIContent.Temp(expressionOutcome));

            EditorGUI.showMixedValue = false;

            if (!string.IsNullOrEmpty(name) && name != noValue)
            {
                versionDefine.name = name;
                foreach (var state in m_TargetStates)
                {
                    state.versionDefines[index] = versionDefine;
                }
            }
        }

        private void DrawPrecompiledReferenceListElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var list = m_PrecompiledReferencesList.list;
            var precompiledReference = list[index] as PrecompiledReference;

            rect.height -= EditorGUIUtility.standardVerticalSpacing;
            GUIContent label = GUIContent.Temp(precompiledReference.name);

            bool mixed = precompiledReference.displayValue == MixedBool.Mixed;
            EditorGUI.showMixedValue = mixed;

            var currentSelectedPrecompiledReferences = m_State.precompiledReferences.Select(x => x.fileName);

            var precompiledAssemblyNames = CompilationPipeline.GetPrecompiledAssemblyNames()
                .Where(x => !currentSelectedPrecompiledReferences.Contains(x));

            var contextList = precompiledAssemblyNames
                .OrderBy(x => x).ToList();

            if (!precompiledReference.precompiled.HasValue)
            {
                contextList.Insert(0, L10n.Tr("None"));

                if (!precompiledAssemblyNames.Any())
                {
                    label = EditorGUIUtility.TrTempContent("No possible references");
                }
            }
            else
            {
                contextList.Insert(0, precompiledReference.fileName);
            }

            int currentlySelectedIndex = 0;
            if (precompiledReference.precompiled.HasValue)
            {
                currentlySelectedIndex = Array.IndexOf(contextList.ToArray(), precompiledReference.fileName);
            }

            EditorGUI.BeginDisabled(!precompiledReference.precompiled.HasValue && !string.IsNullOrEmpty(precompiledReference.name));
            int selectedIndex = EditorGUI.Popup(rect, label, currentlySelectedIndex, contextList.ToArray());
            EditorGUI.EndDisabled();

            if (selectedIndex > 0)
            {
                var selectedAssemblyName = contextList[selectedIndex];
                precompiledReference.precompiled = EditorCompilationInterface.Instance.GetAllPrecompiledAssemblies()
                    .Single(x => AssetPath.GetFileName(x.Path) == selectedAssemblyName);
                precompiledReference.name = selectedAssemblyName;
            }

            EditorGUI.showMixedValue = false;
        }

        void UpdateCombinedCompatibility()
        {
            // Merge platform compability for all targets
            m_State.compatibleWithAnyPlatform = m_TargetStates[0].compatibleWithAnyPlatform;

            var platforms = Compilation.CompilationPipeline.GetAssemblyDefinitionPlatforms();
            m_State.platformCompatibility = new MixedBool[platforms.Length];

            var optionalUnityReferences = CustomScriptAssembly.OptinalUnityAssemblies;
            m_State.optionalUnityReferences = new MixedBool[optionalUnityReferences.Length];

            Array.Copy(m_TargetStates[0].platformCompatibility, m_State.platformCompatibility, platforms.Length);
            Array.Copy(m_TargetStates[0].optionalUnityReferences, m_State.optionalUnityReferences, optionalUnityReferences.Length);

            for (int i = 1; i < m_TargetStates.Length; ++i)
            {
                var targetState = m_TargetStates[i];

                if (m_State.compatibleWithAnyPlatform != MixedBool.Mixed)
                {
                    if (m_State.compatibleWithAnyPlatform != targetState.compatibleWithAnyPlatform)
                        m_State.compatibleWithAnyPlatform = MixedBool.Mixed;
                }

                for (int j = 0; j < m_State.optionalUnityReferences.Length; ++j)
                {
                    if (m_State.optionalUnityReferences[j] != MixedBool.Mixed)
                    {
                        if (m_State.optionalUnityReferences[j] != targetState.optionalUnityReferences[j])
                            m_State.optionalUnityReferences[j] = MixedBool.Mixed;
                    }
                }

                for (int p = 0; p < platforms.Length; ++p)
                {
                    // If already set to mixed, continue.
                    if (m_State.platformCompatibility[p] == MixedBool.Mixed)
                        continue;

                    // If different from existing value, set to mixed.
                    if (m_State.platformCompatibility[p] != targetState.platformCompatibility[p])
                        m_State.platformCompatibility[p] = MixedBool.Mixed;
                }
            }
        }

        static AssemblyDefintionState LoadAssemblyDefintionState(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);

            if (asset == null)
                return null;

            var data = CustomScriptAssemblyData.FromJsonNoFieldValidation(asset.text);

            if (data == null)
                return null;

            try
            {
                data.ValidateFields();
            }
            catch (Exception e)
            {
                Debug.LogException(e, asset);
            }

            var state = new AssemblyDefintionState();

            state.asset = asset;
            state.name = data.name;
            state.references = new List<AssemblyDefinitionReference>();
            state.precompiledReferences = new List<PrecompiledReference>();
            state.defineConstraints = new List<DefineConstraint>();
            state.versionDefines = new List<VersionDefine>();
            state.autoReferenced = ToMixedBool(data.autoReferenced);
            state.allowUnsafeCode = ToMixedBool(data.allowUnsafeCode);
            state.overrideReferences = ToMixedBool(data.overrideReferences);

            // If the .asmdef has no references (true for newly created .asmdef), then use GUIDs.
            // Otherwise do not use GUIDs. This value might be changed below if any reference is a GUID.
            bool hasReferences = (data.references != null && data.references.Length > 0);
            state.useGUIDs = ToMixedBool(!hasReferences);

            if (data.versionDefines != null)
            {
                foreach (var versionDefine in data.versionDefines)
                {
                    try
                    {
                        if (!SymbolNameRestrictions.IsValid(versionDefine.define))
                        {
                            throw new AssemblyDefinitionException($"Invalid version define {versionDefine.define}", path);
                        }

                        state.versionDefines.Add(new VersionDefine
                        {
                            name = versionDefine.name,
                            expression = versionDefine.expression,
                            define = versionDefine.define,
                            displayValue = MixedBool.False,
                        });
                    }
                    catch (AssemblyDefinitionException e)
                    {
                        Debug.LogException(e, asset);
                        state.modified = true;
                    }
                }
            }

            if (data.defineConstraints != null)
            {
                foreach (var defineConstaint in data.defineConstraints)
                {
                    try
                    {
                        var symbolName = defineConstaint.StartsWith(DefineConstraintsHelper.Not) ? defineConstaint.Substring(1) : defineConstaint;
                        if (!SymbolNameRestrictions.IsValid(symbolName))
                        {
                            throw new AssemblyDefinitionException($"Invalid define constraint {symbolName}", path);
                        }

                        state.defineConstraints.Add(new DefineConstraint
                        {
                            name = defineConstaint,
                            displayValue = MixedBool.False,
                        });
                    }
                    catch (AssemblyDefinitionException e)
                    {
                        Debug.LogException(e, asset);
                        state.modified = true;
                    }
                }
            }

            if (data.references != null)
            {
                foreach (var reference in data.references)
                {
                    try
                    {
                        var assemblyDefinitionFile = new AssemblyDefinitionReference
                        {
                            name = reference,
                            serializedReference = reference
                        };

                        // If any references is a GUID, use GUIDs.
                        if (CompilationPipeline.GetAssemblyDefinitionReferenceType(reference) == AssemblyDefinitionReferenceType.Guid)
                        {
                            state.useGUIDs = MixedBool.True;
                        }

                        var referencePath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyReference(reference);

                        if (!string.IsNullOrEmpty(referencePath))
                        {
                            assemblyDefinitionFile.asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(referencePath);
                            assemblyDefinitionFile.data = CustomScriptAssemblyData.FromJson(assemblyDefinitionFile.asset.text);
                            assemblyDefinitionFile.name = assemblyDefinitionFile.data.name;
                        }

                        assemblyDefinitionFile.displayValue = MixedBool.False;
                        state.references.Add(assemblyDefinitionFile);
                    }
                    catch (AssemblyDefinitionException e)
                    {
                        UnityEngine.Debug.LogException(e, asset);
                        state.references.Add(new AssemblyDefinitionReference());
                        state.modified = true;
                    }
                }
            }

            var nameToPrecompiledReference = EditorCompilationInterface.Instance.GetAllPrecompiledAssemblies()
                .Where(x => (x.Flags & AssemblyFlags.UserAssembly) == AssemblyFlags.UserAssembly)
                .ToDictionary(x => AssetPath.GetFileName(x.Path), x => x);
            foreach (var precompiledReferenceName in data.precompiledReferences ?? Enumerable.Empty<String>())
            {
                try
                {
                    var precompiledReference = new PrecompiledReference
                    {
                        name = precompiledReferenceName,
                    };

                    var precompiledAssemblyPossibleReference = nameToPrecompiledReference.ContainsKey(precompiledReferenceName);
                    if (precompiledAssemblyPossibleReference)
                    {
                        precompiledReference.precompiled = nameToPrecompiledReference[precompiledReferenceName];
                    }

                    precompiledReference.displayValue = MixedBool.True;
                    state.precompiledReferences.Add(precompiledReference);
                }
                catch (AssemblyDefinitionException e)
                {
                    Debug.LogException(e, asset);
                    state.precompiledReferences.Add(new PrecompiledReference());
                    state.modified = true;
                }
            }

            var platforms = CompilationPipeline.GetAssemblyDefinitionPlatforms();
            state.platformCompatibility = new MixedBool[platforms.Length];

            var OptinalUnityAssemblies = CustomScriptAssembly.OptinalUnityAssemblies;
            state.optionalUnityReferences = new MixedBool[OptinalUnityAssemblies.Length];

            if (data.optionalUnityReferences != null)
            {
                for (int i = 0; i < OptinalUnityAssemblies.Length; i++)
                {
                    var optionalUnityReferences = OptinalUnityAssemblies[i].OptionalUnityReferences.ToString();
                    var any = data.optionalUnityReferences.Any(x => x == optionalUnityReferences);
                    if (any)
                    {
                        state.optionalUnityReferences[i] = MixedBool.True;
                    }
                }
            }

            state.compatibleWithAnyPlatform = MixedBool.True;
            string[] dataPlatforms = null;

            if (data.includePlatforms != null && data.includePlatforms.Length > 0)
            {
                state.compatibleWithAnyPlatform = MixedBool.False;
                dataPlatforms = data.includePlatforms;
            }
            else if (data.excludePlatforms != null && data.excludePlatforms.Length > 0)
            {
                state.compatibleWithAnyPlatform = MixedBool.True;
                dataPlatforms = data.excludePlatforms;
            }

            if (dataPlatforms != null)
                foreach (var platform in dataPlatforms)
                {
                    var platformIndex = GetPlatformIndex(platforms, platform);
                    state.platformCompatibility[platformIndex] = MixedBool.True;
                }

            return state;
        }

        static AssemblyDefinitionReference CreateAssemblyDefinitionReference(string assemblyName)
        {
            var assemblyDefinitionFile = new AssemblyDefinitionReference();
            var path = Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            if (string.IsNullOrEmpty(path))
                throw new System.Exception(string.Format("Could not get assembly definition filename for assembly '{0}'", assemblyName));

            assemblyDefinitionFile.asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);

            if (assemblyDefinitionFile.asset == null)
                throw new FileNotFoundException(string.Format("Assembly definition file '{0}' not found", assemblyDefinitionFile.path), assemblyDefinitionFile.path);

            assemblyDefinitionFile.data = CustomScriptAssemblyData.FromJson(assemblyDefinitionFile.asset.text);

            return assemblyDefinitionFile;
        }

        static void SaveAndUpdateAssemblyDefinitionStates(AssemblyDefintionState combinedState, AssemblyDefintionState[] states)
        {
            int combinedReferenceCount = combinedState.references.Count();
            int combinedDefineConstraintsCount = combinedState.defineConstraints.Count();
            int combinedPrecompiledReferenceCount = combinedState.precompiledReferences.Count();
            int combinedVersionDefinesCount = combinedState.versionDefines.Count();

            // Update the name if there is only one file selected.
            if (states.Length == 1)
                states[0].name = combinedState.name;

            foreach (var state in states)
            {
                if (combinedState.allowUnsafeCode != MixedBool.Mixed)
                {
                    state.allowUnsafeCode = combinedState.allowUnsafeCode;
                }

                if (combinedState.overrideReferences != MixedBool.Mixed)
                    state.overrideReferences = combinedState.overrideReferences;

                if (combinedState.autoReferenced != MixedBool.Mixed)
                    state.autoReferenced = combinedState.autoReferenced;

                if (combinedState.useGUIDs != MixedBool.Mixed)
                    state.useGUIDs = combinedState.useGUIDs;

                for (int i = 0; i < combinedReferenceCount; ++i)
                {
                    if (combinedState.references[i].displayValue != MixedBool.Mixed)
                        state.references[i] = combinedState.references[i];
                }

                for (int i = 0; i < combinedDefineConstraintsCount; ++i)
                {
                    if (combinedState.defineConstraints[i].displayValue != MixedBool.Mixed)
                        state.defineConstraints[i] = combinedState.defineConstraints[i];
                }

                for (int i = 0; i < combinedVersionDefinesCount; ++i)
                {
                    if (combinedState.versionDefines[i].displayValue != MixedBool.Mixed)
                        state.versionDefines[i] = combinedState.versionDefines[i];
                }

                for (int i = 0; i < combinedPrecompiledReferenceCount; ++i)
                {
                    if (combinedState.precompiledReferences[i].displayValue != MixedBool.Mixed)
                        state.precompiledReferences[i] = combinedState.precompiledReferences[i];
                }

                if (combinedState.compatibleWithAnyPlatform != MixedBool.Mixed)
                    state.compatibleWithAnyPlatform = combinedState.compatibleWithAnyPlatform;

                for (int i = 0; i < combinedState.platformCompatibility.Length; ++i)
                {
                    if (combinedState.platformCompatibility[i] != MixedBool.Mixed)
                        state.platformCompatibility[i] = combinedState.platformCompatibility[i];
                }

                for (int i = 0; i < combinedState.optionalUnityReferences.Length; ++i)
                {
                    if (combinedState.platformCompatibility[i] != MixedBool.Mixed)
                        state.optionalUnityReferences[i] = combinedState.optionalUnityReferences[i];
                }

                SaveAssemblyDefinitionState(state);
            }

            combinedState.modified = false;
        }

        static void SaveAssemblyDefinitionState(AssemblyDefintionState state)
        {
            var references = state.references;
            var platforms = CompilationPipeline.GetAssemblyDefinitionPlatforms();
            var OptinalUnityAssemblies = CustomScriptAssembly.OptinalUnityAssemblies;

            CustomScriptAssemblyData data = new CustomScriptAssemblyData();

            data.name = state.name;

            if (state.useGUIDs == MixedBool.True)
            {
                data.references = references.Select(r =>
                {
                    var guid = AssetDatabase.AssetPathToGUID(r.path);

                    if (string.IsNullOrEmpty(guid))
                        return r.serializedReference;

                    return CompilationPipeline.GUIDToAssemblyDefinitionReferenceGUID(guid);
                }).ToArray();
            }
            else if (state.useGUIDs == MixedBool.False)
            {
                data.references = references.Select(r => r.name).ToArray();
            }
            else
            {
                data.references = references.Select(r => r.serializedReference).ToArray();
            }

            data.defineConstraints = state.defineConstraints
                .Where(x => !string.IsNullOrEmpty(x.name))
                .Select(r => r.name)
                .ToArray();

            data.versionDefines = state.versionDefines.Select(x => new UnityEditor.Scripting.ScriptCompilation.VersionDefine
            {
                name = x.name,
                expression = x.expression,
                define = x.define,
            }).ToArray();

            data.autoReferenced = state.autoReferenced == MixedBool.True;
            data.overrideReferences = state.overrideReferences == MixedBool.True;

            data.precompiledReferences = state.precompiledReferences
                .Select(r => r.name).ToArray();

            List<string> optionalUnityReferences = new List<string>();

            for (int i = 0; i < OptinalUnityAssemblies.Length; i++)
            {
                if (state.optionalUnityReferences[i] == MixedBool.True)
                    optionalUnityReferences.Add(OptinalUnityAssemblies[i].OptionalUnityReferences.ToString());
            }

            data.optionalUnityReferences = optionalUnityReferences.ToArray();

            data.allowUnsafeCode = ToBool(state.allowUnsafeCode);

            List<string> dataPlatforms = new List<string>();

            for (int i = 0; i < platforms.Length; ++i)
            {
                if (state.platformCompatibility[i] == MixedBool.True)
                    dataPlatforms.Add(platforms[i].Name);
            }

            if (dataPlatforms.Any())
            {
                if (state.compatibleWithAnyPlatform == MixedBool.True)
                    data.excludePlatforms = dataPlatforms.ToArray();
                else
                    data.includePlatforms = dataPlatforms.ToArray();
            }

            var json = CustomScriptAssemblyData.ToJson(data);
            File.WriteAllText(state.path, json);
            state.modified = false;

            AssetDatabase.ImportAsset(state.path);
        }

        static int GetPlatformIndex(AssemblyDefinitionPlatform[] platforms, string name)
        {
            for (int i = 0; i < platforms.Length; ++i)
            {
                if (string.Equals(platforms[i].Name, name, System.StringComparison.InvariantCultureIgnoreCase))
                    return i;
            }

            throw new System.ArgumentException(string.Format("Unknown platform '{0}'", name), name);
        }

        void DrawReferenceListElement(Rect rect, int index, bool selected, bool focused)
        {
            var list = m_ReferencesList.list;
            var assemblyDefinitionFile = list[index] as AssemblyDefinitionReference;

            rect.height -= EditorGUIUtility.standardVerticalSpacing;
            var label = assemblyDefinitionFile.name != null ? assemblyDefinitionFile.name : "(Missing Reference)";
            var asset = assemblyDefinitionFile.asset;

            bool mixed = assemblyDefinitionFile.displayValue == MixedBool.Mixed;
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginDisabled(assemblyDefinitionFile.name != null && asset == null);
            assemblyDefinitionFile.asset = EditorGUI.ObjectField(rect, mixed ? "(Multiple Values)" : label, asset, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            EditorGUI.EndDisabled();
            EditorGUI.showMixedValue = false;

            if (asset != assemblyDefinitionFile.asset && assemblyDefinitionFile.asset != null)
            {
                assemblyDefinitionFile.data = CustomScriptAssemblyData.FromJson(assemblyDefinitionFile.asset.text);
                assemblyDefinitionFile.name = assemblyDefinitionFile.data.name;

                foreach (var state in m_TargetStates)
                    state.references[index] = assemblyDefinitionFile;
            }
        }

        void AddReferenceListElement(ReorderableList list)
        {
            ReorderableList.defaultBehaviours.DoAddButton(list);

            foreach (var state in m_TargetStates)
            {
                // Only add references to lists that are smaller or equal to the combined references list size.
                if (state.references.Count <= list.count)
                {
                    int index = Math.Min(list.index, state.references.Count());
                    state.references.Insert(index, list.list[list.index] as AssemblyDefinitionReference);
                }
            }
        }

        void RemoveReferenceListElement(ReorderableList list)
        {
            foreach (var state in m_TargetStates)
                state.references.RemoveAt(list.index);

            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        }
    }
}
