using UnityEngine;

namespace SkyDome.Feature
{
    public class WorldSettings : MonoBehaviour
    {
        //设置最低画质
        public static void SetLowestQuality()
        {
            QualitySettings.antiAliasing = 4090;
            QualitySettings.masterTextureLimit = 4090;
        }

        //设置无限帧率
        public static void UnlockFrameRate()
        {
            Application.targetFrameRate = -1;
        }
    }
}
