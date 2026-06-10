using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    [StaticConstructorOnStartup]
    internal static class RemoteControlTex
    {
        public static readonly Texture2D UseButtonOrLever =
            ContentFinder<Texture2D>.Get("UI/DEx/Buttons/UseButtonOrLever");

        public static readonly Texture2D SecuredRemotely =
            ContentFinder<Texture2D>.Get("UI/DEx/Buttons/SecuredRemotely");

        public static readonly Texture2D ConnectToButton =
            ContentFinder<Texture2D>.Get("UI/DEx/Buttons/ConnectToButton");

        public static readonly Texture2D DisconnectButton =
            ContentFinder<Texture2D>.Get("UI/DEx/Buttons/DisconnectButton");

        public static readonly Material SecuredOverlayMaterial =
            MaterialPool.MatFrom("UI/DEx/Overlays/DE_Secured", ShaderDatabase.Transparent);
    }
}
