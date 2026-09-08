using UnityEngine;
using UnityEngine.Rendering.Universal;

// 性能分为三档
public enum PerfLevelType : byte {
    NONE = 0, 
    // environmentally friendly 节能
    Low = 1, // 流畅
    Mid = 2, // 标准
    High = 3, // 高清
    Perfect = 4, // 极致
}

namespace LowoUN.Module.Perf {
    public class GameSettings : MonoBehaviour {
        public static GameSettings _instance;
        [SerializeField]
        UniversalRenderPipelineAsset highQualityPipeline;
        public UniversalRenderPipelineAsset HighQualityPipeline => highQualityPipeline;
        [SerializeField]
        UniversalRenderPipelineAsset mediumQualityPipeline;
        public UniversalRenderPipelineAsset MediumQualityPipeline => mediumQualityPipeline;
        [SerializeField]
        UniversalRenderPipelineAsset lowQualityPipeline;
        public UniversalRenderPipelineAsset LowQualityPipeline => lowQualityPipeline;

        // [LabelText("使用设置面板的画质")] 
        public bool isQualityLevel_SettingPanel = true;
        // [LabelText("移动设备强制画质水平")] 
        public PerfLevelType ForceQualityLevel = PerfLevelType.NONE;
        // [LabelText("允许后效(高性能设备)")] 
        public bool IsEnableBattleVolume = true;
        // [LabelText("测试关/开后效")] 
        public bool isHideOrShowPostProcess;
        // [LabelText("强制开启抗锯齿(最低档画质)")] 
        public bool IsForceOpenAAOnLowQL = false;

        // [LabelText("显示环境信息")] 
        public bool isShowEnvInfo;
        // [LabelText("显示GUI调试")] 
        public bool ifShowGUITest = true;

        public static bool IsHideOrShowPostProcess {
            get { return _instance.isHideOrShowPostProcess; } set {
                _instance.isHideOrShowPostProcess = value;
            }
        }
        void Awake () {
            DontDestroyOnLoad (gameObject);
            _instance = this;
        }
    }
}