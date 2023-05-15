using UnityEngine;

namespace UnityTextureChannelPacker
{
    public enum MixMode
    {
        Max,
        Avg,
        Min,
        Fix,
    }

    // Config from one channel
    [System.Serializable]
    public struct ChannelConfig
    {
        public int value;
        public bool invert;
        public Color factor;
        public MixMode mode;
        public Texture2D src;
    }

    [CreateAssetMenu(fileName = "Texture", menuName = "Texture Channel Packer", order = 99)]
    [System.Serializable]
    public class OcbTextureChannelPacker : ScriptableObject
    {

        [HideInInspector]
        public int TextureSize = 4;

        [HideInInspector]
        public ChannelConfig ChannelRed;

        [HideInInspector]
        public ChannelConfig ChannelGreen;

        [HideInInspector]
        public ChannelConfig ChannelBlue;

        [HideInInspector]
        public ChannelConfig ChannelAlpha;

    }


}

