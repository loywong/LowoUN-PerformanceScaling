using LowoUN.Util;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LowoUN.Module.Perf {
    // 性能分为三档
    public enum PerfLevelType : byte {
        NONE = 0,
        Low = 1,
        Mid = 2,
        High = 3,
    }
    public class PerfManager : SingletonSimple<PerfManager> {
        // ------ 设备信息
        // [Header ("内存阈值配置")] "触发降级的内存阈值 GB")]
        readonly float memoryThresholdGB_1 = 4.0f;
        readonly float memoryThresholdGB_2 = 6.0f;
        // 设备内存状态缓存
        private float _deviceMemoryGB;
        public float DeviceMemoryGB => _deviceMemoryGB;

        private int cpuProcessorCount;
        public int CpuProcessorCount => cpuProcessorCount;
        private int cpuProcessorFrequency;
        public int CpuProcessorFrequency => cpuProcessorFrequency;
        private string cpuProcessorType;
        public string CpuProcessorType => cpuProcessorType;

        private string graphicsDeviceName;
        public string GraphicsDeviceName => graphicsDeviceName;

#if !SRV_ALIYUN_PRODUCTION
        public string perfLevDesc { private set; get; }
#endif

        // ------ 配置
        // readonly float renderScale = 1.0f;
        // readonly float renderScale_mid = 0.8f;
        // readonly float renderScale_low = 0.6f;

        PerfLevelType curPerfLevelType;
        public PerfLevelType CurPerfLevelType => curPerfLevelType;

        public Debug_FPS Comp_FPS;
        public void Init (Debug_FPS comp) {
            // 启用后台运行
            Application.runInBackground = true;
            // 可选：验证设置是否生效
            Debug.Log ("RunInBackground is set to: " + Application.runInBackground);

#if UNITY_EDITOR
            Debug.Log ($"QualitySettings.GetQualityLevel() = {QualitySettings.GetQualityLevel()}");
#endif

            Comp_FPS = comp;

            // 1 TODO 优化功耗
            // OnDemandRendering.renderFrameInterval = 1; // 设置为1表示每帧都渲染（默认）

            // 2 (仅移动平台生效)
            if (Application.isMobilePlatform) {
                // Screen.sleepTimeout = SleepTimeout.NeverSleep;

                if (GameSettings._instance.ForceQualityLevel != PerfLevelType.NONE) {
                    curPerfLevelType = GameSettings._instance.ForceQualityLevel;
                } else {
                    GetDeviceInfo ();
                    UpdatePerfLevel ();
                }

            } else {
                if (GameSettings._instance.ForceQualityLevel != PerfLevelType.NONE) {
                    curPerfLevelType = GameSettings._instance.ForceQualityLevel;

                    AdjustPerformance ();
                } else {
                    GetDeviceInfo ();

                    UpdatePerfLevel ();
                    // curPerfLevelType = PerfLevelType.High;
                    // Application.targetFrameRate = 60;
                }
            }

#if !SRV_ALIYUN_PRODUCTION
            perfLevDesc = $"Mem:{PerfManager.Self.DeviceMemoryGB}[{memLev}], CPU:{PerfManager.Self.CpuProcessorType}_{cpuProcessorCount}_{cpuProcessorFrequency}-{cpuPerformanceScore}[{cpuLev}], GPU:{PerfManager.Self.GraphicsDeviceName}-{gpuPerformanceScore}[{gpuLev}]";
            Debug.Log (perfLevDesc);
#endif

            AdjustPerformance ();
        }

        void GetDeviceInfo () {
            // 获取系统内存（转换为GB）
            _deviceMemoryGB = SystemInfo.systemMemorySize / 1024f;

            cpuProcessorType = SystemInfo.processorType;
            cpuProcessorCount = SystemInfo.processorCount;
            cpuProcessorFrequency = SystemInfo.processorFrequency;

            graphicsDeviceName = SystemInfo.graphicsDeviceName;
        }

        void AdjustPerformance () {
            UniversalRenderPipelineAsset selectedPipeline = null;

            // Common
            QualitySettings.vSyncCount = 0;

            switch (curPerfLevelType) {
                case PerfLevelType.Low: // 低档
                    Application.targetFrameRate = 60; //30;
                    Screen.sleepTimeout = SleepTimeout.NeverSleep; //SystemSetting;
                    selectedPipeline = GameSettings._instance.LowQualityPipeline;
                    break;
                case PerfLevelType.Mid: // 中档 - 设置为45帧(如果手机没有45帧，会退到30帧???)
                    Application.targetFrameRate = 60;
                    Screen.sleepTimeout = SleepTimeout.NeverSleep; //SystemSetting;
                    selectedPipeline = GameSettings._instance.MediumQualityPipeline;
                    break;
                case PerfLevelType.High: // 高档
                    Application.targetFrameRate = 60; // 或120
                    Screen.sleepTimeout = SleepTimeout.NeverSleep;
                    selectedPipeline = GameSettings._instance.HighQualityPipeline;
                    break;
            }

            // 这是切换渲染管线的核心语句
            if (selectedPipeline != null)
                GraphicsSettings.renderPipelineAsset = selectedPipeline;

            // 同时调整其他画质设置
            Set_Quality ();
        }

        public PerfLevelType memLev { private set; get; }
        public PerfLevelType cpuLev { private set; get; }
        public PerfLevelType gpuLev { private set; get; }
        void UpdatePerfLevel () {
            memLev = UpdatePerfLevel_Mem ();
            cpuLev = UpdatePerfLevel_CPU ();
            gpuLev = UpdatePerfLevel_GPU ();
            Debug.Log ($"UpdatePerfLevel --> memLev:{memLev},cpuLev:{cpuLev},gpuLev:{gpuLev}");

            curPerfLevelType = PerfLevelType.High;
            if (memLev < curPerfLevelType)
                curPerfLevelType = memLev;
            if (cpuLev < curPerfLevelType)
                curPerfLevelType = cpuLev;
            if (gpuLev < curPerfLevelType)
                curPerfLevelType = gpuLev;
        }
        PerfLevelType UpdatePerfLevel_Mem () {
            Debug.Log ($"UpdatePerfLevel_Mem --> _deviceMemoryGB:{_deviceMemoryGB}");

            if (_deviceMemoryGB <= memoryThresholdGB_1)
                return PerfLevelType.Low;
            else if (_deviceMemoryGB > memoryThresholdGB_1 && _deviceMemoryGB <= memoryThresholdGB_2)
                return PerfLevelType.Mid;
            else if (_deviceMemoryGB > memoryThresholdGB_2)
                return PerfLevelType.High;

            return PerfLevelType.Low;
        }
        PerfLevelType UpdatePerfLevel_CPU () {
            var t = DeterminePerformanceTier (
                cpuProcessorCount,
                cpuProcessorFrequency
            );

            return t;
        }

        public float cpuPerformanceScore;
        PerfLevelType DeterminePerformanceTier (int coreCount, int frequencyMHz) {
            float frequencyGHz = frequencyMHz / 1000f;

            // 基于市场数据分析的权重算法
            cpuPerformanceScore = CalculatePerformanceScore (coreCount, frequencyGHz);
            Debug.Log ($"UpdatePerfLevel_CPU --> Score:{cpuPerformanceScore} -- {PerfManager.Self.CpuProcessorType}, coreCount:{coreCount}, frequencyGHz:{frequencyGHz}");

            if (cpuPerformanceScore >= 9.5f) // default suggestion value 8
                return PerfLevelType.High;
            else if (cpuPerformanceScore >= 9f) // default suggestion value 4.5
                return PerfLevelType.Mid;
            else
                return PerfLevelType.Low;
        }

        float CalculatePerformanceScore (int coreCount, float frequencyGHz) {
            // 性能评分算法：核心数和频率的加权计算
            // 权重基于市场数据分析（核心数权重0.6，频率权重0.4）
            float coreScore = Mathf.Clamp (coreCount / 8f, 0f, 1f) * 10f;
            // 高档	3.8 GHz - 4.74 GHz 中档	2.8 GHz - 3.5 GHz 低档	2.0 GHz - 2.7 GHz(小于2.8GHz)
            float freqScore = Mathf.Clamp (frequencyGHz / 3.8f, 0f, 1f) * 10f;

            // 可能适用于移动平台，PC平台核心数权重应该更高
            return (coreScore * 0.6f) + (freqScore * 0.4f);
        }

        public string gpuName;
        public int gpuMemoryMB;
        public bool hasTessellation;
        public bool hasGeometryShaders;
        public bool hasComputeShaders;
        public int maxTextureSize;
        public bool astcSupport;
        public bool etc2Support;
        public bool pvrtcSupport;
        public int graphicsShaderLevel;
        public float gpuPerformanceScore;
        PerfLevelType UpdatePerfLevel_GPU () {
            gpuName = SystemInfo.graphicsDeviceName;
            gpuMemoryMB = SystemInfo.graphicsMemorySize;
            hasTessellation = SystemInfo.supportsTessellationShaders;
            hasGeometryShaders = SystemInfo.supportsGeometryShaders;
            hasComputeShaders = SystemInfo.supportsComputeShaders;
            maxTextureSize = SystemInfo.maxTextureSize;
            graphicsShaderLevel = SystemInfo.graphicsShaderLevel;

            // 纹理压缩格式支持检测
            astcSupport = SystemInfo.SupportsTextureFormat (TextureFormat.ASTC_6x6);
            etc2Support = SystemInfo.SupportsTextureFormat (TextureFormat.ETC2_RGBA8);
            pvrtcSupport = SystemInfo.SupportsTextureFormat (TextureFormat.PVRTC_RGBA4);

            // 计算性能评分并确定档位
            gpuPerformanceScore = CalculateGPUScore ();
            Debug.Log ($"UpdatePerfLevel_GPU --> Score:{gpuPerformanceScore} -- gpuName:{gpuName},gpuMemoryMB:{gpuMemoryMB},hasTessellation:{hasTessellation},hasGeometryShaders:{hasGeometryShaders},maxTextureSize:{maxTextureSize},graphicsShaderLevel:{graphicsShaderLevel},astcSupport:{astcSupport},etc2Support:{etc2Support},pvrtcSupport:{pvrtcSupport}");
            PerfLevelType t = DetermineGPUTier (gpuPerformanceScore);

            return t;
        }

        PerfLevelType DetermineGPUTier (float performanceScore) {
            if (performanceScore >= 96) //35f
                return PerfLevelType.High;
            else if (performanceScore >= 80f) //20f
                return PerfLevelType.Mid;
            else
                return PerfLevelType.Low;

            // 以 adreno (tm) 7/6/5 三代gpu为例 分别 得分是 100 / 84 / 72
        }

        float CalculateGPUScore () {
            float score = 0f;

            // 显存评分 (权重: 20%)
            score += Mathf.Clamp (gpuMemoryMB / 2048f, 0f, 1f) * 25f;

            // Shader Level评分 (权重: 20%)
            score += Mathf.Clamp ((graphicsShaderLevel - 20) / 50f, 0f, 1f) * 20f;

            // 特性支持评分 (权重: 5%)
            float featureScore = 0f;
            if (hasTessellation) featureScore += 0.3f;
            if (hasGeometryShaders) featureScore += 0.2f;
            if (hasComputeShaders) featureScore += 0.3f;
            if (astcSupport) featureScore += 0.2f;
            score += featureScore * 10f;

            // 纹理大小评分 (权重: 5%)
            score += Mathf.Clamp (maxTextureSize / 8192f, 0f, 1f) * 15f;

            // GPU型号识别加分 (权重: 50%)
            score += GetGPUModelBonus () * 40f;
            // score += 0.6f * 40f;

            return score;
        }

        private float GetGPUModelBonus () {
            string lowerName = gpuName.ToLower ();
            // Debug.LogError($"GetGPUModelBonus lowerName:{lowerName}");

            // 高端GPU型号识别
#if UNITY_EDITOR
            // pc平台 
            // if (lowerName.Contains("nvidia geforce") || lowerName.Contains("amd radeon"))// rtx 3060 ti)
            return 1.0f;
#endif

            // mobile平台
            // TEST
            // if (lowerName.Contains("adreno (tm) 9") || lowerName.Contains("adreno (tm) 8") || lowerName.Contains("adreno (tm) 7")
            if (lowerName.Contains ("adreno (tm) 9") || lowerName.Contains ("adreno (tm) 8") || lowerName.Contains ("adreno (tm) 750") || lowerName.Contains ("adreno (tm) 740") ||
                lowerName.Contains ("mali-g7") || lowerName.Contains ("mali-g8") ||
                lowerName.Contains ("apple a1") || lowerName.Contains ("apple a2"))
                return 1.0f;

            // 中端GPU型号识别
            if (lowerName.Contains ("adreno (tm) 730") ||
                lowerName.Contains ("adreno (tm) 6") || //lowerName.Contains("adreno (tm) 5") || //lowerName.Contains("adreno 4") ||
                lowerName.Contains ("mali-t8") ||
                lowerName.Contains ("powervr g") || lowerName.Contains ("apple a9") ||
                lowerName.Contains ("apple a10"))
                return 0.6f;

            // 低端GPU型号识别
            //lowerName.Contains("mali-g5") || 红米note10 典型的低端机
            return 0.3f;
        }

        void Set_Quality () {
            var qLevel = 0;
            switch (curPerfLevelType) {
                case PerfLevelType.Low:
                    qLevel = 1;
                    break;
                case PerfLevelType.Mid:
                    qLevel = 2;
                    break;
                case PerfLevelType.High:
                    qLevel = 5;
                    break;
            }
            QualitySettings.SetQualityLevel (qLevel, true);
        }

        GameObject curr_sceneVolume;
        public void Set_PostProcessing () {
            if (!GameSettings._instance.IsEnableVolume)
                return;
            if (curPerfLevelType < PerfLevelType.High) {
                SetBattlePostProcessDisable ();
                return;
            }

            var res = Resources.Load<GameObject> ("TestGlobalVolume");
            // ResUtils.LoadAsync<GameObject> ("TestGlobalVolume", (res) => {
            curr_sceneVolume = UnityEngine.Object.Instantiate (res);
            curr_sceneVolume.SetActive (!GameSettings.IsHideOrShowPostProcess);
            // });
        }

        public void SetBattlePostProcessDisable () {
            if (curr_sceneVolume != null) {
                // curr_sceneVolume.SetActive(false);
                GameObject.Destroy (curr_sceneVolume);
            }
            curr_sceneVolume = null;
        }

        public void TEST_Toggle_PostProcess () {
            if (curr_sceneVolume != null) {
                GameSettings.IsHideOrShowPostProcess = !GameSettings.IsHideOrShowPostProcess;
                curr_sceneVolume.gameObject.SetActive (!GameSettings.IsHideOrShowPostProcess);
            } else {
                Debug.Log ($"curPerfLevelType:{curPerfLevelType}, has no post processing");
            }
        }

        // 中高性能模式下，开启camera自身的后处理 和 FXAA 抗锯齿，低端机则关闭所有
        public void Check_SetManiCameraPostProcessAndFXAA () {
            if (curPerfLevelType is PerfLevelType.High or PerfLevelType.Mid)
                SetManiCameraPostProcessAndFXAA (true);
            else
                SetManiCameraPostProcessAndFXAA (false);
        }
        // 也可以在某些特殊场景 强制开启或关闭
        public void SetManiCameraPostProcessAndFXAA (bool isOpen) {
            var cam = Camera.main;

            if (cam == null) {
                Debug.LogWarning ($"SetManiCameraPostProcessAndFXAA main camera is null"); //, Game State:{GameController.Self.CurState}
                return;
            }

            UniversalAdditionalCameraData cameraData = cam.GetComponent<UniversalAdditionalCameraData> ();
            if (cameraData == null) {
                Debug.LogWarning ("SetManiCameraPostProcessAndFXAA cameraData is null");
                return;
            }

            cameraData.renderPostProcessing = isOpen;
            if (isOpen)
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; //FastApproximateAntialiasing
            else
                cameraData.antialiasing = AntialiasingMode.None;
        }

        // 处理特效的高中低分档，场景预置 + 战斗即时动态生成
        // Awake时处理
        // 一旦找到目标对象，无需要递归遍历所有层级的子对象
        // -- 目前的规则是 1 场景预置的 在第一子级 2 动态生成的在 Con的第一子级
        public void SetVFXLevel_DynamicSpawn (Transform vfxContainer) {
            if (curPerfLevelType >= PerfLevelType.High)
                return;

            var con = vfxContainer.Find ("Con");
            if (con != null)
                SetVFXLevel (con);
            else SetVFXLevel (con);
        }
        void SetVFXLevel (Transform vfxContainer) {
            foreach (Transform child in vfxContainer) {
                // Log.Console ($"vfxContainer:{vfxContainer.name},child.name:{child.name}");
                GameObject childObj = child.gameObject;

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

                // 是否递归
            }
        }
    }
}