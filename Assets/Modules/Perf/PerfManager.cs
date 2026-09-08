using System;
using LowoUN.Module.Perf;
using LowoUN.Util;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerfManager : SingletonSimple<PerfManager> {
    public event Action OnResetPerfLev;
    private UniversalRenderPipelineAsset selectedPipeline;

    // ------ 设备信息
    // [Header ("内存阈值配置")] "触发降级的内存阈值 GB")]
    #if UNITY_IOS
    readonly float memoryThresholdGB_1 = 3.0f;
    #else
    readonly float memoryThresholdGB_1 = 4.0f;
    #endif
    #if UNITY_IOS
    readonly float memoryThresholdGB_2 = 5.4f;
    #else
    readonly float memoryThresholdGB_2 = 6.0f;
    #endif

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
    public string perfLevDesc{private set;get;}
    #endif

    // 需要记录真实的性能档位（根据算法得出的结果）
    PerfLevelType curPerfLevelType_Real;
    public PerfLevelType CurPerfLevelType_Real => curPerfLevelType_Real;
    // 同时也需要记录当前生效的性能档位（可能被强制设置覆盖）
    PerfLevelType curPerfLevelType;
    public PerfLevelType CurPerfLevelType => curPerfLevelType;
    bool isIPhone = false;
    public void Init_DebugMode () {
        isIPhone = false; 

        if (GameSettings._instance.ForceQualityLevel == PerfLevelType.NONE) {
            #if UNITY_EDITOR
            this.curPerfLevelType = PerfLevelType.High;
            #else
            GetDeviceInfo();
            UpdatePerfLevel ();
            #endif
        }
        else
            this.curPerfLevelType = GameSettings._instance.ForceQualityLevel;
            
        Init2();
    }

    void CheckIsIphone () {
        #if UNITY_IOS// && !UNITY_EDITOR
        isIPhone = true;
        #else
        isIPhone = false;
        #endif
        LLog.NOT_PRODUCTION_LOG($"[Perf] Init -- isIPhone:{isIPhone}");
        // TEST
        // var cpuSocre = CalculatePerformanceScore_IPhone("apple a14");
        // Debug.LogError($"cpuSocre:{cpuSocre}");
        // return;
    }
    public void Init (PerfLevelType cachePerfLevType) {
        curPerfLevelType_Real = GetPerfLevel_FirstLaunchApp();
        PerfLevelHandler.Self.SetOutlinePassEnabled(curPerfLevelType_Real > PerfLevelType.Low);

        CheckIsIphone();

        LLog.NOT_PRODUCTION_LOG($"[Perf] Init -- cachePerfLevType:{cachePerfLevType}");
        this.curPerfLevelType = cachePerfLevType;

        Init2();
    }

    void Init2 () {
        LLog.NOT_PRODUCTION_LOG ($"[perf] curPerfLevelType:{this.curPerfLevelType}, QualitySettings.GetQualityLevel() = {QualitySettings.GetQualityLevel()}");

        LLog.NOT_PRODUCTION_LOG("[Perf] "+ $"Mem:{DeviceMemoryGB}[{memLev}], CPU:{CpuProcessorType}_{cpuProcessorCount}_{cpuProcessorFrequency}-{cpuPerformanceScore}[{cpuLev}], GPU:{GraphicsDeviceName}-{gpuPerformanceScore}[{gpuLev}]");

        AdjustPerformance (this.curPerfLevelType);

        // 3 Others
        // UpdateCameraData();

        Init_default_renderScale();

        Init_MaxScreenParticle();

        // 是否动态渲染帧
        // // 禁用Ondemand Rendering
        // OnDemandRendering.renderFrameInterval = 1; // 设置为1表示每帧都渲染
        // // OnDemandRendering.enabled = false; // 完全禁用动态渲染
        // // 通过VSync控制帧率
        // QualitySettings.vSyncCount = 1; // 每垂直同步渲染一帧，会影响输入

        // 启用后台运行
        Application.runInBackground = true;
        // 可选：验证设置是否生效
        LLog.NOT_PRODUCTION_LOG($"[Perf] RunInBackground is set to: " + Application.runInBackground);
    }

    // 缓存当前值
    private int _currentMaxScreenParticle;
    // 公开属性
    public int MaxScreenParticle => _currentMaxScreenParticle;
    public void Init_MaxScreenParticle () {
        _currentMaxScreenParticle = curPerfLevelType switch {
            PerfLevelType.High => PerfSettings.MaxScreenParticle_High,
            PerfLevelType.Mid => PerfSettings.MaxScreenParticle_Middle,
            PerfLevelType.Low => PerfSettings.MaxScreenParticle_Low,
            _ => PerfSettings.MaxScreenParticle_Low  // 默认值
        };
        // Debug.LogError($"Init_MaxScreenParticle -- 222 _currentMaxScreenParticle:{_currentMaxScreenParticle}");
    }

    public void SetMaxScreenParticle(int value)
    {
        _currentMaxScreenParticle = value;
    }

    void GetDeviceInfo () {
        // 获取系统内存（转换为GB）
        _deviceMemoryGB = SystemInfo.systemMemorySize / 1024f;

        cpuProcessorType = SystemInfo.processorType;
        cpuProcessorCount = SystemInfo.processorCount;
        cpuProcessorFrequency = SystemInfo.processorFrequency;

        graphicsDeviceName = SystemInfo.graphicsDeviceName;
    }

    public PerfLevelType GetPerfLevel_FirstLaunchApp()
    {
        CheckIsIphone();

        GetDeviceInfo();
        memLev = UpdatePerfLevel_Mem();
        gpuLev = UpdatePerfLevel_GPU();
        cpuLev = UpdatePerfLevel_CPU();

        var levelType = PerfLevelType.High;
        if(memLev < levelType)
            levelType = memLev;
        if(cpuLev < levelType)
            levelType = cpuLev;
        if(gpuLev < levelType)
            levelType = gpuLev;

        LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel:{levelType} --> memLev:{memLev},cpuLev:{cpuLev},gpuLev:{gpuLev}");
        return levelType;
    }
    void AdjustPerformance (PerfLevelType levelType) {
        QualitySettings.vSyncCount = 0;

        switch (levelType) {
            case PerfLevelType.Low: // 低档
                Application.targetFrameRate = 60;//30;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;//SystemSetting;
                selectedPipeline = GameSettings._instance.LowQualityPipeline;
                break;
            case PerfLevelType.Mid: // 中档 - 设置为45帧(如果手机没有45帧，会退到30帧???)
                Application.targetFrameRate = 60;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;//SystemSetting;
                selectedPipeline = GameSettings._instance.MediumQualityPipeline;
                break;
            case PerfLevelType.High: // 高档
                Application.targetFrameRate = 60; // 或120
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                selectedPipeline = GameSettings._instance.HighQualityPipeline;
                break;
        }

        // 获取URP管线配置
        // _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        // Set_RenderScale ();
        // 这是切换渲染管线的核心语句
        if (selectedPipeline != null) {
            GraphicsSettings.renderPipelineAsset = selectedPipeline;
            // 如果切换后需要强制刷新所有渲染器，可以取消下一行的注释
            // UniversalRenderPipeline.ReloadAllRenderers();
        }

        // 同时调整其他画质设置
        Set_Quality ();
    }

    public PerfLevelType memLev{private set;get;}
    public PerfLevelType cpuLev{private set;get;}
    public PerfLevelType gpuLev{private set;get;}
    void UpdatePerfLevel () {
        memLev = UpdatePerfLevel_Mem();
        gpuLev = UpdatePerfLevel_GPU();
        cpuLev = UpdatePerfLevel_CPU();
        LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel --> memLev:{memLev},cpuLev:{cpuLev},gpuLev:{gpuLev}");

        curPerfLevelType = PerfLevelType.High;
        if(memLev < curPerfLevelType)
            curPerfLevelType = memLev;
        if(cpuLev < curPerfLevelType)
            curPerfLevelType = cpuLev;
        if(gpuLev < curPerfLevelType)
            curPerfLevelType = gpuLev;
    }
    PerfLevelType UpdatePerfLevel_Mem () {
        LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel_Mem --> _deviceMemoryGB:{_deviceMemoryGB}");
        
        if (_deviceMemoryGB <= memoryThresholdGB_1)
            return PerfLevelType.Low;
        else if (_deviceMemoryGB > memoryThresholdGB_1 && _deviceMemoryGB <= memoryThresholdGB_2)
            return PerfLevelType.Mid;
        else if (_deviceMemoryGB > memoryThresholdGB_2)
            return PerfLevelType.High;

        return PerfLevelType.Low;
    }
    PerfLevelType UpdatePerfLevel_CPU () {
        var t = DeterminePerformanceTier(
            cpuProcessorCount,
            cpuProcessorFrequency
        );

        return t;
    }
    
    public float cpuPerformanceScore;
    PerfLevelType DeterminePerformanceTier(int coreCount, int frequencyMHz)
    {
        float frequencyGHz = frequencyMHz / 1000f;
        // // TEST
        // cpuPerformanceScore = CalculatePerformanceScore_IPhone(gpuName);
        // Debug.LogError($"cpuPerformanceScore:{cpuPerformanceScore}");

        if (isIPhone) {
            // cpuPerformanceScore = CalculatePerformanceScore_IPhone2(coreCount, frequencyGHz, cpuProcessorType);
            cpuPerformanceScore = CalculatePerformanceScore_IPhone(gpuName);
            LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel_CPU [iPhone] --> Score:{cpuPerformanceScore} -- {CpuProcessorType}, coreCount:{coreCount}, frequencyGHz:{frequencyGHz}");
            return DeterminePerformanceTierByCpuScore_Iphone(cpuPerformanceScore);
        }

        cpuPerformanceScore = CalculatePerformanceScore(coreCount, frequencyGHz);
        LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel_CPU --> Score:{cpuPerformanceScore} -- {CpuProcessorType}, coreCount:{coreCount}, frequencyGHz:{frequencyGHz}");

        return DeterminePerformanceTierByCpuScore(cpuPerformanceScore);
    }

    float CalculatePerformanceScore(int coreCount, float frequencyGHz)
    {
        // 性能评分算法：核心数和频率的加权计算
        // 权重基于市场数据分析（核心数权重0.6，频率权重0.4）
        float coreScore = Mathf.Clamp(coreCount / 8f, 0f, 1f) * 10f;
        // 高档	3.8 GHz - 4.74 GHz 中档	2.8 GHz - 3.5 GHz 低档	2.0 GHz - 2.7 GHz(小于2.8GHz)
        float freqScore = Mathf.Clamp(frequencyGHz / 3.8f, 0f, 1f) * 10f;
        
        // 可能适用于移动平台，PC平台核心数权重应该更高
        return (coreScore * 0.6f) + (freqScore * 0.4f);
    }

    float CalculatePerformanceScore_IPhone(string gpuName) {
        LLog.NOT_PRODUCTION_ERROR($"[Perf] CalculatePerformanceScore_IPhone gpuName:{gpuName}");
        // TEST
        // string lowerName = "apple a14";

        string lowerName = gpuName?.ToLower() ?? string.Empty;
        // #if UNITY_EDITOR
        // return 10f;
        // #endif

        if (lowerName.Contains("apple m"))
            return 10f;
        
        if (lowerName.Contains("apple a")) {
            LLog.NOT_PRODUCTION_LOG($"[Perf] CalculatePerformanceScore_IPhone 1");

            int version = ExtractNumber(lowerName);
            LLog.NOT_PRODUCTION_LOG($"[Perf] CalculatePerformanceScore_IPhone 2 ExtractNumber version:{version}");
        
            if (version >= 14) return 10f;   // A14 及以上（含 A14, A15, A16, A17, A18...）
            if (version >= 10) return 8.2f;  // A10 - A13
            if (version >= 7)  return 7.5f;  // A7 - A9
            if (version >= 4)  return 6.5f;  // A4 - A6
            return 6f;                       // A3 及以下
        }

        return 6f;
    }
    // float CalculatePerformanceScore_IPhone2(int coreCount, float frequencyGHz, string processorType)
    // {
    //     var lowerCpu = processorType?.ToLower() ?? string.Empty;
        
    //     // 对于 arm64e 架构的 iPhone（可能不带 Apple SoC 标识的系统）
    //     // 根据核心数和频率综合评估
    //     if (lowerCpu.Contains("arm64e"))
    //     {
    //         // iPhone 12-15 通常 6 核 + 3.0-3.2GHz -> 中高端（A14/A15/A16/A17）
    //         // iPhone 11 通常 6 核 + 2.65-2.9GHz -> 中端（A13）
    //         // iPhone XS/XR 通常 6 核 + 2.4-2.5GHz -> 中端（A12）
    //         // iPhone 8-X 通常 6 核 + 2.3-2.4GHz -> 低中端（A11/A10）
            
    //         if (coreCount >= 6 && frequencyGHz >= 3.0f)
    //             return 10f;  // A14 及更新
    //         else if (coreCount >= 6 && frequencyGHz >= 2.65f && frequencyGHz < 3.0f)
    //             return 8.2f;  // A13
    //         else if (coreCount >= 6 && frequencyGHz >= 2.35f && frequencyGHz < 2.65f)
    //             return 7.5f;  // A12/A11
    //         else if (coreCount >= 4 && frequencyGHz >= 2.0f)
    //             return 6.5f;  // A10/A9 及更旧
    //     }

    //     // 如果能识别到具体的 Apple SoC 名称，直接返回对应分数
    //     if (lowerCpu.Contains("apple m"))
    //         return 10f;

    //     if(lowerCpu.Contains("apple a")) {
    //         if (lowerCpu.Contains("apple a17") || lowerCpu.Contains("apple a16") || lowerCpu.Contains("apple a15") || lowerCpu.Contains("apple a14"))
    //             return 10f;

    //         if (lowerCpu.Contains("apple a13") || lowerCpu.Contains("apple a12") || lowerCpu.Contains("apple a11") || lowerCpu.Contains("apple a10"))
    //             return 8.2f;

    //         if (lowerCpu.Contains("apple a9") || lowerCpu.Contains("apple a8") || lowerCpu.Contains("apple a7"))
    //             return 6.5f;
    //     }

    //     // 其他未知 iPhone 机型，使用默认 CPU 核心/频率计算，并降低阈值
    //     float baseScore = CalculatePerformanceScore(coreCount, frequencyGHz);
    //     return Mathf.Clamp(baseScore * 0.95f, 4f, 10f);
    // }

    PerfLevelType DeterminePerformanceTierByCpuScore_Iphone(float score)
    {
        LLog.NOT_PRODUCTION_LOG("[Perf] DeterminePerformanceTierByCpuScore_Iphone");
        if (score >= 9f)
            return PerfLevelType.High;
        else if (score >= 7f)
            return PerfLevelType.Mid;
        else
            return PerfLevelType.Low;
    }
    PerfLevelType DeterminePerformanceTierByCpuScore(float score)
    {
        if (score >= 9.5f)
            return PerfLevelType.High;
        else if (score >= 9f)
            return PerfLevelType.Mid;
        else
            return PerfLevelType.Low;
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
        astcSupport = SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_6x6);
        etc2Support = SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA8);
        pvrtcSupport = SystemInfo.SupportsTextureFormat(TextureFormat.PVRTC_RGBA4);

        // 计算性能评分并确定档位
        gpuPerformanceScore = CalculateGPUScore();
        LLog.NOT_PRODUCTION_LOG($"[Perf] UpdatePerfLevel_GPU --> Score:{gpuPerformanceScore} -- gpuName:{gpuName},gpuMemoryMB:{gpuMemoryMB},hasTessellation:{hasTessellation},hasGeometryShaders:{hasGeometryShaders},maxTextureSize:{maxTextureSize},graphicsShaderLevel:{graphicsShaderLevel},astcSupport:{astcSupport},etc2Support:{etc2Support},pvrtcSupport:{pvrtcSupport}");
        PerfLevelType t = DetermineGPUTier(gpuPerformanceScore);

        return t;
    }

    PerfLevelType DetermineGPUTier(float performanceScore)
    {
        // bool isIPhone = Application.platform == RuntimePlatform.IPhonePlayer;
        if (isIPhone)
        {
            if (performanceScore >= 90f)
                return PerfLevelType.High;
            else if (performanceScore >= 72f)
                return PerfLevelType.Mid;
            else
                return PerfLevelType.Low;
        }

        if (performanceScore >= 96)
            return PerfLevelType.High;
        else if (performanceScore >= 80f)
            return PerfLevelType.Mid;
        else
            return PerfLevelType.Low;

        // 以 adreno (tm) 7/6/5 三代gpu为例 分别 得分是 100 / 84 / 72
    }

    float CalculateGPUScore()
    {
        // bool isIPhone = Application.platform == RuntimePlatform.IPhonePlayer;
        if (isIPhone)
            return CalculateGPUScore_IPhone();

        float score = 0f;

        // 显存评分 (权重: 20%)
        score += Mathf.Clamp(gpuMemoryMB / 2048f, 0f, 1f) * 25f;

        // Shader Level评分 (权重: 20%)
        score += Mathf.Clamp((graphicsShaderLevel - 20) / 50f, 0f, 1f) * 20f;

        // 特性支持评分 (权重: 5%)
        float featureScore = 0f;
        if (hasTessellation) featureScore += 0.3f;
        if (hasGeometryShaders) featureScore += 0.2f;
        if (hasComputeShaders) featureScore += 0.3f;
        if (astcSupport) featureScore += 0.2f;
        score += featureScore * 10f;

        // 纹理大小评分 (权重: 5%)
        score += Mathf.Clamp(maxTextureSize / 8192f, 0f, 1f) * 15f;

        // GPU型号识别加分 (权重: 50%)
        score += GetGPUModelBonus(gpuName) * 40f;

        return score;
    }

    float CalculateGPUScore_IPhone()
    {
        LLog.NOT_PRODUCTION_LOG("[Perf] CalculateGPUScore_IPhone");

        float score = 0f;

        // iPhone 上显存不会太大，因此以 2GB 为参考基准
        score += Mathf.Clamp(gpuMemoryMB / 2048f, 0f, 1f) * 20f;

        // Shader Level 仍有参考价值，但不应过度依赖
        score += Mathf.Clamp((graphicsShaderLevel - 20) / 40f, 0f, 1f) * 15f;

        float featureScore = 0f;
        if (hasTessellation) featureScore += 0.25f;
        if (hasGeometryShaders) featureScore += 0.2f;
        if (hasComputeShaders) featureScore += 0.25f;
        if (astcSupport) featureScore += 0.3f;
        score += featureScore * 10f;

        score += Mathf.Clamp(maxTextureSize / 8192f, 0f, 1f) * 15f;

        score += GetGPUModelBonus_Iphone(gpuName) * 40f;

        return score;
    }

    private float GetGPUModelBonus_Iphone(string gpuName)
    {
        string lowerName = gpuName?.ToLower() ?? string.Empty;
        #if UNITY_EDITOR
        return 1.0f;
        #else

        if (lowerName.Contains("apple m"))
            return 1.0f;
        
        if (lowerName.Contains("apple a")) {
            // if (lowerName.Contains("apple a17") || lowerName.Contains("apple a16") || lowerName.Contains("apple a15") || lowerName.Contains("apple a14"))
            //     return 1.0f;

            // if (lowerName.Contains("apple a13") || lowerName.Contains("apple a12") || lowerName.Contains("apple a11") || lowerName.Contains("apple a10"))
            //     return 0.75f;

            // if (lowerName.Contains("apple a9") || lowerName.Contains("apple a8") || lowerName.Contains("apple a7"))
            //     return 0.55f;

            int version = ExtractNumber(lowerName);
            LLog.NOT_PRODUCTION_LOG($"[Perf] GetGPUModelBonus_Iphone version:{version}");
        
            if (version >= 14) return 1.0f;   // A14 及以上（含 A14, A15, A16, A17, A18...）
            if (version >= 10) return 0.75f;  // A10 - A13
            if (version >= 7)  return 0.55f;  // A7 - A9
            if (version >= 4)  return 0.35f;  // A4 - A6
            return 0.3f;                       // A3 及以下
        }

        return 0.3f;
        #endif
    }

    // 提取 "apple a14" 中的数字 14
    private int ExtractNumber(string input) {
        var match = System.Text.RegularExpressions.Regex.Match(input, @"apple a(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            return num;
        return 0;
    }

    private float GetGPUModelBonus(string gpuName)
    {
        string lowerName = gpuName?.ToLower() ?? string.Empty;
        #if UNITY_EDITOR
        return 1.0f;
        #else

        if (lowerName.Contains("adreno (tm) 9") || lowerName.Contains("adreno (tm) 8") || lowerName.Contains("adreno (tm) 750") || lowerName.Contains("adreno (tm) 740") ||
            lowerName.Contains("mali-g7") || lowerName.Contains("mali-g8"))
            return 1.0f;

        if (lowerName.Contains("adreno (tm) 730") || lowerName.Contains("adreno (tm) 6") || lowerName.Contains("mali-t8") || lowerName.Contains("powervr g") || lowerName.Contains("apple a9") || lowerName.Contains("apple a10"))
            return 0.6f;

        return 0.3f;
        #endif
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

    #if UNITY_ANDROID && !UNITY_EDITOR
    readonly float default_renderScale_high = PerfSettings.default_renderScale_Android_high;
    readonly float default_renderScale_mid = PerfSettings.default_renderScale_Android_mid;
    readonly float default_renderScale_low = PerfSettings.default_renderScale_Android_low;
    #elif UNITY_IOS && !UNITY_EDITOR
    readonly float default_renderScale_high = PerfSettings.default_renderScale_iOS_high;
    readonly float default_renderScale_mid = PerfSettings.default_renderScale_iOS_mid;
    readonly float default_renderScale_low = PerfSettings.default_renderScale_iOS_low;
    #else
    readonly float default_renderScale_high = PerfSettings.default_renderScale_high;
    readonly float default_renderScale_mid = PerfSettings.default_renderScale_mid;
    readonly float default_renderScale_low = PerfSettings.default_renderScale_low;
    #endif

    void Init_default_renderScale() {
        if (selectedPipeline == null) {
            LLog.NOT_PRODUCTION_ERROR("[Perf] URP asset not found!");
            return;
        }

        Set_RenderScale_Init();
    }
    public void Set_Battle_In () {
        // LLog.Error("Set_RenderScale_Battle_In");
        Set_RenderScale_Battle_In();

        //Fog
        if(curPerfLevelType == PerfLevelType.Low) {
            // if(HeightFog.HeightFogGlobal.Instance) {
            //     HeightFog.HeightFogGlobal.Instance.GetComponent<MeshRenderer>().enabled = false;
            //     HeightFog.HeightFogGlobal.Instance.gameObject.SetActive(false);
            // }
        }
    }
    void Set_RenderScale_Battle_In () {
        // LLog.Error("Set_RenderScale_Battle_In");
        if(curPerfLevelType == PerfLevelType.High)
            Set_RenderScale(default_renderScale_high-0.05f);
        else if(curPerfLevelType == PerfLevelType.Mid)
            Set_RenderScale(default_renderScale_mid-0.1f);
        else if(curPerfLevelType == PerfLevelType.Low)
            Set_RenderScale(default_renderScale_low-0.05f);
    }
    void Set_RenderScale_Init () {
        // LLog.Error("Set_RenderScale_Battle_Out");
        if(curPerfLevelType == PerfLevelType.High)
            Set_RenderScale(default_renderScale_high);
        else if(curPerfLevelType == PerfLevelType.Mid)
            Set_RenderScale(default_renderScale_mid);
        else if(curPerfLevelType == PerfLevelType.Low)
            Set_RenderScale(default_renderScale_low);
    }
    public void Set_Battle_Out () {
        Set_RenderScale_Init();
    }
    void Set_RenderScale (float targetScale) {
        if (selectedPipeline == null) {
            LLog.NOT_PRODUCTION_ERROR ("[Perf] URP asset not found!");
            return;
        }
        LLog.Green ($"Set_RenderScale:{targetScale}");
        selectedPipeline.renderScale = targetScale;
    }

    // 即时使画质渲染生效
    public void RefreshAll () {
        var newPerfLevType = UISettingsController.Self.GetPerfLevelType();
        this.curPerfLevelType = newPerfLevType;
        AdjustPerformance (this.curPerfLevelType);
        Init_default_renderScale();
        Init_MaxScreenParticle();
        
        // 系统内部直接调用
        PerfLevelHandler.Self.PerfLevChanged();
        OnResetPerfLev?.Invoke();
    }
}
