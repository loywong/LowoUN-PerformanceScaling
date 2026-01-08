using UnityEngine;
using UnityEngine.Rendering.Universal;

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

        // [LabelText("移动设备强制画质水平")] 
        public PerfLevelType ForceQualityLevel = PerfLevelType.NONE;
        // [LabelText("允许后效(高性能设备)")] 
        public bool IsEnableVolume = true;
        // [LabelText("测试关/开后效")] 
        public bool isHideOrShowPostProcess;

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