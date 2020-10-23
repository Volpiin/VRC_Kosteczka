using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.EditorBindings;
using VRC.Udon.EditorBindings.Interfaces;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.UAssembly.Interfaces;

namespace VRC.Udon.Editor
{
    public class UdonEditorManager : IUdonEditorInterface
    {
        #region Singleton

        private static UdonEditorManager _instance;
        public static UdonEditorManager Instance => _instance ?? (_instance = new UdonEditorManager());

        #endregion

        #region Build Preprocessor Class

        private class UdonBuildPreprocessor : IProcessSceneWithReport
        {
            public int callbackOrder => 0;

            public void OnProcessScene(Scene scene, BuildReport report)
            {
                PopulateSceneSerializedProgramAssetReferences(scene);
                PopulateAllPrefabSerializedProgramAssetReferences();
            }
        }

        #endregion

        #region Public Events

        public event Action WantRepaint;

        #endregion

        #region Private Constants

        private const double REFRESH_QUEUE_WAIT_PERIOD = 5.0;

        #endregion

        #region Private Fields

        private readonly UdonEditorInterface _udonEditorInterface;

        private readonly HashSet<AbstractUdonProgramSource> _programSourceRefreshQueue = new HashSet<AbstractUdonProgramSource>();

        #endregion

        #region Initialization

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            _instance = new UdonEditorManager();
        }

        #endregion

        #region Constructors

        private UdonEditorManager()
        {
            _udonEditorInterface = new UdonEditorInterface();
            _udonEditorInterface.AddTypeResolver(new UdonBehaviourTypeResolver());

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        #endregion

        #region UdonBehaviour and ProgramSource Refresh

        public void QueueAndRefreshProgram(AbstractUdonProgramSource programSource)
        {
            QueueProgramSourceRefresh(programSource);
            RefreshQueuedProgramSources();
        }

        public void RefreshQueuedProgramSources()
        {
            foreach(AbstractUdonProgramSource programSource in _programSourceRefreshQueue)
            {
                if(programSource == null)
                {
                    return;
                }

                try
                {
                    programSource.RefreshProgram();
                }
                catch(Exception e)
                {
                    Debug.LogError($"Failed to refresh program '{programSource.name}' due to exception '{e}'.");
                }
            }

            _programSourceRefreshQueue.Clear();

            WantRepaint?.Invoke();
        }

        public bool IsProgramSourceRefreshQueued(AbstractUdonProgramSource programSource)
        {
            if(_programSourceRefreshQueue.Count <= 0)
            {
                return false;
            }

            if(!_programSourceRefreshQueue.Contains(programSource))
            {
                return false;
            }

            return true;
        }

        public void QueueProgramSourceRefresh(AbstractUdonProgramSource programSource)
        {
            if(Application.isPlaying)
            {
                return;
            }

            if(programSource == null)
            {
                return;
            }

            if(IsProgramSourceRefreshQueued(programSource))
            {
                return;
            }

            _programSourceRefreshQueue.Add(programSource);
        }

        public void CancelQueuedProgramSourceRefresh(AbstractUdonProgramSource programSource)
        {
            if(programSource == null)
            {
                return;
            }

            if(_programSourceRefreshQueue.Contains(programSource))
            {
                _programSourceRefreshQueue.Remove(programSource);
            }
        }

        [PublicAPI]
        public static void PopulateAllPrefabSerializedProgramAssetReferences()
        {
            foreach(string prefabPath in GetAllPrefabAssetPaths())
            {
                PopulatePrefabSerializedProgramAssetReferences(prefabPath);
            }
        }

        private static void PopulatePrefabSerializedProgramAssetReferences(string prefabPath)
        {
            using(EditPrefabAssetScope editScope = new EditPrefabAssetScope(prefabPath))
            {
                if(!editScope.IsEditable)
                {
                    return;
                }

                UdonBehaviour[] udonBehaviours = editScope.PrefabRoot.GetComponentsInChildren<UdonBehaviour>();
                if(udonBehaviours.Length <= 0)
                {
                    return;
                }

                foreach(UdonBehaviour udonBehaviour in udonBehaviours)
                {
                    PopulateSerializedProgramAssetReference(udonBehaviour);
                }

                editScope.MarkDirty();
            }
        }

        #endregion

        #region Scene Manager Callbacks

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshQueuedProgramSources();
            PopulateSceneSerializedProgramAssetReferences(scene);
        }

