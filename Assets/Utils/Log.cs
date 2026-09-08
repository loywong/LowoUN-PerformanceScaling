using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 色彩使用建议:
// Gray 数量多，不重要的信息
// White -- 普通log 数量少，重要提醒
// Gray -- 普通log 数量多，一般信息
// Green/Flow 流程控制相关
// Blue/Data 数据相关, 包含 Profile & Config & Settings
// Orange 警告性质，或者特别提醒
// Red 错误性质，或者特别严重的提醒

// 模式 ----------------------------------------------------------------------
// 1 PROJECT_NOTPRODUCTION_LOG 只适用于 非线上正式服环境下 的日志输出
// 2 PROJECT_LOG 只适用于Editor环境下的开发日志输出
namespace LowoUN.Util {
	public static class LLog {
		// 是否允许Unity运行时日志
		static void SetLogEnabled (bool isRuntimeLogEnabled) {
#if UNITY_EDITOR
			Debug.unityLogger.logEnabled = true;
#else
			Debug.unityLogger.logEnabled = isRuntimeLogEnabled;
#endif
		}
		// 设置Unity输出日志等级
		static void SetLogLevel () {
			// 用于错误类型
			var logType = LogType.Error;
			// Debug.unityLogger.Log(LogType.Error);
			// 仅输出 托管堆栈跟踪
			var traceType = StackTraceLogType.ScriptOnly;
			Application.SetStackTraceLogType (logType, traceType);
		}

		public static void Init (bool isDebug) {
			if (!isDebug) {
				LLog.SetOpen (0);
				return;
			}

			LLog.SetOpen (1);

			TextAsset txt = Resources.Load ("Setting_Log") as TextAsset;
			// 以换行符作为分割点，将该文本分割成若干行字符串，并以数组的形式来保存每行字符串的内容
			string[] str = txt.text.Split ('\n');
			// 将每行字符串的内容以逗号作为分割点，并将每个逗号分隔的字符串内容遍历输出
			for (int i = 0; i < str.Length; i++) {
				// Debug.Log("___"+str[i]);
				if (i == 0) {
					// MARK loywong 由项目GameSettings面板值决定
					// Log.SetOpen (int.Parse (str[0]));
					continue;
				}

				// Debug.Log("________"+str[i]);
				if (string.IsNullOrWhiteSpace (str[i]))
					continue;

				string[] ss = str[i].Split ('#');
				// Debug.Log("________ "+ss.Length);
				if (ss.Length == 1)
					LLog.OpenTag (str[i].Trim ());
			}
		}

		private static Dictionary<string, string> tags = new Dictionary<string, string> ();

		private static bool isOpen = false;
		// public static bool IsOpen => isOpen;
		static void SetOpen (int openState) {
			isOpen = openState == 1;
		}

		static void OpenTag (string tag) {
			if (!isOpen) return;

			tags[tag] = tag.ToString ();
			// Debug.Log("tag:"+tag);
		}

		// -------------------------------------------------------------
		// // [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Exception (params object[] msg) {
		// 	// if (!isOpen) return;
		// 	throw new System.Exception("【Editor临时-Exception】" + ParseMsg (msg));
		// }

		// 用于在非正式线上包（测试真机设备上，仍然输出Debug日志）
		// !SRV_ALIYUN_PRODUCTION -------------------------------------------------------------
		[System.Diagnostics.Conditional ("PROJECT_NOTPRODUCTION_LOG")]
		public static void NOT_PRODUCTION_ERROR (params object[] msg) {
			Debug.LogError ("【外网测试】" + ParseMsg (msg));
			// HandleWithColor ("FF5C95", ParseMsg (msg));
		}
		// MARK loywong 把 warning 留给Unity等非业务逻辑系统
		// [System.Diagnostics.Conditional ("PROJECT_NOTPRODUCTION_LOG")]
		// public static void NOT_PRODUCTION_WARN (params object[] msg) {
		// 	Debug.LogWarning ("【外网测试】" + ParseMsg (msg));
		// 	// HandleWithColor ("FFAE00", ParseMsg (msg));
		// }
		[System.Diagnostics.Conditional ("PROJECT_NOTPRODUCTION_LOG")]
		public static void NOT_PRODUCTION_LOG (params object[] msg) {
#if UNITY_EDITOR
			string color = "eeeeee";
#else
			string color = "666666";
#endif
			object msg2 = ParseMsg (msg);
			Debug.Log ("<color=#" + color + ">" + "【外网测试】" + msg2 + "</color>");

			// Debug.Log ("【!SRV_ALIYUN_PRODUCTION】" + ParseMsg (msg));
		}

