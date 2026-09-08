using System.Collections.Generic;
using LowoUN.Module.Perf;
using LowoUN.Util;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PerfLevelHandler : SingletonSimple<PerfLevelHandler> {
    const string ToonSpawnShaderName = "Toon/ToonSpawn";
    const string OutlineLightModeTag = "SRPDefaultUnlit";
    bool outlinePassEnabled = true;

    PerfLevelType curPerfLevelType => PerfManager.Self.CurPerfLevelType;

    public void SetOutlinePassEnabled (bool enabled) {
        outlinePassEnabled = enabled;
        // var processedMaterials = new HashSet<Material>();
        // var enemyUnits = Object.FindObjectsOfType<CZ.EnemyUnit>(true);
        // foreach (var enemyUnit in enemyUnits) {
        //     if(enemyUnit)
        //         ApplyOutlinePassState(enemyUnit.gameObject, processedMaterials);
        // }
    }

    public void ApplyOutlinePassState (GameObject target) {
        ApplyOutlinePassState (target, null);
    }

    void ApplyOutlinePassState (GameObject target, HashSet<Material> processedMaterials) {
        if (!target)
            return;

        var renderers = target.GetComponentsInChildren<Renderer> (true);
        foreach (var targetRenderer in renderers) {
            if (!targetRenderer)
                continue;

            var sharedMaterials = targetRenderer.sharedMaterials;
            foreach (var material in sharedMaterials) {
                if (!material || !material.shader || material.shader.name != ToonSpawnShaderName)
                    continue;

                if (processedMaterials != null && !processedMaterials.Add (material))
                    continue;

                if (material.GetShaderPassEnabled (OutlineLightModeTag) == outlinePassEnabled)
                    continue;

                material.SetShaderPassEnabled (OutlineLightModeTag, outlinePassEnabled);
            }
        }
    }

    // 目前只有大厅需要 即时 响应 画质分档事件
    public void PerfLevChanged () {
        Check_SetManiCameraPostProcessAndFXAA ();
        ResetVFXLevel_FieldLayout_Lobby ();
    }
    // UniversalAdditionalCameraData _cameraData;
    // void UpdateCameraData() {
    //     _cameraData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
    //     _cameraData.enabled = false;
    //     _cameraData.renderPostProcessing = level > 0;
    //     _cameraData.renderShadows = level > 0;
    // }

    // 处理特效的高中低分档，场景预置 + 战斗即时动态生成
    // Awake时处理
    // 一旦找到目标对象，无需要递归遍历所有层级的子对象
    // -- 目前的规则是 1 场景预置的 在第一子级 2 动态生成的在 Con的第一子级
    public void SetVFXLevel_FieldLayout_Battle (Transform vfxContainer) {
        if (curPerfLevelType >= PerfLevelType.High)
            return;

        SetVFXLevel_Battle (vfxContainer);
    }
    // 特效，静态和动态的都调用此接口
    // 大厅中 不需要独立处理每一个 预置在场景上的特效
    // 大厅中没有动态生成的特效吧？？？
    public void SetVFXLevel_EffectData (Transform vfxContainer) {
        bool isForBattleScene = true; //GameController.Self.CurState == GameState.Battle || GameController.Self.GoState == GameState.Battle;
        if (!isForBattleScene)
            return;

        // LLog.Error ($"SetVFXLevel_EffectData curPerfLevelType:{curPerfLevelType}, vfxContainer:{vfxContainer.name}");
        if (curPerfLevelType >= PerfLevelType.High)
            return;

        var con = vfxContainer.Find ("Con");
        if (con != null)
            SetVFXLevel_Battle (con);
    }
    // 目前就是给战斗场景用的
    void SetVFXLevel_Battle (Transform vfxContainer) {
        foreach (Transform child in vfxContainer) {
            // Log.Console ($"vfxContainer:{vfxContainer.name},child.name:{child.name}");
            GameObject childObj = child.gameObject;
            // LLog.Log($"SetVFXLevel -- childObj:{childObj.name}");

            // if (childObj.name.Contains("QLTLow")) {
            //     childObj.SetActive(curPerfLevelType>=PerfLevelType.Low);
            // }
            if (curPerfLevelType == PerfLevelType.Low) {
                if (childObj.name.Contains ("QLTMiddle")) {
                    childObj.SetActive (false);
                } else if (childObj.name.Contains ("QLTHigh")) {
                    childObj.SetActive (false);
                }
            } else if (curPerfLevelType == PerfLevelType.Mid) {
                if (childObj.name.Contains ("QLTHigh")) {
                    childObj.SetActive (false);
                }
            }
        }
    }

    Transform FieldLayout_Lobby;
    public void SetVFXLevel_FieldLayout_Lobby (Transform vfxContainer) {
        FieldLayout_Lobby = vfxContainer;

        if (curPerfLevelType >= PerfLevelType.High)
            return;

        SetVFXLevel_Lobby (vfxContainer);
    }
    void ResetVFXLevel_FieldLayout_Lobby () {
        if (curPerfLevelType >= PerfLevelType.High)
            return;
        if (!FieldLayout_Lobby) {
            // LLog.Error($"ResetVFXLevel_FieldLayout_Lobby -- FieldLayout_Lobby is null");
            return;
        }

        SetVFXLevel_Lobby (FieldLayout_Lobby);
    }
    // 递归, 但是一旦找到 QLTMiddle QLTHigh 就不递归其内部了
    void SetVFXLevel_Lobby (Transform vfxContainer) {
        foreach (Transform child in vfxContainer) {
            // Log.Console ($"vfxContainer:{vfxContainer.name},child.name:{child.name}");
            GameObject childObj = child.gameObject;

            bool isMiddleLevelNode = childObj.name.Contains ("QLTMiddle");
            bool isHighLevelNode = childObj.name.Contains ("QLTHigh");
            bool isTargetLevelNode = isMiddleLevelNode || isHighLevelNode;

            // if (childObj.name.Contains("QLTLow")) {
            //     childObj.SetActive(curPerfLevelType>=PerfLevelType.Low);
            // }
            if (curPerfLevelType == PerfLevelType.Low) {
                if (isMiddleLevelNode || isHighLevelNode) {
                    // LLog.Error($"SetVFXLevel_Lobby -- childObj:{childObj.name}");
                    childObj.SetActive (false);
                }
            } else if (curPerfLevelType == PerfLevelType.Mid) {
                if (isHighLevelNode) {
                    // LLog.Error($"SetVFXLevel_Lobby -- childObj:{childObj.name}");
                    childObj.SetActive (false);
                }
            }

            if (isTargetLevelNode)
                continue;

            if (child.childCount > 0)
                SetVFXLevel_Lobby (child);
        }
    }

    GameObject curr_sceneVolume;
    public void Set_PostProcessing_Battle () {
        LLog.NOT_PRODUCTION_LOG ($"Set_PostProcessing_Battle curPerfLevelType:{curPerfLevelType}");
        if (!GameSettings._instance.IsEnableBattleVolume)
            return;

        if (curPerfLevelType >= PerfLevelType.High) {
            if (curr_sceneVolume == null) {
                const string resourcePath = "GlobalVolume_HighQuality";
                var res = Resources.Load<GameObject> (resourcePath);
                if (res == null) {
                    Debug.LogError ($"[Perf] Failed to load Resources/{resourcePath}.prefab");
                    return;
                }

                curr_sceneVolume = UnityEngine.Object.Instantiate (res);
                curr_sceneVolume.SetActive (!GameSettings.IsHideOrShowPostProcess);
            }
            return;
        }

        Disable_PostProcess_Battle ();
    }

    public void Disable_PostProcess_Battle () {
        if (!GameSettings._instance.IsEnableBattleVolume)
            return;

        if (curr_sceneVolume != null) {
            // curr_sceneVolume.SetActive(false);
            GameObject.Destroy (curr_sceneVolume);
        }
        curr_sceneVolume = null;
    }

    public void TEST_Toggle_PostProcess () {
        LLog.Green ($"[Perf] curPerfLevelType:{curPerfLevelType}");
        if (curPerfLevelType < PerfLevelType.High) {
            LLog.NOT_PRODUCTION_ERROR ($"[Perf] TEST_Toggle_PostProcess no post process for low/mid perf level");
            return;
        }

        if (curr_sceneVolume != null) {
            GameSettings.IsHideOrShowPostProcess = !GameSettings.IsHideOrShowPostProcess;
            curr_sceneVolume.gameObject.SetActive (!GameSettings.IsHideOrShowPostProcess);
        } else {
            LLog.NOT_PRODUCTION_ERROR ($"[Perf] no post processing asset had been loaded yet!");
        }
    }

    // 中高性能模式下，开启camera自身的后处理 和 FXAA 抗锯齿，低端机则关闭所有
    public void Check_SetManiCameraPostProcessAndFXAA () {
        if (curPerfLevelType is PerfLevelType.High or PerfLevelType.Mid)
            SetManiCameraPostProcessAndFXAA (true);
        else {
            if (GameSettings._instance.IsForceOpenAAOnLowQL)
                SetManiCameraPostProcessAndFXAA (true);
            else
                SetManiCameraPostProcessAndFXAA (false);
        }
    }
    // Login 和 CreateRole场景中，不需要响应即时刷新
    public void SetManiCameraPostProcessAndFXAA (bool isOpen) {
        var cam = Camera.main;

        if (cam == null) {
            // LLog.NOT_PRODUCTION_ERROR($"[Perf] SetManiCameraPostProcessAndFXAA main camera is null, Game State:{GameController.Self.CurState}");
            return;
        }

        UniversalAdditionalCameraData cameraData = cam.GetComponent<UniversalAdditionalCameraData> ();
        if (cameraData == null) {
            LLog.NOT_PRODUCTION_ERROR ("[Perf] SetManiCameraPostProcessAndFXAA cameraData is null");
            return;
        }

        cameraData.renderPostProcessing = isOpen;
        if (isOpen)
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; //FastApproximateAntialiasing
        else
            cameraData.antialiasing = AntialiasingMode.None;
    }
}