        private void OnSceneSaving(Scene scene, string _)
        {
            RefreshQueuedProgramSources();
            PopulateSceneSerializedProgramAssetReferences(scene);
        }

        private static void PopulateSceneSerializedProgramAssetReferences(Scene scene)
        {
            foreach(GameObject sceneGameObject in scene.GetRootGameObjects())
            {
                foreach(UdonBehaviour udonBehaviour in sceneGameObject.GetComponentsInChildren<UdonBehaviour>())
                {
                    PopulateSerializedProgramAssetReference(udonBehaviour);
                }
            }
        }

        private static void PopulateSerializedProgramAssetReference(UdonBehaviour udonBehaviour)
        {
            SerializedObject serializedUdonBehaviour = new SerializedObject(udonBehaviour);
            SerializedProperty programSourceSerializedProperty = serializedUdonBehaviour.FindProperty("programSource");
            SerializedProperty serializedProgramAssetSerializedProperty = serializedUdonBehaviour.FindProperty("serializedProgramAsset");

            if(!(programSourceSerializedProperty.objectReferenceValue is AbstractUdonProgramSource abstractUdonProgramSource))
            {
                return;
            }

            if(abstractUdonProgramSource == null)
            {
                return;
            }

            serializedProgramAssetSerializedProperty.objectReferenceValue = abstractUdonProgramSource.SerializedProgramAsset;
            serializedUdonBehaviour.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        #region PlayMode Callback

        private void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if(playModeStateChange != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            for(int index = 0; index < SceneManager.sceneCount; index++)
            {
                PopulateSceneSerializedProgramAssetReferences(SceneManager.GetSceneAt(index));
            }
        }

        #endregion

        #region IUdonEditorInterface Methods

        public IUdonVM ConstructUdonVM()
        {
            return _udonEditorInterface.ConstructUdonVM();
        }

        public IUdonProgram Assemble(string assembly)
        {
            return _udonEditorInterface.Assemble(assembly);
        }

        public IUdonWrapper GetWrapper()
        {
            return _udonEditorInterface.GetWrapper();
        }

        public IUdonHeap ConstructUdonHeap()
        {
            return _udonEditorInterface.ConstructUdonHeap();
        }

        public IUdonHeap ConstructUdonHeap(uint heapSize)
        {
            return _udonEditorInterface.ConstructUdonHeap(heapSize);
        }

        public string CompileGraph(
            IUdonCompilableGraph graph, INodeRegistry nodeRegistry,
            out Dictionary<string, (string uid, string fullName, int index)> linkedSymbols,
            out Dictionary<string, (object value, Type type)> heapDefaultValues
        )
        {
            return _udonEditorInterface.CompileGraph(graph, nodeRegistry, out linkedSymbols, out heapDefaultValues);
        }

        public Type GetTypeFromTypeString(string typeString)
        {
            return _udonEditorInterface.GetTypeFromTypeString(typeString);
        }

        public void AddTypeResolver(IUAssemblyTypeResolver typeResolver)
        {
            _udonEditorInterface.AddTypeResolver(typeResolver);
        }

        public string[] DisassembleProgram(IUdonProgram program)
        {
            return _udonEditorInterface.DisassembleProgram(program);
        }

        public string DisassembleInstruction(IUdonProgram program, ref uint offset)
        {
            return _udonEditorInterface.DisassembleInstruction(program, ref offset);
        }

        public UdonNodeDefinition GetNodeDefinition(string identifier)
        {
            return _udonEditorInterface.GetNodeDefinition(identifier);
        }

        public IEnumerable<UdonNodeDefinition> GetNodeDefinitions()
        {
            return _udonEditorInterface.GetNodeDefinitions();
        }

        public Dictionary<string, INodeRegistry> GetNodeRegistries()
        {
            return _udonEditorInterface.GetNodeRegistries();
        }

        private IReadOnlyDictionary<string, ReadOnlyCollection<KeyValuePair<string, INodeRegistry>>> _topRegistries;

        public IReadOnlyDictionary<string, ReadOnlyCollection<KeyValuePair<string, INodeRegistry>>> GetTopRegistries()
        {
            if (_topRegistries != null) return _topRegistries;

            var topRegistries = new Dictionary<string, List<KeyValuePair<string, INodeRegistry>>>()
            {
                {"System", new List<KeyValuePair<string, INodeRegistry>>()},
                {"Udon", new List<KeyValuePair<string, INodeRegistry>>()},
                {"VRC", new List<KeyValuePair<string, INodeRegistry>>()},
                {"UnityEngine", new List<KeyValuePair<string, INodeRegistry>>()},
            };

            // Go through each node registry and put it in the right parent registry
            foreach (KeyValuePair<string, INodeRegistry> nodeRegistry in GetNodeRegistries())
            {
                if (nodeRegistry.Key.StartsWith("System"))
                {
                    topRegistries["System"].Add(nodeRegistry);
                }
                else if (nodeRegistry.Key.StartsWith("Udon"))
                {
                    topRegistries["Udon"].Add(nodeRegistry);
                }
                else if (nodeRegistry.Key.StartsWith("VRC"))
                {
                    topRegistries["VRC"].Add(nodeRegistry);
                }
                else if (nodeRegistry.Key.StartsWith("UnityEngine"))
                {
                    topRegistries["UnityEngine"].Add(nodeRegistry);
                }
                else
                {
                    Debug.Log(nodeRegistry.Key);
                }
            }

            // Save result as cached variable
            _topRegistries = new ReadOnlyDictionary<string, ReadOnlyCollection<KeyValuePair<string, INodeRegistry>>>
            (
                topRegistries.ToDictionary(entry => entry.Key, entry => entry.Value.AsReadOnly())
            );
            
            // return cached version
           return _topRegistries;
        }

        private Dictionary<string, INodeRegistry> _registryLookup;

        private void CacheRegistryLookup()
        {
            _registryLookup = new Dictionary<string, INodeRegistry>();

            foreach (KeyValuePair<string, INodeRegistry> topRegistry in GetNodeRegistries())
            {
                // save top-level registry. do we need to do this? probably not
                _registryLookup.Add(topRegistry.Key, topRegistry.Value);

                foreach (KeyValuePair<string, INodeRegistry> registry in topRegistry.Value.GetNodeRegistries())
                {
                    _registryLookup.Add(registry.Key, registry.Value);
                }
            }
        }

        public bool TryGetRegistry(string name, out INodeRegistry registry)
        {
            if(_registryLookup == null)
            {
                CacheRegistryLookup();
            }
            return _registryLookup.TryGetValue(name, out registry);
        }

        public IEnumerable<UdonNodeDefinition> GetNodeDefinitions(string baseIdentifier)
        {
            return _udonEditorInterface.GetNodeDefinitions(baseIdentifier);
        }

        #endregion

        #region Prefab Utilities

        private static IEnumerable<string> GetAllPrefabAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.EndsWith(".prefab"))
                .Where(path => path.StartsWith("Assets"));
        }

        private class EditPrefabAssetScope : IDisposable
        {
            private readonly string _assetPath;
            private readonly GameObject _prefabRoot;
            public GameObject PrefabRoot => _disposed ? null : _prefabRoot;

            private readonly bool _isEditable;
            public bool IsEditable => !_disposed && _isEditable;

            private bool _dirty = false;
            private bool _disposed;

            public EditPrefabAssetScope(string assetPath)
            {
                _assetPath = assetPath;
                _prefabRoot = PrefabUtility.LoadPrefabContents(_assetPath);
                _isEditable = !PrefabUtility.IsPartOfImmutablePrefab(_prefabRoot);
            }

            public void MarkDirty()
            {
                _dirty = true;
            }

            public void Dispose()
            {
                if(_disposed)
                {
                    return;
                }

                _disposed = true;

                if(_dirty)
                {
                    try
                    {
                        PrefabUtility.SaveAsPrefabAsset(_prefabRoot, _assetPath);
                    }
                    catch(Exception e)
                    {
                        Debug.LogError($"Failed to save changes to prefab at '{_assetPath}' due to exception '{e}'.");
                    }
                }

                PrefabUtility.UnloadPrefabContents(_prefabRoot);
            }
        }

        #endregion
    }
}