using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class UScene
    {
        protected enum SceneState
        {
            Ready,
            Loading,
            Loaded,
            Unloading,
        }

        private UAsset _asset;
        private LoadSceneMode _mode;
        private SceneState _state = SceneState.Ready;

#if UNITY_EDITOR
        private Scene _scene;
#endif
        private List<Action<UScene>> _callbacks = new List<Action<UScene>>();

        public event Action<UScene> completed
        {
            add
            {
                if (_state == SceneState.Loaded)
                {
                    value(this);
                }
                else
                {
                    _callbacks.Add(value);
                }
            }

            remove
            {
                _callbacks.Remove(value);
            }
        }

        public UScene(UAsset asset)
        {
            _asset = asset;
            _asset.completed += OnAssetCompleted;
        }

        private void OnAssetCompleted(UAsset asset)
        {
            if (_state == SceneState.Loading && asset.isLoaded)
            {
                JobScheduler.DispatchCoroutine(_LoadAsync());
            }
        }

        public UScene Load()
        {
            if (_state == SceneState.Ready)
            {
                _state = SceneState.Loading;
                _mode = LoadSceneMode.Single;
                OnAssetCompleted(_asset);
            }
            return this;
        }

        public UScene LoadAdditive()
        {
            if (_state == SceneState.Ready)
            {
                _state = SceneState.Loading;
                _mode = LoadSceneMode.Additive;
                OnAssetCompleted(_asset);
            }
            return this;
        }

        private IEnumerator _LoadAsync()
        {
            AsyncOperation op = null;
#if UNITY_EDITOR
            // EditorSceneManager.LoadSceneInPlayMode 没有像文档里说的那样正常工作... 
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(_asset.assetPath, new LoadSceneParameters(_mode));
#else
            op = SceneManager.LoadSceneAsync(_asset.assetPath, _mode);
#endif
            if (op != null)
            {
                yield return op;
            }
            if (_state == SceneState.Loading)
            {
                _state = SceneState.Loaded;
                while (_callbacks.Count > 0)
                {
                    var callback = _callbacks[0];
                    _callbacks.RemoveAt(0);
                    callback(this);
                }
            }
            else if (_state == SceneState.Unloading)
            {
                Debug.LogWarning("未加载完成时已经请求卸载场景");
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.UnloadSceneAsync(_scene);
#else
                yield return SceneManager.UnloadSceneAsync(_asset.assetPath);
#endif
                _state = SceneState.Ready;
            }
        }

        private IEnumerator _UnloadAsync()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.UnloadSceneAsync(_scene);
#else
            yield return SceneManager.UnloadSceneAsync(_asset.assetPath);
#endif
            _state = SceneState.Ready;
        }

        public void UnloadScene()
        {
            if (_state != SceneState.Ready)
            {
                if (_state == SceneState.Loaded)
                {
                    _state = SceneState.Unloading;
                    JobScheduler.DispatchCoroutine(_UnloadAsync());
                }
                else
                {
                    if (_asset.isLoaded)
                    {
                        _state = SceneState.Unloading;
                    }
                    else
                    {
                        _state = SceneState.Ready;
                    }
                }
            }
        }
    }
}