		// PROJECT_LOG (Editor) -------------------------------------------------------------
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Error (params object[] msg) {
			if (!isOpen) return;
			Debug.LogError ("【Editor测试_不卡流程!!!】" + ParseMsg (msg));
		}
		// MARK loywong 把 warning 留给Unity等非业务逻辑系统
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Warn (params object[] msg) {
		// 	if (!isOpen) return;
		// 	Debug.LogWarning ("【Editor测试】" + ParseMsg (msg));
		// }
		// 颜色 999999 为了和系统的做区分 又不至于像FFFFFF那么亮 取值为cccccc好了
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Log (params object[] msg) {
			// Gray(msg);
			if (!isOpen) return;

			string color = "eeeeee";
			object msg2 = ParseMsg (msg);
			Debug.Log ("<color=#" + color + ">" + "【Editor测试】" + msg2 + "</color>");

			// Debug.Log ("【Editor测试】" + ParseMsg (msg));
		}
		// // MARK 避免和ET的Log.Console混淆
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Print (params object[] msg) {
		// 	Print(msg);
		// }
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Console (params object[] msg) {
		// 	Print(msg);
		// }
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Output (params object[] msg) {
		// 	Print(msg);
		// }
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Trace (params object[] msg) {
		// 	Print(msg);
		// }

		// // TODO loywong 计划移除，真正需要提示Error的地方，使用UnityEngine本身的Error
		// [System.Diagnostics.Conditional ("PROJECT_LOG")]
		// public static void Log (params object[] msg) {
		// 	// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
		// 	HandleWithColor ("FFFFFF", ParseMsg (msg));
		// }
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Gray (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("606060", ParseMsg (msg));
		}

		// 相当于 Warning
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Orange (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("FFAE00", ParseMsg (msg));
		}

		// 相当于 Error
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Red (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("FF5C95", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Green (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("90FF81", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Flow (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("90FF81", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Blue (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("3A5FCD", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Data (params object[] msg) {
			// Debug.LogError ("【Editor测试】" + ParseMsg (msg));
			HandleWithColor ("3A5FCD", ParseMsg (msg));
		}

		// -------------------------------------------------------------
		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_White (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "eeeeee", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_Gray (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "606060", msg);
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_Orange (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "FFAE00", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_Red (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "FF5C95", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_Green (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "90FF81", ParseMsg (msg));
		}

		[System.Diagnostics.Conditional ("PROJECT_LOG")]
		public static void Tag_Blue (string tag, params object[] msg) {
			HandleWithTagAndColor (tag, "3A5FCD", msg);
		}

		private static void HandleWithColor (string color, params object[] paramsMsg) {
			if (!isOpen) return;

			object msg = ParseMsg (paramsMsg);

			Debug.Log ("<color=#" + color + ">" + "【Editor测试】" + msg + "</color>");
		}
		private static void HandleWithTagAndColor (string tag, string color, params object[] paramsMsg) {
			if (!isOpen) return;

			if (!tags.ContainsKey (tag))
				return;

			object msg = ParseMsg (paramsMsg);

			Debug.Log ("<color=#" + color + ">" + "【Editor测试-[ " + tags[tag] + " ]】" + msg + "</color>");
		}

		// [System.Diagnostics.Conditional("PROJECT_LOG")]
		// 解第一层
		private static string ParseMsg (params object[] msg) {
			// Debug.Log ("ParseObjects() length: " + msg.Length);
			if (msg.Length == 1)
				return GetString (msg[0]);

			var str = "";

			for (int i = 0; i < msg.Length; i++) {
				var s = (i == 0 ? "" : ", ") + GetString (msg[i]);
				str += s;
			}

			return str;
		}

		// 解第二层
		// [System.Diagnostics.Conditional("PROJECT_LOG")]
		private static string GetString (object msg) {
			string detail = "";
			if (msg is ICollection)
				detail = Stringify (msg as ICollection);
			else
				detail = msg.ToString ();

			return detail;
		}

		// [System.Diagnostics.Conditional("PROJECT_LOG")]
		private static string Stringify (ICollection col) {
			var str = "";
			var isFirst = true;
			foreach (var item in col) {
				// item 还是有可能是集合，不递归解下去了！！！
				if (isFirst) {
					str += item.ToString ();
					isFirst = false;
				} else
					str += "+" + item.ToString ();
			}
			return str;
		}
	}
}